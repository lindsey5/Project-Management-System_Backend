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
    public class AssigneeController : ControllerBase
    {

        private readonly ApplicationDBContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserConnectionService _userConnectionService;

        public AssigneeController(
            ApplicationDBContext context, 
            IHubContext<NotificationHub> hubContext,
            UserConnectionService userConnectionService)
        {
            _context = context;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
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

                var task = await _context.Tasks
                    .Include(t => t.Project)
                    .FirstOrDefaultAsync(t => t.Id == task_id);

                if(task == null) return NotFound(new { success = false, message = "Task does not exist"});
                
                var isAdmin = await _context.Members.AnyAsync(m => m.User_Id == userId && m.Project_Id == task.Project_Id && m.Status == "Active");

                if(!isAdmin) return Unauthorized(new { success = false, message = "Unauthorized: Access is restricted to administrators only. " });
                
                var assigneesToRemove = new List<Assignee>();

                foreach (var assigneeToAdd in assigneesUpdate.AssigneesToAdd)
                {
                    var newAssignee = await _context.Members
                        .Include(m => m.User)
                        .FirstOrDefaultAsync(m => m.Id == assigneeToAdd.Member_Id);

                    if (newAssignee == null || newAssignee.User == null || task.Project == null)
                    {
                        continue;
                    }

                    _context.Task_Histories.Add(new Task_History
                    {
                        Task_Id = assigneeToAdd.Task_Id,
                        Project_Id = task.Project_Id,
                        New_Value = $"{newAssignee.User.Firstname} {newAssignee.User.Lastname}",
                        Action_Description = $"{user.Firstname} added an assignee",
                        Date_Time = DateTime.Now
                    });

                    if(newAssignee.User.Id != userId){
                        var newNotification = new Notification
                        {
                            Message = $"You have been assigned to the task \"{task.Task_Name}\" in project \"{task.Project.Title}\"",
                            User_id = newAssignee.User.Id,
                            Task_id = task.Id,
                            Project_id = task.Project_Id,
                            Type = "TaskAssigned",
                            Created_by = userId,
                            IsRead = false,
                            Date_time = DateTime.Now,
                            User = user
                        };
                        
                        _context.Notifications.Add(newNotification);

                        if(_userConnectionService.GetConnections().TryGetValue(newAssignee.User.Email, out var connectionId)){
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                        }
                    }
                }

                foreach (var assigneeToRemove in assigneesUpdate.AssigneesToRemove)
                {
                    var assignee = await _context.Assignees
                        .Include(a => a.Member)
                        .ThenInclude(m => m.User)
                        .FirstOrDefaultAsync(a => a.Id == assigneeToRemove.Id); 

                    if (assignee == null || assignee.Member == null || assignee.Member.User == null || task.Project == null)
                        continue;

                    _context.Task_Histories.Add(new Task_History
                    {
                        Task_Id = assignee.Task_Id,
                        Project_Id = task.Project_Id,
                        Prev_Value = $"{assignee.Member.User.Firstname} {assignee.Member.User.Lastname}",
                        New_Value = "Deleted",
                        Action_Description = $"{user.Firstname} removed an assignee",
                        Date_Time = DateTime.Now,
                    });

                    if(assignee.Member.User.Id != userId){
                        var newNotification = new Notification
                        {
                            Message = $"You have been removed from the task \"{task.Task_Name}\" in project \"{task.Project.Title}\"",
                            User_id = assignee.Member.User.Id,
                            Task_id = task.Id,
                            Project_id = task.Project_Id,
                            Created_by = userId,
                            Type = "TaskRemoved",
                            IsRead = false,
                            Date_time = DateTime.Now,
                            User = user
                        };

                        _context.Notifications.Add(newNotification);

                        assigneesToRemove.Add(assignee);

                        if(_userConnectionService.GetConnections().TryGetValue(assignee.Member.User.Email, out var connectionId)){
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                        }
                    }
                        
                
                }
                _context.Assignees.AddRange(assigneesUpdate.AssigneesToAdd);
                _context.Assignees.RemoveRange(assigneesToRemove);      
                await _context.SaveChangesAsync();

                return Ok(new { success = true,  message = "Assignees successfully updated" });


            }catch(Exception ex){
                return StatusCode(500, new { 
                    success = false, 
                    message = $"An internal error occurred: {ex.Message}",
                    details = ex.StackTrace
                });
            }

        }

    }
    
}
