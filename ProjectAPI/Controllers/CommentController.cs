using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.DTOs;
using ProjectAPI.Models;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentController : ControllerBase
    {
        private readonly ApplicationDBContext _context;

        public CommentController(ApplicationDBContext context)
        {
            _context = context;
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
                    
                    var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == newComment.Task_Id);

                    if(task == null) return NotFound(new { success = false, message = "Task not found"});
                    
                    var member = await _context.Members.FirstOrDefaultAsync(m => 
                        m.User_Id == userId && m.Project_Id == task.Project_Id
                    );

                    if(member == null) return Unauthorized(new { success = false, message = "Unauthorized you must be a member of the project"});

                    var comment = new Comment{
                        Task_Id = newComment.Task_Id,
                        Member_Id = member.Id,
                        Content = newComment.Content,
                        Date_time = DateTime.Now,
                    };

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
        public async Task<IActionResult> GetComments(int task_id){
            try
                {
                    var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                        return Unauthorized(new { success = false, message = "Invalid user token" });

                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                        return NotFound(new { success = false, message = "User not found" });
                    
                    var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == task_id);

                    if(task == null) return NotFound(new { success = false, message = "Task not found"});
                    
                    var member = await _context.Members.FirstOrDefaultAsync(m => 
                        m.User_Id == userId && m.Project_Id == task.Project_Id
                    );

                    if(member == null) return Unauthorized(new { success = false, message = "Unauthorized you must be a member of the project"});

                    var comments = await _context.Comments
                        .Include(c => c.Member)
                            .ThenInclude(m => m.User)
                        .Where(c => c.Task_Id == task_id)
                        .ToListAsync();
                    return Ok(new { success = true, comments});
                    
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
                    
                    var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == comment.Task_Id);

                    if(task == null) return NotFound(new { success = false, message = "Task not found"});
                    
                    var isAuthorize = await _context.Members.AnyAsync(m => m.Project_Id == task.Project_Id 
                    && m.User_Id == userId); 

                    if(!isAuthorize) return Unauthorized(new { success = false, message = "Unauthorized you must be a member of the project"});

                    if (commentAttachment.File == null || commentAttachment.File.Length == 0)
                    return BadRequest("No file uploaded.");

                    var memoryStream = new MemoryStream();
                    await commentAttachment.File.CopyToAsync(memoryStream);
                    var fileBytes = memoryStream.ToArray();
                    
                    var newCommentAttachment = new CommentAttachment{
                        Comment_Id = commentAttachment.Comment_Id,
                        Content = fileBytes
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
                        file = commentAttachment.File
                    });
                }

        }

    }

    
}
