#include <Arduino.h>
#include <ArduinoJson.h>
#include <LoRa.h>
#include <SPI.h>
#include <WiFi.h>

#include "board_def.h"

#ifndef IOT_PEER_NAME
#define IOT_PEER_NAME "lora1"
#endif

#ifndef IOT_DEVICE_ID
#define IOT_DEVICE_ID 11
#endif

#ifndef IOT_ENABLE_WIFI
#define IOT_ENABLE_WIFI 1
#endif

#ifndef IOT_WIFI_SSID
#define IOT_WIFI_SSID ""
#endif

#ifndef IOT_WIFI_PASSWORD
#define IOT_WIFI_PASSWORD ""
#endif

#ifndef IOT_UPSTREAM_HOST
#define IOT_UPSTREAM_HOST "pi51"
#endif

#ifndef IOT_UPSTREAM_PORT
#define IOT_UPSTREAM_PORT 5050
#endif

#define LORA_LED_PIN 4
#define LORA_PACKET_MAX 240
#define SEEN_MESSAGE_COUNT 24

struct PointDefinition
{
	int id;
	int deviceId;
	const char* name;
	int pin;
	const char* status0;
	const char* status1;
	bool output;
};

static PointDefinition points[] = {
	{8, 11, "LoRa1 Led1", LORA_LED_PIN, "Off", "On", true},
	{9, 12, "LoRa2 Led1", LORA_LED_PIN, "Off", "On", true},
};

WiFiClient upstreamClient;
String upstreamBuffer;
String seenMessageIds[SEEN_MESSAGE_COUNT];
int nextSeenMessageId = 0;
unsigned long nextWifiConnectAttempt = 0;
unsigned long nextPollAt = 0;
unsigned long nextStatusAt = 0;
bool ledStatus = false;

static PointDefinition* findPoint(int pointId)
{
	for (PointDefinition& point : points)
	{
		if (point.id == pointId)
			return &point;
	}

	return nullptr;
}

static bool isLocalPoint(const PointDefinition& point)
{
	return point.deviceId == IOT_DEVICE_ID;
}

static bool shouldForwardToLora(const PointDefinition* point)
{
#if IOT_ENABLE_WIFI
	return point == nullptr || point->deviceId != IOT_DEVICE_ID;
#else
	return false;
#endif
}

static bool shouldForwardToWifi(const PointDefinition* point)
{
#if IOT_ENABLE_WIFI
	return point == nullptr || point->deviceId != IOT_DEVICE_ID;
#else
	return false;
#endif
}

static String newMessageId()
{
	static uint32_t counter = 0;
	char id[24];
	snprintf(id, sizeof(id), "%08lx%08lx", millis(), counter++);
	return String(id);
}

static bool rememberMessageId(const char* messageId)
{
	if (messageId == nullptr || messageId[0] == '\0')
		return true;

	for (const String& seenMessageId : seenMessageIds)
	{
		if (seenMessageId == messageId)
			return false;
	}

	seenMessageIds[nextSeenMessageId] = messageId;
	nextSeenMessageId = (nextSeenMessageId + 1) % SEEN_MESSAGE_COUNT;
	return true;
}

static bool isActiveStatus(const char* status, const char* activeStatus)
{
	if (status == nullptr)
		return false;

	return strcasecmp(status, activeStatus) == 0 ||
		strcasecmp(status, "on") == 0 ||
		strcasecmp(status, "high") == 0 ||
		strcasecmp(status, "true") == 0 ||
		strcmp(status, "1") == 0;
}

static const char* currentLedStatus()
{
	return ledStatus ? "On" : "Off";
}

static void applyLocalPointControl(const PointDefinition& point, const char* requestedStatus)
{
	if (!point.output)
		return;

	ledStatus = isActiveStatus(requestedStatus, point.status1);
	digitalWrite(point.pin, ledStatus ? HIGH : LOW);
	Serial.printf("Point %d %s: %s\n", point.id, point.name, currentLedStatus());
}

static String createMessage(const char* type, JsonDocument& payload)
{
	JsonDocument message;
	message["type"] = type;
	message["payload"] = payload.as<JsonVariant>();
	message["id"] = newMessageId();
	message["sentAtUtc"] = "1970-01-01T00:00:00+00:00";

	String json;
	serializeJson(message, json);
	return json;
}

static void sendWifiLine(const String& json)
{
#if IOT_ENABLE_WIFI
	if (upstreamClient.connected())
		upstreamClient.println(json);
#endif
}

static void sendLoraLine(const String& json)
{
	if (json.length() > LORA_PACKET_MAX)
	{
		Serial.printf("LoRa packet too large: %u bytes\n", json.length());
		return;
	}

	LoRa.beginPacket();
	LoRa.print(json);
	LoRa.endPacket();
}

static void sendPointStatus(const PointDefinition& point)
{
	JsonDocument payload;
	payload["id"] = point.id;
	payload["status"] = currentLedStatus();
	String json = createMessage("point.status", payload);

	sendWifiLine(json);
#if IOT_ENABLE_WIFI
	sendLoraLine(json);
#else
	sendLoraLine(json);
#endif
}

static void sendHandshake()
{
	JsonDocument payload;
	payload["peerName"] = IOT_PEER_NAME;
	payload["protocolVersion"] = "1.0";
	JsonArray types = payload["supportedMessageTypes"].to<JsonArray>();
	types.add("handshake");
	types.add("handshake.ack");
	types.add("poll");
	types.add("poll.ack");
	types.add("peer.status");
	types.add("point.status");
	types.add("point.control");
	sendWifiLine(createMessage("handshake", payload));
}

static void sendPeerStatus()
{
	JsonDocument payload;
	payload["peerName"] = IOT_PEER_NAME;
	payload["state"] = "ready";
	payload["activeConnections"] = 1;
	sendWifiLine(createMessage("peer.status", payload));
}

static void sendPoll()
{
	JsonDocument payload;
	payload["peerName"] = IOT_PEER_NAME;
	payload["pollId"] = newMessageId();
	payload["sentAtUtc"] = "1970-01-01T00:00:00+00:00";
	sendWifiLine(createMessage("poll", payload));
}

static void handleMessage(const String& json, bool fromLora)
{
	JsonDocument message;
	DeserializationError error = deserializeJson(message, json);
	if (error)
	{
		Serial.printf("Invalid JSON: %s\n", error.c_str());
		return;
	}

	const char* type = message["type"] | "";
	const char* id = message["id"] | "";
	if (!rememberMessageId(id))
		return;

	JsonObject payload = message["payload"].as<JsonObject>();
	if (strcmp(type, "point.control") == 0)
	{
		int pointId = payload["id"] | 0;
		const char* status = payload["status"] | "";
		PointDefinition* point = findPoint(pointId);

		if (point != nullptr && isLocalPoint(*point))
		{
			applyLocalPointControl(*point, status);
			sendPointStatus(*point);
			return;
		}

		if (!fromLora && shouldForwardToLora(point))
			sendLoraLine(json);
		if (fromLora && shouldForwardToWifi(point))
			sendWifiLine(json);
		return;
	}

	if (strcmp(type, "point.status") == 0)
	{
		int pointId = payload["id"] | 0;
		PointDefinition* point = findPoint(pointId);
		if (fromLora && shouldForwardToWifi(point))
			sendWifiLine(json);
		if (!fromLora && shouldForwardToLora(point))
			sendLoraLine(json);
		return;
	}

	if (strcmp(type, "poll") == 0 && fromLora)
	{
		JsonDocument ackPayload;
		ackPayload["peerName"] = IOT_PEER_NAME;
		ackPayload["pollId"] = payload["pollId"] | id;
		ackPayload["receivedAtUtc"] = "1970-01-01T00:00:00+00:00";
		sendLoraLine(createMessage("poll.ack", ackPayload));
	}
}

static void pumpLora()
{
	int packetSize = LoRa.parsePacket();
	if (packetSize <= 0)
		return;

	String json;
	while (LoRa.available())
		json += static_cast<char>(LoRa.read());

	Serial.printf("LoRa rx RSSI %d: %s\n", LoRa.packetRssi(), json.c_str());
	handleMessage(json, true);
}

static void pumpWifi()
{
#if IOT_ENABLE_WIFI
	while (upstreamClient.connected() && upstreamClient.available())
	{
		char ch = static_cast<char>(upstreamClient.read());
		if (ch == '\n')
		{
			String line = upstreamBuffer;
			upstreamBuffer = "";
			line.trim();
			if (line.length() > 0)
				handleMessage(line, false);
		}
		else if (ch != '\r')
		{
			upstreamBuffer += ch;
		}
	}

	if (upstreamClient.connected())
		return;

	if (WiFi.status() != WL_CONNECTED || millis() < nextWifiConnectAttempt)
		return;

	nextWifiConnectAttempt = millis() + 10000;
	Serial.printf("Connecting upstream %s:%d\n", IOT_UPSTREAM_HOST, IOT_UPSTREAM_PORT);
	if (!upstreamClient.connect(IOT_UPSTREAM_HOST, IOT_UPSTREAM_PORT))
	{
		upstreamClient.stop();
		return;
	}

	upstreamClient.setNoDelay(true);
	sendHandshake();
	delay(100);
	sendPeerStatus();
#endif
}

static void initialiseWifi()
{
#if IOT_ENABLE_WIFI
	if (strlen(IOT_WIFI_SSID) == 0)
	{
		Serial.println("WiFi disabled: IOT_WIFI_SSID is empty.");
		return;
	}

	WiFi.mode(WIFI_STA);
	WiFi.begin(IOT_WIFI_SSID, IOT_WIFI_PASSWORD);
	Serial.printf("Connecting WiFi SSID %s", IOT_WIFI_SSID);
	while (WiFi.status() != WL_CONNECTED)
	{
		delay(500);
		Serial.print(".");
	}

	Serial.printf("\nWiFi connected: %s\n", WiFi.localIP().toString().c_str());
#endif
}

static void initialiseLora()
{
	SPI.begin(CONFIG_CLK, CONFIG_MISO, CONFIG_MOSI, CONFIG_NSS);
	LoRa.setPins(CONFIG_NSS, CONFIG_RST, CONFIG_DIO0);
	LoRa.setSpreadingFactor(12);
	LoRa.setSignalBandwidth(125E3);
	LoRa.setCodingRate4(5);
	LoRa.setPreambleLength(8);
	if (!LoRa.begin(BAND))
	{
		Serial.println("Starting LoRa failed.");
		while (true)
			delay(1000);
	}

	LoRa.setTxPower(20, PA_OUTPUT_PA_BOOST_PIN);
	LoRa.setSyncWord(0x12);
	LoRa.enableCrc();
}

void setup()
{
	Serial.begin(115200);
	delay(200);
	Serial.printf("\n%s starting as device %d\n", IOT_PEER_NAME, IOT_DEVICE_ID);

	pinMode(LORA_LED_PIN, OUTPUT);
	digitalWrite(LORA_LED_PIN, LOW);

	initialiseLora();
	initialiseWifi();

	nextPollAt = millis() + 15000;
	nextStatusAt = millis() + 3000;
}

void loop()
{
	pumpWifi();
	pumpLora();

	unsigned long now = millis();
	if (now >= nextPollAt)
	{
		nextPollAt = now + 30000;
		sendPoll();
	}

	if (now >= nextStatusAt)
	{
		nextStatusAt = now + 60000;
		for (PointDefinition& point : points)
		{
			if (isLocalPoint(point))
				sendPointStatus(point);
		}
	}

	delay(10);
}
