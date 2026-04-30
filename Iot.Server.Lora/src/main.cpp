#include <Arduino.h>
#include <ArduinoJson.h>
#include <LoRa.h>
#include <SPI.h>
#include <WiFi.h>

#include "board_def.h"
#include "PointStore.h"

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

//#define LORA_LED_PIN 4
#define LORA_PACKET_MAX 240
#define SEEN_MESSAGE_COUNT 24
#define DISPLAY_LOG_LINES 6
#define LORA_ACK_TIMEOUT_MS 5000
#define LORA_MAX_SEND_ATTEMPTS 4

struct PendingLoraMessage
{
	bool active;
	String json;
	String id;
	String type;
	int attempts;
	unsigned long nextAttemptAt;
	bool localStatusReport;
};

struct LocalPointState
{
	const Point* point;
	bool currentActive;
	bool lastReportedActive;
};

static const int LOCAL_POINT_CAPACITY = 8;

static PointStore pointStore = PointStore::CreateDefault();
static LocalPointState localPoints[LOCAL_POINT_CAPACITY];
static int localPointCount = 0;

WiFiClient upstreamClient;
OLED_CLASS_OBJ display(OLED_ADDRESS, OLED_SDA, OLED_SCL);
String upstreamBuffer;
String seenMessageIds[SEEN_MESSAGE_COUNT];
String displayLogLines[DISPLAY_LOG_LINES];
PendingLoraMessage pendingLoraMessage = {};
int nextSeenMessageId = 0;
unsigned long nextWifiConnectAttempt = 0;
unsigned long nextPollAt = 0;
unsigned long nextStatusAt = 0;
bool displayReady = false;

static String compactLogLine(const String& line)
{
	const int maxLength = 21;
	if (line.length() <= maxLength)
		return line;

	return line.substring(0, maxLength);
}

static void renderDisplayLog()
{
	if (!displayReady)
		return;

	display.clear();
	display.setTextAlignment(TEXT_ALIGN_LEFT);
	display.setFont(ArialMT_Plain_10);
	for (int i = 0; i < DISPLAY_LOG_LINES; i++)
		display.drawString(0, i * 10, displayLogLines[i]);
	display.display();
}

static void logEvent(const String& line)
{
	Serial.println(line);
	for (int i = 0; i < DISPLAY_LOG_LINES - 1; i++)
		displayLogLines[i] = displayLogLines[i + 1];

	displayLogLines[DISPLAY_LOG_LINES - 1] = compactLogLine(line);
	renderDisplayLog();
}

static void initialiseDisplay()
{
	if (OLED_RST > 0)
	{
		pinMode(OLED_RST, OUTPUT);
		digitalWrite(OLED_RST, HIGH);
		delay(100);
		digitalWrite(OLED_RST, LOW);
		delay(100);
		digitalWrite(OLED_RST, HIGH);
	}

	displayReady = display.init();
	if (!displayReady)
	{
		Serial.println("OLED init failed.");
		return;
	}

	display.flipScreenVertically();
	display.clear();
	display.setTextAlignment(TEXT_ALIGN_LEFT);
	display.setFont(ArialMT_Plain_16);
	display.display();
	logEvent(String(IOT_PEER_NAME) + " display ready");
}

static const Point* findPoint(int pointId)
{
	return pointStore.Find(pointId);
}

static bool isLocalPoint(const Point& point)
{
	return point.deviceId == IOT_DEVICE_ID;
}

static bool shouldForwardToLora(const Point* point)
{
#if IOT_ENABLE_WIFI
	return point == nullptr || point->deviceId != IOT_DEVICE_ID;
#else
	return false;
#endif
}

static bool shouldForwardToWifi(const Point* point)
{
#if IOT_ENABLE_WIFI
	return point == nullptr || point->deviceId != IOT_DEVICE_ID;
#else
	return false;
#endif
}

static void writePointFields(JsonObject payload, const Point& point)
{
	payload["id"] = point.id;
	payload["deviceId"] = point.deviceId;
	payload["name"] = point.name;
	payload["description"] = point.description;
	payload["typeId"] = static_cast<int>(point.typeId);
	payload["address"] = point.address;
	payload["pin"] = point.pin;
	payload["status"] = point.status;
	payload["rawStatus"] = point.rawStatus;
	payload["status0"] = point.status0;
	payload["status1"] = point.status1;
	payload["scale"] = point.scale;
	payload["units"] = point.units;
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

static LocalPointState* findLocalPointState(const Point& point)
{
	for (int i = 0; i < localPointCount; i++)
	{
		if (localPoints[i].point == &point || localPoints[i].point->id == point.id)
			return &localPoints[i];
	}

	return nullptr;
}

static const char* currentPointStatus(const Point& point, bool active)
{
	return active ? point.status1 : point.status0;
}

static bool readLocalPointActive(const Point& point)
{
	return digitalRead(point.pin) == HIGH;
}

static void forceLocalStatusRetry()
{
	for (int i = 0; i < localPointCount; i++)
		localPoints[i].lastReportedActive = !localPoints[i].currentActive;
}

static void applyLocalPointControl(const Point& point, const char* requestedStatus)
{
	if (point.typeId != DigitalOutput)
		return;

	bool active = isActiveStatus(requestedStatus, point.status1);
	digitalWrite(point.pin, active ? HIGH : LOW);

	LocalPointState* state = findLocalPointState(point);
	if (state != nullptr)
		state->currentActive = active;

	logEvent("Point " + String(point.id) + " " + currentPointStatus(point, active));
}

static String createMessage(const char* type, JsonDocument& payload, String* messageId = nullptr)
{
	String id = newMessageId();
	JsonDocument message;
	message["type"] = type;
	message["payload"] = payload.as<JsonVariant>();
	message["id"] = id;
	message["sentAtUtc"] = "1970-01-01T00:00:00+00:00";

	String json;
	serializeJson(message, json);
	if (messageId != nullptr)
		*messageId = id;

	return json;
}

static String createPointStatusMessage(const Point& point, const char* status, bool includePointFields, String* messageId = nullptr)
{
	JsonDocument payload;
	if (includePointFields)
		writePointFields(payload.to<JsonObject>(), point);
	else
		payload["id"] = point.id;

	payload["status"] = status;
	return createMessage("point.status", payload, messageId);
}

static void sendWifiLine(const String& json)
{
#if IOT_ENABLE_WIFI
	if (upstreamClient.connected())
		upstreamClient.println(json);
#endif
}

static bool sendRawLoraLine(const String& json)
{
	if (json.length() > LORA_PACKET_MAX)
	{
		logEvent("LoRa tx too large " + String(json.length()));
		return false;
	}

	LoRa.beginPacket();
	LoRa.print(json);
	LoRa.endPacket();
	logEvent("LoRa tx " + String(json.length()) + "b");
	return true;
}

static void sendLoraLine(const String& json)
{
	sendRawLoraLine(json);
}

static void clearPendingLoraMessage(bool retryLocalStatusLater)
{
	if (retryLocalStatusLater && pendingLoraMessage.localStatusReport)
		forceLocalStatusRetry();

	pendingLoraMessage.active = false;
	pendingLoraMessage.json = "";
	pendingLoraMessage.id = "";
	pendingLoraMessage.type = "";
	pendingLoraMessage.localStatusReport = false;
}

static void sendReliableLoraLine(const String& json, const char* messageId, const char* messageType, bool localStatusReport = false)
{
	if (messageId == nullptr || messageId[0] == '\0')
	{
		sendLoraLine(json);
		return;
	}

	if (sendRawLoraLine(json))
	{
		if (pendingLoraMessage.active && pendingLoraMessage.id != messageId)
		{
			logEvent("LoRa pending replaced");
			clearPendingLoraMessage(true);
		}

		pendingLoraMessage.active = true;
		pendingLoraMessage.json = json;
		pendingLoraMessage.id = messageId;
		pendingLoraMessage.type = messageType == nullptr ? "" : messageType;
		pendingLoraMessage.attempts = 1;
		pendingLoraMessage.nextAttemptAt = millis() + LORA_ACK_TIMEOUT_MS;
		pendingLoraMessage.localStatusReport = localStatusReport;
	}
}

static void sendLoraAck(const char* messageId, const char* messageType)
{
	if (messageId == nullptr || messageId[0] == '\0')
		return;

	JsonDocument payload;
	payload["ackId"] = messageId;
	payload["ackType"] = messageType;
	sendRawLoraLine(createMessage("message.ack", payload));
}

static void handleLoraAck(JsonObject payload)
{
	const char* ackId = payload["ackId"] | "";
	if (!pendingLoraMessage.active || pendingLoraMessage.id != ackId)
		return;

	logEvent("LoRa ack " + pendingLoraMessage.type);
	clearPendingLoraMessage(false);
}

static void pumpLoraRetries()
{
	if (!pendingLoraMessage.active || millis() < pendingLoraMessage.nextAttemptAt)
		return;

	if (pendingLoraMessage.attempts >= LORA_MAX_SEND_ATTEMPTS)
	{
		logEvent("LoRa no ack " + pendingLoraMessage.type);
		clearPendingLoraMessage(true);
		return;
	}

	pendingLoraMessage.attempts++;
	pendingLoraMessage.nextAttemptAt = millis() + LORA_ACK_TIMEOUT_MS;
	logEvent("LoRa retry " + String(pendingLoraMessage.attempts));
	sendRawLoraLine(pendingLoraMessage.json);
}

static void sendPointStatus(const Point& point, bool toWifi, bool toLora)
{
	bool active = readLocalPointActive(point);
	const char* status = currentPointStatus(point, active);
	LocalPointState* state = findLocalPointState(point);
	if (state != nullptr)
		state->currentActive = active;

	String messageId;
	logEvent("Status p" + String(point.id) + "=" + String(status));

	if (toWifi)
		sendWifiLine(createPointStatusMessage(point, status, true));
	if (toLora)
	{
		String json = createPointStatusMessage(point, status, false, &messageId);
		sendReliableLoraLine(json, messageId.c_str(), "point.status", true);
	}
}

static void sendPointStatusIfChanged(const Point& point, bool toWifi, bool toLora)
{
	LocalPointState* state = findLocalPointState(point);
	if (state == nullptr)
		return;

	bool active = readLocalPointActive(point);
	state->currentActive = active;
	if (state->lastReportedActive == active)
		return;

	if (toLora && pendingLoraMessage.active)
		return;

	sendPointStatus(point, toWifi, toLora);
	state->lastReportedActive = active;
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
	types.add("message.ack");
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
		logEvent("JSON error " + String(error.c_str()));
		return;
	}

	const char* type = message["type"] | "";
	const char* id = message["id"] | "";
	JsonObject payload = message["payload"].as<JsonObject>();

	if (fromLora && strcmp(type, "message.ack") == 0)
	{
		handleLoraAck(payload);
		return;
	}

	if (fromLora && (strcmp(type, "point.control") == 0 || strcmp(type, "point.status") == 0))
		sendLoraAck(id, type);

	if (!rememberMessageId(id))
		return;

	logEvent(String(fromLora ? "LoRa rx " : "WiFi rx ") + type);
	if (strcmp(type, "point.control") == 0)
	{
		int pointId = payload["id"] | 0;
		const char* status = payload["status"] | "";
		const Point* point = findPoint(pointId);

		if (point != nullptr && isLocalPoint(*point))
		{
			if (point->typeId != DigitalOutput)
			{
				logEvent("Reject control p" + String(point->id));
				sendPointStatus(*point, !fromLora, fromLora);
				return;
			}

			applyLocalPointControl(*point, status);
			logEvent(String(fromLora ? "Reply LoRa p" : "Reply WiFi p") + String(point->id));
			sendPointStatusIfChanged(*point, !fromLora, fromLora);
			return;
		}

		if (!fromLora && shouldForwardToLora(point))
		{
			logEvent("Forward LoRa p" + String(pointId));
			sendReliableLoraLine(json, id, type);
		}
		if (fromLora && shouldForwardToWifi(point))
		{
			logEvent("Forward WiFi p" + String(pointId));
			sendWifiLine(json);
		}
		return;
	}

	if (strcmp(type, "point.status") == 0)
	{
		int pointId = payload["id"] | 0;
		const char* status = payload["status"] | "";
		const Point* point = findPoint(pointId);
		if (fromLora && shouldForwardToWifi(point))
		{
			logEvent("Forward WiFi p" + String(pointId));
			if (point != nullptr)
				sendWifiLine(createPointStatusMessage(*point, status, true));
			else
				sendWifiLine(json);
		}
		if (!fromLora && shouldForwardToLora(point))
		{
			logEvent("Forward LoRa p" + String(pointId));
			sendReliableLoraLine(json, id, type);
		}
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

	logEvent("LoRa rssi " + String(LoRa.packetRssi()));
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
	logEvent("Upstream connect");
	if (!upstreamClient.connect(IOT_UPSTREAM_HOST, IOT_UPSTREAM_PORT))
	{
		upstreamClient.stop();
		logEvent("Upstream failed");
		return;
	}

	upstreamClient.setNoDelay(true);
	logEvent("Upstream ready");
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
		logEvent("WiFi disabled");
		return;
	}

	WiFi.mode(WIFI_STA);
	WiFi.begin(IOT_WIFI_SSID, IOT_WIFI_PASSWORD);
	logEvent("WiFi connecting");
	while (WiFi.status() != WL_CONNECTED)
	{
		delay(500);
		Serial.print(".");
	}

	logEvent("WiFi " + WiFi.localIP().toString());
#endif
}

static void initialiseLocalPoints()
{
	const Point* matches[LOCAL_POINT_CAPACITY];
	size_t count = pointStore.GetForDevice(IOT_DEVICE_ID, matches, LOCAL_POINT_CAPACITY);
	localPointCount = static_cast<int>(count > LOCAL_POINT_CAPACITY ? LOCAL_POINT_CAPACITY : count);

	for (int i = 0; i < localPointCount; i++)
	{
		const Point* point = matches[i];
		if (point->typeId == DigitalInput)
			pinMode(point->pin, INPUT);
		else if (point->typeId == DigitalOutput)
		{
			pinMode(point->pin, OUTPUT);
			digitalWrite(point->pin, LOW);
		}

		bool active = readLocalPointActive(*point);
		localPoints[i].point = point;
		localPoints[i].currentActive = active;
		localPoints[i].lastReportedActive = !active;
		logEvent("Local point " + String(point->id) + " pin " + String(point->pin));
	}
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
		logEvent("LoRa failed");
		while (true)
			delay(1000);
	}

	LoRa.setTxPower(20, PA_OUTPUT_PA_BOOST_PIN);
	LoRa.setSyncWord(0x12);
	LoRa.enableCrc();
	logEvent("LoRa ready");
}

void setup()
{
	Serial.begin(115200);
	delay(200);
	Serial.printf("\n%s starting as device %d\n", IOT_PEER_NAME, IOT_DEVICE_ID);
	initialiseDisplay();
	logEvent("Device " + String(IOT_DEVICE_ID));

	initialiseLocalPoints();
	initialiseLora();
	initialiseWifi();

	nextPollAt = millis() + 60000;
	nextStatusAt = millis() + 500;
}

void loop()
{
	pumpWifi();
	pumpLora();
	pumpLoraRetries();

	unsigned long now = millis();
	if (now >= nextPollAt)
	{
		nextPollAt = now + 60000;
		sendPoll();
	}

	if (now >= nextStatusAt)
	{
		nextStatusAt = now + 500;
		for (int i = 0; i < localPointCount; i++)
		{
			const Point& point = *localPoints[i].point;
			sendPointStatusIfChanged(point, IOT_ENABLE_WIFI != 0, IOT_ENABLE_WIFI == 0);
		}
	}


}
