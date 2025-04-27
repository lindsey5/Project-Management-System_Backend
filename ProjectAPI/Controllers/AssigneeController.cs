using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
        private readonly IHubContext<NotificationHub> _hubContext;
        public AssigneeController(ApplicationDBContext context, IHubContext<NotificationHub> hubContext){
            _context = context;
            _hubContext = hubContext;
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
                    var newAssignee = await _context.Members
                        .Include(m => m.User)
                        .FirstOrDefaultAsync(m => m.Id == assigneeToAdd.Member_Id);

                    _context.Task_Histories.Add(new Task_History{
                        Task_Id = assigneeToAdd.Task_Id,
                        New_Value = $"{newAssignee?.User?.Firstname} {newAssignee?.User?.Lastname}",
                        Action_Description = $"{user.Firstname} added an assignee",
                        Date_Time = DateTime.Now
                    });

                    _context.Notifications.Add(new Notification{
                        Message = "",
                        User_id = newAssignee.User.Id,
                        Task_id = task.Id,
                        Project_id = task.Project_Id,
                        Notification_type = "TaskAssigned"
                    });
                    var count = await _context.Notifications.CountAsync(n => n.User_id == newAssignee.User.Id);
                    
                    await _hubContext.Clients.User(newAssignee.User.Email).SendAsync("ReceiveTaskNotification", count);

                }

                foreach(var assigneeToRemove in assigneesUpdate.AssigneesToRemove){
                    var assignee = await _context.Assignees
                        .Include(a => a.Member)
                        .ThenInclude(m => m.User)
                        .FirstOrDefaultAsync(a => a.Id == assigneeToRemove.Id); 
                    if(assignee != null) {
                        _context.Assignees.Remove(assignee);
                        _context.Task_Histories.Add(new Task_History{
                            Task_Id = assignee.Task_Id,
                            Prev_Value = $"{assignee?.Member?.User?.Firstname} {assignee?.Member?.User?.Lastname}",
                            New_Value = "Deleted",
                            Action_Description = $"{user.Firstname} removed an assignee",
                            Date_Time = DateTime.Now
                        });
                    }
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
