using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;
using ProjectAPI.Models.Task_Attachment;
using ProjectAPI.Services;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Task_AttachmentController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserConnectionService _userConnectionService;

        public Task_AttachmentController(
            ApplicationDBContext context,
            IHubContext<NotificationHub> hubContext,
            UserConnectionService userConnectionService
        ){
            _context = context;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
        }

        [Authorize]
        [HttpPost()]
        public async Task<IActionResult> CreateAttachment([FromForm] TaskAttachmentDto task_Attachment) {
            try
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);

                if(user == null) return NotFound(new { success = false, message = "User not found."}); 

                var task = await _context.Tasks
                    .Include(t => t.Project)
                    .Include(t => t.Assignees)
                        .ThenInclude(a => a.Member)
                            .ThenInclude(m => m.User)
                    .FirstOrDefaultAsync(t => t.Id == task_Attachment.Task_Id);

                if(task == null) return NotFound(new { success = false, message = "Task not found"});

                var isAuthorize = await _context.Members.AnyAsync(m => m.Project_Id == task.Project_Id 
                    && m.User_Id == userId && (m.Role == "Admin" || m.Role == "Editor") && m.Status == "Active"); 

                if(!isAuthorize) return Unauthorized(new { success = false, message = "Access is restricted to administrators and editors only."});

                if (task_Attachment.File == null || task_Attachment.File.Length == 0)
                    return BadRequest("No file uploaded.");

                var memoryStream = new MemoryStream();
                await task_Attachment.File.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                var attachment = new Task_Attachment
                {
                    Task_Id = task_Attachment.Task_Id,
                    Name = task_Attachment.File.FileName,
                    Type = task_Attachment.File.ContentType,
                    Content = fileBytes
                };
                _context.Task_Attachments.Add(attachment);

                _context.Task_Histories.Add(new Task_History{
                    Action_Description = $"{user.Firstname} added an attachment",
                    New_Value  = attachment.Name,
                    Task_Id = task_Attachment.Task_Id,
                    Project_Id = task.Project_Id,
                    Date_Time = DateTime.Now,
                });

                foreach(var assignee in task.Assignees){
                        if(assignee.Member == null || task.Project == null || assignee.Member.User == null || assignee.Member.User.Id == userId) continue;

                        var newNotification = new Notification
                        {
                            Message = $"{user.Firstname} {user.Lastname} added an attachment in task \"{task.Task_Name}\" in project \"{task.Project.Title}\"",
                            User_id = assignee.Member.User.Id,
                            Task_id = task.Id,
                            Project_id = task.Project_Id,
                            Type = "AttachmentAdded",
                            Created_by = userId,
                            IsRead = false,
                            Date_time = DateTime.Now,
                            User = user
                        };

                        _context.Notifications.Add(newNotification);

                        if(_userConnectionService.GetConnections().TryGetValue(assignee.Member.User.Email, out var connectionId)){
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                        }
                    }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, attachment});
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = "An internal error occurred",
                    ex
                });
            }
        }


        [Authorize]
        [HttpGet("{task_Id}")]
        public async Task<IActionResult> GetAttachments(int task_Id)
        {
            try{
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);

                if(user == null) return NotFound(new { success = false, message = "User not found."}); 

                var task = await _context.Tasks.FindAsync(task_Id);

                if(task == null) return NotFound(new { success = false, message = "Task not found"});

                var member = await _context.Members.AnyAsync(m => 
                    m.Project_Id == task.Project_Id && 
                    m.User_Id == userId && 
                    m.Status == "Active"
                );

                if(!member) return Unauthorized(new { success= false, message = "Access is restricted to members only."});

                var attachments = await _context.Task_Attachments
                        .Where(a => a.Task_Id == task_Id)
                        .ToListAsync();

                // Return the file with the appropriate MIME type
                return Ok(new { success = true, attachments});
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500, new { 
                    success = false, 
                    message = $"An internal error occurred: {ex.Message}",
                });
            }
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAttachment(int id){
            try{
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);

                if(user == null) return NotFound(new { success = false, message = "User not found."}); 
                
                var attachment = await _context.Task_Attachments.FindAsync(id);

                if(attachment == null) return NotFound(new { success = false, message = "Attachment doesn't exist"});

                var task = await _context.Tasks
                    .Include(t => t.Project)
                    .Include(t => t.Assignees)
                        .ThenInclude(a => a.Member)
                            .ThenInclude(m => m.User)
                    .FirstOrDefaultAsync(t => t.Id == attachment.Task_Id);

                if(task == null) return NotFound(new { success = false, message = "This attachment has no linked task."});

                var isAdmin = await _context.Members
                    .AnyAsync(m => m.Project_Id == task.Project_Id && 
                    m.User_Id == userId && 
                    m.Role == "Admin" &&
                    m.Status == "Active"
                );

                if(!isAdmin) return Unauthorized(new { success = false, message = "Access is restricted to administrators only.."});

                _context.Task_Attachments.Remove(attachment);

                _context.Task_Histories.Add(new Task_History{
                    Action_Description = $"{user.Firstname} added an attachment",
                    Prev_Value = attachment.Name,
                    New_Value  = "Deleted",
                    Task_Id = task.Id,
                    Date_Time = DateTime.Now,
                    Project_Id = task.Project_Id,
                });

                foreach(var assignee in task.Assignees){
                    if(assignee.Member == null || task.Project == null || assignee.Member.User == null || assignee.Member.User.Id == userId) continue;

                    var newNotification = new Notification
                    {
                        Message = $"{user.Firstname} {user.Lastname} added an attachment in task \"{task.Task_Name}\" in project \"{task.Project.Title}\"",
                        User_id = assignee.Member.User.Id,
                        Task_id = task.Id,
                        Project_id = task.Project_Id,
                        Type = "AttachmentRemoved",
                        Created_by = userId,
                        IsRead = false,
                        Date_time = DateTime.Now,
                        User = user
                    };

                    _context.Notifications.Add(newNotification);

                    if(_userConnectionService.GetConnections().TryGetValue(assignee.Member.User.Email, out var connectionId)){
                        await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Attachment is successfully deleted."});

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500, new { 
                    success = false, 
                    message = "An internal error occurred",
                });
            }
        }

    }
}
