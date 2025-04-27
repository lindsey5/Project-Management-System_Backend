using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;
using System.Collections.Concurrent;
using Task = System.Threading.Tasks.Task;

public class NotificationHub : Hub
{
    // Map user names to connection IDs
    private static ConcurrentDictionary<string, string> userConnections = new();
    private readonly ApplicationDBContext _context;

    public NotificationHub(ApplicationDBContext context)
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

    public async Task SendUserNotification(int user_id, int project_id, int task_id, string message, string type)
    {
        var User = await _context.Users.FindAsync(user_id);

        if(User != null && userConnections.TryGetValue(User.Email, out var connectionId))
        {
            _context.Notifications.Add(new Notification{
                Message = message,
                User_id = user_id,
                Task_id = task_id,
                Project_id = project_id,
                Notification_type = type
            });
            var count = await _context.Notifications.CountAsync(n => n.User_id == user_id);
            await Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", count);
        }
    }

    /*    public async Task SendUserNotification(int user_id, int task_id, int project_id, string message, string type)
    {

        
        foreach(var admin in admins){
            if(admin.User != null && userConnections.TryGetValue(admin.User.Email, out var connectionId))
            await Clients.Client(connectionId).SendAsync("ReceiveRequestNotification", count);
        }
    }*/
}
