using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Task_HistoryController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        public Task_HistoryController(ApplicationDBContext context){
            _context = context;
        }

        [Authorize]
        [HttpGet("{task_id}")]
        public async Task<IActionResult> GetTaskHistory(int task_id, [FromQuery] int page = 1){
            try
            {
                int limit = 5;
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });
                
                var task = await _context.Tasks.FindAsync(task_id);

                if(task == null) return NotFound(new { success = false, message = "Task doesn't exist"});
                
                if(!await _context.Members.AnyAsync(m => m.Project_Id == task.Project_Id && m.User_Id == user.Id))
                    return Unauthorized(new { success = false, message = "Unauthorized. Only member can access."});

                var history = await _context.Task_Histories
                        .Where(t => t.Task_Id == task_id)
                        .OrderByDescending(t => t.Date_Time)
                        .Skip((page - 1) * limit)
                        .Take(limit)
                        .ToListAsync();
                
                return Ok(new { success = true, history});
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

    }
}
