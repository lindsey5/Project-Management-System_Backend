using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

public class SignalHub : Hub
{
    // Map user names to connection IDs
    private static ConcurrentDictionary<string, string> userConnections = new();
    private readonly ApplicationDBContext _context;

    public SignalHub(ApplicationDBContext context)
    {
        _context = context;
    }

    public override Task OnConnectedAsync()
    {
        var context = Context;
        if (context != null)
        {
            var httpContext = context.GetHttpContext();
            var email = httpContext?.Request.Query["email"].ToString();

            if (!string.IsNullOrEmpty(email))
            {
                userConnections[email] = context.ConnectionId;
            }
        }

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        var email = userConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (email != null)
        {
            userConnections.TryRemove(email, out _);
        }

        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendRequestNotification(int project_id)
    {
        int count = await _context.Requests.CountAsync(r => r.Project_Id == project_id && r.Status == "Pending");
        var admins = await _context.Members
            .Include(m => m.User)
            .Where(m => m.Project_Id == project_id && m.Role == "Admin").ToListAsync();
        
        foreach(var admin in admins){
            if(admin.User != null && userConnections.TryGetValue(admin.User.Email, out var connectionId))
            await Clients.Client(connectionId).SendAsync("ReceiveRequestNotification", count);
        }
    }
}
