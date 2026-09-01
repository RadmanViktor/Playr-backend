using System.Collections.Concurrent;

namespace Playr.Api.Hubs;

/// <summary>
/// In-memory, singleton tracker of active SignalR connection ids per user.
/// </summary>
public sealed class UserConnectionTracker : IUserConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> connectionsByUser = new();

    public int AddConnection(Guid userId, string connectionId)
    {
        lock (connectionsByUser)
        {
            var connections = connectionsByUser.GetOrAdd(userId, static _ => new HashSet<string>());
            connections.Add(connectionId);
            return connections.Count;
        }
    }

    public int RemoveConnection(Guid userId, string connectionId)
    {
        lock (connectionsByUser)
        {
            if (!connectionsByUser.TryGetValue(userId, out var connections))
            {
                return 0;
            }

            connections.Remove(connectionId);
            var remaining = connections.Count;
            if (remaining == 0)
            {
                connectionsByUser.TryRemove(userId, out _);
            }

            return remaining;
        }
    }

    public bool HasConnections(Guid userId)
    {
        lock (connectionsByUser)
        {
            return connectionsByUser.TryGetValue(userId, out var connections) && connections.Count > 0;
        }
    }
}
