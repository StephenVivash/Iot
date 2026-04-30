#pragma once

#include <stddef.h>

enum PointType
{
	DigitalInput = 1,
	DigitalOutput = 2,
	AnalogInput = 3,
	AnalogOutput = 4,
	PwmOutput = 5,
	Tm1637 = 6,
	Bmp280 = 7,
	ShiftInput = 8,
	ShifOutput = 9,
	Sequencer = 10
};

struct Point
{
	int id;
	int deviceId;
	const char* name;
	const char* description;
	PointType typeId;
	const char* address;
	int pin;
	const char* status;
	double rawStatus;
	const char* status0;
	const char* status1;
	double scale;
	const char* units;
};

class PointStore
{
public:
	PointStore(const Point* points, size_t pointCount);

	const Point* Find(int id) const;
	size_t GetForDevice(int deviceId, const Point** matches, size_t capacity) const;

	static PointStore CreateDefault();

private:
	const Point* _points;
	size_t _pointCount;
};
