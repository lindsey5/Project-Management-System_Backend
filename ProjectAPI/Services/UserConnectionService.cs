using System;
using System.Collections.Concurrent;

namespace ProjectAPI.Services;

public class UserConnectionService {
    private static ConcurrentDictionary<string, string> userConnections = new();

    public void AddConnection(string email, string connectionId) {
        userConnections[email] = connectionId;
    }

    public void RemoveConnection(string connectionId) {
        var email = userConnections.FirstOrDefault(x => x.Value == connectionId).Key;
        if (email != null)
        {
            userConnections.TryRemove(email, out _);
        }
    }

    public ConcurrentDictionary<string, string> GetConnections() {
        return userConnections;
    }
}
