#include "PointStore.h"

namespace
{
	const Point defaultPoints[] = {
		{8, 11, "Lora1 Led1", "Lora1 Led1 Test", DigitalOutput, "4", 4, "", 0, "Off", "On", 1, ""},
		{9, 12, "Lora2 Led1", "Lora2 Led1 Test", DigitalOutput, "4", 4, "", 0, "Off", "On", 1, ""},
		{33, 11, "Lora1 Light1", "Lora1 Light1 Test", DigitalInput, "5", 5, "", 0, "Off", "On", 1, ""},
		{34, 12, "Lora2 Light1", "Lora2 Light1 Test", DigitalInput, "5", 5, "", 0, "Off", "On", 1, ""},
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
