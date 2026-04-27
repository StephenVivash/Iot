using Iot.Data;
using Iot.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PeerJsonSockets;

internal sealed record PointControlRoute(
	PeerConnection Connection,
	int TargetDeviceId,
	string TargetDeviceName,
	int NextHopDeviceId,
	string NextHopDeviceName);

internal static class PointControlRouteResolver
{
	public static async Task<PointControlRoute?> ResolveAsync(
		IotDatabase database,
		string localPeerName,
		int pointId,
		PeerConnection sourceConnection,
		IEnumerable<PeerConnection> candidateConnections,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		await using AppDbContext dbContext = database.CreateDbContext();
		Device[] devices = await dbContext.Devices.AsNoTracking().ToArrayAsync(cancellationToken);
		Point? point = await dbContext.Points.AsNoTracking().FirstOrDefaultAsync(point => point.Id == pointId, cancellationToken);
		if (point is null)
		{
			logger.LogWarning("Point control route lookup failed for unknown point {PointId}.", pointId);
			return null;
		}

		Device? localDevice = FindDeviceByName(devices, localPeerName);
		if (localDevice is null)
		{
			logger.LogWarning("Point control route lookup failed because local peer {PeerName} is not in the device hierarchy.",
				localPeerName);
			return null;
		}

		if (!TryBuildRoute(devices, localDevice.Id, point.DeviceId, out int[] routeDeviceIds))
		{
			logger.LogWarning("Point control route lookup failed from device {LocalDeviceId} to {TargetDeviceId} for point {PointId}.",
				localDevice.Id, point.DeviceId, pointId);
			return null;
		}

		if (routeDeviceIds.Length < 2)
		{
			logger.LogWarning("Point control for point {PointId} resolved to local device {DeviceId}, but it was not handled locally.",
				pointId, localDevice.Id);
			return null;
		}

		int nextHopDeviceId = routeDeviceIds[1];
		Device? nextHopDevice = devices.FirstOrDefault(device => device.Id == nextHopDeviceId);
		Device? targetDevice = devices.FirstOrDefault(device => device.Id == point.DeviceId);
		if (nextHopDevice is null || targetDevice is null)
		{
			logger.LogWarning("Point control route lookup found an invalid route from device {LocalDeviceId} to target device {TargetDeviceId} for point {PointId}.",
				localDevice.Id, point.DeviceId, pointId);
			return null;
		}

		PeerConnection[] routeConnections = candidateConnections
			.Where(connection => connection.Id != sourceConnection.Id)
			.Where(connection => !connection.CancellationToken.IsCancellationRequested)
			.Where(connection => IsConnectionForDevice(devices, connection, nextHopDeviceId))
			.ToArray();

		if (routeConnections.Length == 0)
		{
			logger.LogWarning("Point control route from {LocalDeviceName} to {TargetDeviceName} for point {PointId} requires connection to {NextHopDeviceName}.",
				localDevice.Name, targetDevice.Name, pointId, nextHopDevice.Name);
			return null;
		}

		if (routeConnections.Length > 1)
		{
			logger.LogWarning("Point control route from {LocalDeviceName} to {TargetDeviceName} for point {PointId} found {ConnectionCount} connections for {NextHopDeviceName}; ambiguous.",
				localDevice.Name, targetDevice.Name, pointId, routeConnections.Length, nextHopDevice.Name);
			return null;
		}

		return new PointControlRoute(routeConnections[0], targetDevice.Id, targetDevice.Name, nextHopDevice.Id, nextHopDevice.Name);
	}

	private static bool TryBuildRoute(Device[] devices, int localDeviceId, int targetDeviceId, out int[] routeDeviceIds)
	{
		routeDeviceIds = [];

		if (!TryBuildAncestorPath(devices, localDeviceId, out int[] localAncestors) ||
			!TryBuildAncestorPath(devices, targetDeviceId, out int[] targetAncestors))
		{
			return false;
		}

		HashSet<int> targetAncestorIds = targetAncestors.ToHashSet();
		int commonDeviceId = localAncestors.FirstOrDefault(targetAncestorIds.Contains);
		if (commonDeviceId == 0)
			return false;

		List<int> route = [];
		foreach (int deviceId in localAncestors)
		{
			route.Add(deviceId);
			if (deviceId == commonDeviceId)
				break;
		}

		int commonIndexInTarget = Array.IndexOf(targetAncestors, commonDeviceId);
		for (int i = commonIndexInTarget - 1; i >= 0; i--)
			route.Add(targetAncestors[i]);

		routeDeviceIds = route.ToArray();
		return routeDeviceIds.Length > 0;
	}

	private static bool TryBuildAncestorPath(Device[] devices, int deviceId, out int[] ancestorDeviceIds)
	{
		Dictionary<int, Device> devicesById = devices.ToDictionary(device => device.Id);
		List<int> ancestors = [];
		HashSet<int> visited = [];
		int currentDeviceId = deviceId;

		while (devicesById.TryGetValue(currentDeviceId, out Device? device))
		{
			if (!visited.Add(device.Id))
				return Fail(out ancestorDeviceIds);

			ancestors.Add(device.Id);
			if (device.ParentDeviceId == device.Id || device.ParentDeviceId <= 0)
			{
				ancestorDeviceIds = ancestors.ToArray();
				return true;
			}

			currentDeviceId = device.ParentDeviceId;
		}

		return Fail(out ancestorDeviceIds);
	}

	private static bool Fail(out int[] ancestorDeviceIds)
	{
		ancestorDeviceIds = [];
		return false;
	}

	private static bool IsConnectionForDevice(Device[] devices, PeerConnection connection, int deviceId)
	{
		if (string.IsNullOrWhiteSpace(connection.RemotePeerName))
			return false;

		Device? remoteDevice = FindDeviceByName(devices, connection.RemotePeerName);
		return remoteDevice?.Id == deviceId;
	}

	private static Device? FindDeviceByName(Device[] devices, string peerName) =>
		devices.FirstOrDefault(device => string.Equals(device.Name, peerName, StringComparison.OrdinalIgnoreCase));
}
