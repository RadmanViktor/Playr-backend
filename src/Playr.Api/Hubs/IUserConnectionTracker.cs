namespace Playr.Api.Hubs;

/// <summary>
/// Tracks how many active SignalR connections each user currently has.
/// A user can have multiple simultaneous connections (multiple tabs, or a brief overlap
/// between an old connection closing and a new one opening during a page refresh),
/// so presence must only flip to Offline once the LAST connection for a user disappears.
/// </summary>
public interface IUserConnectionTracker
{
    /// <summary>Registers a connection for the user. Returns the number of active connections after adding.</summary>
    int AddConnection(Guid userId, string connectionId);

    /// <summary>Removes a connection for the user. Returns the number of active connections remaining after removal.</summary>
    int RemoveConnection(Guid userId, string connectionId);

    /// <summary>Returns whether the user currently has any active connections.</summary>
    bool HasConnections(Guid userId);
}
