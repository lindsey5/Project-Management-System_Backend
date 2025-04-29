using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Services;
using Task = System.Threading.Tasks.Task;

public class NotificationHub : Hub
{
    private readonly ApplicationDBContext _context;
    private readonly UserConnectionService _userConnectionService;

    public NotificationHub(ApplicationDBContext context, UserConnectionService userConnectionService)
    {
        _context = context;
        _userConnectionService = userConnectionService;
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
                _userConnectionService.AddConnection(email, context.ConnectionId);
            }
        }

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        _userConnectionService.RemoveConnection(Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendRequestNotification(int project_id)
    {
        int count = await _context.Requests.CountAsync(r => r.Project_Id == project_id && r.Status == "Pending");
        var admins = await _context.Members
            .Include(m => m.User)
            .Where(m => m.Project_Id == project_id && m.Role == "Admin").ToListAsync();
        
        foreach(var admin in admins){
            if(admin.User != null && _userConnectionService.GetConnections().TryGetValue(admin.User.Email, out var connectionId))
            await Clients.Client(connectionId).SendAsync("ReceiveRequestNotification", count);
        }
    }
}
