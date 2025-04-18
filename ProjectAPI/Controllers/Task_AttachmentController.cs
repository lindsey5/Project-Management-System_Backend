using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;
using ProjectAPI.Models.Task_Attachment;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Task_AttachmentController : ControllerBase
    {
        private readonly ApplicationDBContext _context;

        public Task_AttachmentController(ApplicationDBContext context){
            _context = context;
        }

        [Authorize]
        [HttpPost()]
        public async Task<IActionResult> CreateAttachment([FromForm] TaskAttachmentDto task_Attachment) {

            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            var task = await _context.Tasks.FindAsync(task_Attachment.Task_Id);

            if(task == null) return NotFound(new { success = false, message = "Task not found"});

            var isAuthorize = await _context.Members.AnyAsync(m => m.Project_Id == task.Project_Id 
                && m.User_Id == userId && m.Role == "Admin"); 

            if(!isAuthorize) return Unauthorized(new { success = false, message = "Only admin is authorized."});
            

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
            await _context.SaveChangesAsync();

            return Ok(new { success = true, attachment});
        }


        [Authorize]
        [HttpGet("{task_Id}")]
        public async Task<IActionResult> GetAttachments(int task_Id)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            var task = await _context.Tasks.FindAsync(task_Id);

            if(task == null) return NotFound(new { success = false, message = "Task not found"});

            var member = await _context.Members.AnyAsync(m => m.Project_Id == task.Project_Id && m.User_Id == userId);

            if(!member) return Unauthorized(new { success= false, message = "Unauthorized."});

            var attachments = await _context.Task_Attachments
                    .Where(a => a.Task_Id == task_Id)
                    .ToListAsync();

            // Return the file with the appropriate MIME type
            return Ok(new { success = true, attachments});
        }

        [Authorize]
        [HttpGet("{task_Id}/{id}")]
        public async Task<IActionResult> GetAttachment(int id, int task_Id)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            var task = await _context.Tasks.FindAsync(task_Id);

            if(task == null) return NotFound(new { success = false, message = "Task not found"});

            var member = await _context.Members.AnyAsync(m => m.Project_Id == task.Project_Id && m.User_Id == userId);

            if(!member) return Unauthorized(new { success= false, message = "Unauthorized."});

            var attachment = await _context.Task_Attachments.FindAsync(id);

            if (attachment == null)
                return NotFound(new { success = false, message = "Attachment not found" });

            // Return the file with the appropriate MIME type
            return File(attachment.Content, attachment.Type, attachment.Name);
        }
    }
}
