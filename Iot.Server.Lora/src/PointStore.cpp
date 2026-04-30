#include "PointStore.h"

namespace
{
	const Point defaultPoints[] = {
		{8, 11, "Lora1 Led1", DigitalOutput, "4", "", "Off", "On", 1, 0, ""},
		{9, 12, "Lora2 Led1", DigitalOutput, "4", "", "Off", "On", 1, 0, ""},
		{33, 11, "Lora1 Light1", DigitalInput, "5", "", "Off", "On", 1, 0, ""},
		{34, 12, "Lora2 Light1", DigitalInput, "5", "", "Off", "On", 1, 0, ""},
		{35, 11, "Lora Supply", AnalogInput, "PIN=32", "", "", "", 0.000805, 0.2, "Volts"},
		{36, 11, "Lora Temp", AnalogInput, "PIN=33", "", "", "", 0.02444, 10, "°C"},
	};
}

PointStore::PointStore(const Point* points, size_t pointCount)
	: _points(points),
	  _pointCount(pointCount)
{
}

const Point* PointStore::Find(int id) const
{
	for (size_t i = 0; i < _pointCount; i++)
	{
		if (_points[i].id == id)
			return &_points[i];
	}

	return nullptr;
}

size_t PointStore::GetForDevice(int deviceId, const Point** matches, size_t capacity) const
{
	size_t count = 0;
	for (size_t i = 0; i < _pointCount; i++)
	{
		if (_points[i].deviceId != deviceId)
			continue;

		if (count < capacity)
			matches[count] = &_points[i];

		count++;
	}

	return count;
}

PointStore PointStore::CreateDefault()
{
	return PointStore(defaultPoints, sizeof(defaultPoints) / sizeof(defaultPoints[0]));
}
