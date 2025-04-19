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
    public class AssigneeController : ControllerBase
    {

        private readonly ApplicationDBContext _context;
        public AssigneeController(ApplicationDBContext context){
            _context = context;
        }

        [Authorize]
        [HttpPut("{task_id}")]
        public async Task<IActionResult> UpdateAssignees(int task_id, AssigneesUpdateDto assigneesUpdate) {
            try{
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "Unauthorized: Invalid user token" });
                
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return Unauthorized(new { success = false, message = "Unauthorized: User account does not exist." });

                var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == task_id);

                if(task == null) return NotFound(new { success = false, message = "Task does not exist"});
                
                var isAdmin = await _context.Members.AnyAsync(m => m.User_Id == userId && m.Project_Id == task.Project_Id);

                if(!isAdmin) return Unauthorized(new { success = false, message = "Unauthorized: Access is restricted to administrators only. " });

                foreach(var assigneeToAdd in assigneesUpdate.AssigneesToAdd){
                    _context.Assignees.Add(assigneeToAdd);
                }

                foreach(var assigneeToRemove in assigneesUpdate.AssigneesToRemove){
                    var assignee = await _context.Assignees.FirstOrDefaultAsync(a => a.Id == assigneeToRemove.Id); 
                    if(assignee != null) _context.Assignees.Remove(assignee);
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Assignees successfully updated" });


            }catch(Exception ex){
                return StatusCode(500, new { 
                    success = false, 
                    message = "An internal error occurred",
                    ex
                });
            }

        }

    }
    
}
