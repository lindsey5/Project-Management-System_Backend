using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.DTOs;
using ProjectAPI.Models;
using ProjectAPI.Services;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserConnectionService _userConnectionService;

        public CommentController(ApplicationDBContext context, IHubContext<NotificationHub> hubContext,
            UserConnectionService userConnectionService)
        {
            _context = context;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
        }

        [Authorize]
        [HttpPost()]
        public async Task<IActionResult> CreateComment([FromBody] CommentCreateDto newComment){
            try
                {
                    var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                        return Unauthorized(new { success = false, message = "Invalid user token" });

                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                        return NotFound(new { success = false, message = "User not found" });
                    
                    var task = await _context.Tasks
                    .Include(t => t.Assignees)
                        .ThenInclude(a => a.Member)
                        .ThenInclude(m => m.User)
                    .Include(t => t.Project)
                    .FirstOrDefaultAsync(t => t.Id == newComment.Task_Id);

                    if(task == null) return NotFound(new { success = false, message = "Task not found"});
                    
                    var member = await _context.Members.FirstOrDefaultAsync(m => 
                        m.User_Id == userId && m.Project_Id == task.Project_Id && m.Status == "Active"
                    );

                    if(member == null) return Unauthorized(new { success = false, message = "Unauthorized you must be a member of the project"});

                    var comment = new Comment{
                        Task_Id = newComment.Task_Id,
                        Member_Id = member.Id,
                        Content = newComment.Content,
                        Date_time = DateTime.Now,
                    };

                    _context.Task_Histories.Add(new Task_History
                    {
                        Task_Id = comment.Task_Id,
                        Project_Id = task.Project_Id,
                        Action_Description = $"{user.Firstname} added a comment",
                        Date_Time = DateTime.Now
                    });

                    foreach(var assignee in task.Assignees){
                        if(assignee.Member == null || assignee.Member.User == null || task.Project == null || assignee.Member.User.Id == userId) continue;
                        
                        var newNotification = new Notification
                        {
                            Message = $"{assignee.Member.User.Firstname} {assignee.Member.User.Lastname} added a comment to task \"{task.Task_Name}\" in project \"{task.Project.Title}\"",
                            User_id = assignee.Member.User.Id,
                            Task_id = task.Id,
                            Project_id = task.Project_Id,
                            Type = "CommentAdded",
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

                    _context.Comments.Add(comment);
                    await _context.SaveChangesAsync();

                    return Ok(new { success = true, comment});
                    
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "An internal error occurred. Please try again later.",
                        error = ex.Message
                    });
                }
        }

        [Authorize]
        [HttpGet("{task_id}")]
        public async Task<IActionResult> GetComments(int task_id, [FromQuery] int page = 1){
            try
                {
                    int limit = 5;

                    var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                        return Unauthorized(new { success = false, message = "Invalid user token" });

                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                        return NotFound(new { success = false, message = "User not found" });
                    
                    var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == task_id);

                    if(task == null) return NotFound(new { success = false, message = "Task not found"});
                    
                    var member = await _context.Members.FirstOrDefaultAsync(m => 
                        m.User_Id == userId && m.Project_Id == task.Project_Id && m.Status == "Active"
                    );

                    if(member == null) return Unauthorized(new { success = false, message = "Unauthorized you must be a member of the project"});

                    var totalComments = await _context.Comments
                        .Where(c => c.Task_Id == task_id)
                        .CountAsync();

                    var comments = await _context.Comments
                        .Include(c => c.Member)
                            .ThenInclude(m => m.User)
                        .Where(c => c.Task_Id == task_id)
                        .OrderByDescending(c => c.Date_time)
                        .Skip((page - 1) * limit)
                        .Take(limit)
                        .ToListAsync();

                    return Ok(new { success = true, comments, totalComments});
                    
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "An internal error occurred. Please try again later.",
                        error = ex.Message
                    });
                }
        }

        [Authorize]
        [HttpPost("attachment")]
        public async Task<IActionResult> CreateCommentAttachment([FromForm] CommentAttachmentDto commentAttachment){
            try
                {
                    var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                        return Unauthorized(new { success = false, message = "Invalid user token" });

                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                        return NotFound(new { success = false, message = "User not found" });
                    
                    var comment = await _context.Comments.FindAsync(commentAttachment.Comment_Id);

                    if(comment == null) return NotFound(new { success = false, message = "Comment not found"});
                    
                    var task = await _context.Tasks.FindAsync(comment.Task_Id);

                    if(task == null) return NotFound(new { success = false, message = "Task not found"});
                    
                    var isAuthorize = await _context.Members.AnyAsync(m => m.Project_Id == task.Project_Id 
                    && m.User_Id == userId && m.Status == "Active"); 

                    if(!isAuthorize) return Unauthorized(new { success = false, message = "Unauthorized you must be a member of the project"});

                    if (commentAttachment.File == null || commentAttachment.File.Length == 0)
                    return BadRequest("No file uploaded.");

                    var memoryStream = new MemoryStream();
                    await commentAttachment.File.CopyToAsync(memoryStream);
                    var fileBytes = memoryStream.ToArray();
                    
                    var newCommentAttachment = new CommentAttachment{
                        Name = commentAttachment.File.FileName,
                        Comment_Id = commentAttachment.Comment_Id,
                        Content = fileBytes,
                        Type = commentAttachment.File.ContentType,
                    };

                    _context.CommentAttachments.Add(newCommentAttachment);

                    await _context.SaveChangesAsync();
                    
                    return Ok(new { success = true, commentAttachment = newCommentAttachment});
                    
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "An internal error occurred. Please try again later.",
                        error = ex.Message,
                    });
                }
        }

        [Authorize]
        [HttpGet("attachment/{comment_id}")]
        public async Task<IActionResult> GetCommentAttachments(int comment_id){
            try
                {
                    var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                        return Unauthorized(new { success = false, message = "Invalid user token" });

                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                        return NotFound(new { success = false, message = "User not found" });
                    
                    var comment = await _context.Comments
                        .Include(c => c.Task)
                        .FirstOrDefaultAsync(c => c.Id == comment_id);

                    if(comment == null) return NotFound(new { success = false, message = "Comment not found"});
                
                    var attachments = await _context.CommentAttachments
                        .Where(a => a.Comment_Id == comment_id)
                        .ToListAsync();
                    
                    return Ok(new { success = true, attachments});
                    
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "An internal error occurred. Please try again later.",
                        error = ex.Message,
                    });
                }

        }

    }

    
}
