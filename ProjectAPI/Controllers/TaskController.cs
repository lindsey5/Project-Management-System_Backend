using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ProjectAPI.Models;
using ProjectAPI.Services;
using Task = ProjectAPI.Models.Task;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        private readonly AssigneeService _assigneeService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserConnectionService _userConnectionService;
        
        public TaskController(
            ApplicationDBContext context, 
            AssigneeService assigneeService,
            IHubContext<NotificationHub> hubContext,
            UserConnectionService userConnectionService
        )
        {
            _context = context;
            _assigneeService = assigneeService;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask([FromBody] Task UpdatedTask, int id)
        {
            try{
                var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.Assignees)
                    .ThenInclude(a => a.Member)
                        .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(t => t.Id == id);

                if (task == null) return NotFound(new { success = false, message = "Task not found"});
                
                if(task.Status == "Deleted") return BadRequest(new { success = false, message = "Can't update, task is already deleted"});

                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);

                if(user == null) return NotFound(new { success = false, message = "User not found."}); 

                var isAuthorize = await _context.Members.FirstOrDefaultAsync(m => 
                    m.User_Id == Convert.ToInt32(idClaim.Value) && 
                    m.Project_Id == task.Project_Id && 
                    m.Status == "Active"
                );
                
                if(isAuthorize == null) return Unauthorized(new { success = false, message = "Access is restricted to members only." });

                var changes = new List<Task_History>();

                void AddHistory(string description, string prev, string next)
                {
                    changes.Add(new Task_History
                    {
                        Action_Description = $"{user.Firstname} {description}",
                        Prev_Value = prev,
                        New_Value = next,
                        Date_Time = DateTime.Now,
                        Task_Id = task.Id,
                        Project_Id = task.Project_Id,
                    });
                }

                if (task.Status != UpdatedTask.Status)
                    AddHistory("changed the status", task.Status, UpdatedTask.Status);

                if (task.Task_Name != UpdatedTask.Task_Name)
                    AddHistory("changed the task name", task.Task_Name, UpdatedTask.Task_Name);

                if (task.Description != UpdatedTask.Description)
                    AddHistory("changed the description", task.Description, UpdatedTask.Description);

                if (task.Priority != UpdatedTask.Priority)
                    AddHistory("changed the priority", task.Priority, UpdatedTask.Priority);

                if (task.Due_date != UpdatedTask.Due_date)
                    AddHistory("changed the due date", task.Due_date.ToString(), UpdatedTask.Due_date.ToString());

                _context.Task_Histories.AddRange(changes);
                task.Status = UpdatedTask.Status;
                task.Task_Name = UpdatedTask.Task_Name;
                task.Description = UpdatedTask.Description;
                task.Priority = UpdatedTask.Priority;
                task.Due_date = UpdatedTask.Due_date;
                task.Updated_At = DateTime.Now;

                if(changes.Count > 0){
                    foreach(var assignee in task.Assignees){
                        if(assignee.Member == null || task?.Project == null || assignee.Member.User == null || assignee.Member.User.Id == userId) continue;
                        
                        var builder = new StringBuilder();
                        builder.AppendLine($"Task \"{task.Task_Name}\" in project \"{task?.Project.Title}\" has been {(UpdatedTask.Status != "Deleted" ? "updated" : "deleted")}");

                        if(UpdatedTask.Status != "Deleted"){
                            foreach(var change in changes){
                            builder.AppendLine($"{change.Action_Description} from \"{change.Prev_Value}\" to \"{change.New_Value}\"");
                        }
                        }

                        string message = builder.ToString();
                        var newNotification = new Notification
                        {
                            Message = message,
                            User_id = assignee.Member.User.Id,
                            Task_id = task?.Id,
                            Project_id = task?.Project_Id,
                            Type = UpdatedTask.Status != "Deleted" ? "TaskUpdated" : "TaskDeleted",
                            Created_by = userId,
                            IsRead = false,
                            Date_time = DateTime.Now,
                            User = user
                        };

                        _context.Notifications.Add(newNotification);
                        Console.WriteLine(_userConnectionService.GetConnections());

                        if(_userConnectionService.GetConnections().TryGetValue(assignee.Member.User.Email, out var connectionId)){
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, updatedTask = task});

            }catch(Exception ex){
                return StatusCode(500, new { Error = ex.Message });
            }
        }


        [Authorize]
        [HttpGet()]
        public async Task<IActionResult> GetTasks([FromQuery] int project_id)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            var isAuthorize = await _context.Members.FirstOrDefaultAsync(m => 
                m.User_Id == Convert.ToInt32(idClaim.Value) && 
                m.Project_Id == project_id && 
                m.Status == "Active"
            );
            
            if(isAuthorize == null) return Unauthorized(new { success = false, message = "Access is restricted to members only." });

            var tasks = await _context.Tasks
                .Where(t => t.Project_Id == project_id && t.Status != "Deleted")
                .OrderBy(t => t.Due_date)
                .Include(t => t.Comments)
                .Include(t => t.Member)
                    .ThenInclude(m => m.User)
                .Include(t => t.Assignees)
                    .ThenInclude(a => a.Member)
                        .ThenInclude(m => m.User)
                .ToListAsync();

            return Ok(new { success = true, tasks, message = "asdsa"});
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTaskById(int id)
        {
            // Fetch the project by its ID, including the related user (if needed)
            var task = await _context.Tasks
            .Include(t => t.Member)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id);

            // If the project is not found, return a NotFound response
            if (task == null)
            {
                return NotFound(new { message = "Project not found." });
            }

            // Return the found project
            return Ok(new { success = true, task });
        }
        
        [Authorize]
        [HttpPost()]
        public async Task<IActionResult> CreateTask([FromBody] TaskCreateDto taskCreateDto) {
            if (taskCreateDto == null) return BadRequest("Task data is required");

            if (!ModelState.IsValid) return BadRequest(ModelState);

            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId)) return Unauthorized(new { success = false, message = "ID not found in token." });

            var user = await _context.Users.FindAsync(userId);

            if(user == null) return NotFound(new { success = false, message = "User not found."}); 

            var project = await _context.Projects.FindAsync(taskCreateDto.Project_Id);

            if(project == null) return NotFound( new { success = false, message = "Project not found"});
            
            var member = await _context.Members.FirstOrDefaultAsync(m => 
                m.Project_Id == taskCreateDto.Project_Id && m
                .User_Id == userId && m.Role == "Admin" && m.Status == "Active"
            );

            if(member == null ) return Unauthorized("Access is restricted to administrators only."); 

            try {
                var task = new Task{
                    Task_Name = taskCreateDto.Task_Name,
                    Description = taskCreateDto.Description,
                    Due_date = taskCreateDto.Due_date,
                    Priority = taskCreateDto.Priority,
                    Status = taskCreateDto.Status ?? "To Do",
                    Project_Id = taskCreateDto.Project_Id,
                    Created_At = DateTime.Now,
                    Updated_At = DateTime.Now,
                    Creator = member.Id
                };

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();

                var Assignees = await _assigneeService.CreateAssignees(_context, taskCreateDto.AssigneesMemberId, task.Id, taskCreateDto.Project_Id);
                await _context.SaveChangesAsync();

                foreach (var assignee in Assignees)
                {
                    ;
                    var newAssignee = await _context.Members
                        .Include(m => m.User)
                        .FirstOrDefaultAsync(m => m.Id == assignee.Member_Id);

                    if (newAssignee?.User == null)
                        continue;
                    _context.Task_Histories.Add(new Task_History
                    {
                        Task_Id = assignee.Task_Id,
                        Project_Id = task.Project_Id,
                        New_Value = $"{newAssignee.User.Firstname} {newAssignee.User.Lastname}",
                        Action_Description = $"{user.Firstname} added an assignee",
                        Date_Time = DateTime.Now
                    });

                    // Notify only if the assignee is not the one making the assignment
                    if (newAssignee.User.Id != userId)
                    {
                        var notification = new Notification
                        {
                            Message = $"You have been assigned to the task \"{task.Task_Name}\" in project \"{project.Title}\"",
                            User_id = newAssignee.User.Id,
                            Task_id = task.Id,
                            Project_id = task.Project_Id,
                            Type = "TaskAssigned",
                            Created_by = userId,
                            IsRead = false,
                            Date_time = DateTime.Now,
                            User = user // assumes user is already tracked or from context
                        };

                        _context.Notifications.Add(notification);

                        if (_userConnectionService.GetConnections().TryGetValue(newAssignee.User.Email, out var connectionId))
                        {
                            await _hubContext.Clients.Client(connectionId)
                                .SendAsync("ReceiveTaskNotification", 1, notification);
                        }
                    }
                }


                await _context.SaveChangesAsync();
                return Ok(new {success = "true", task});
                
            }catch (DbUpdateException ex) when (ex.InnerException is MySqlException mySqlEx && mySqlEx.Number == 1452){
                
                return BadRequest(ex);
            }
            catch (Exception ex){
                
                return StatusCode(500, new { Error = ex.Message });
            }

        }
    
    
        [Authorize]
        [HttpGet("user")]
        public async Task<IActionResult> GetUserTasks(){
            try
            {
                 var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });
                
                var memberIds = await _context.Members
                    .Where(m => m.User_Id == userId)
                    .Select(m => m.Id )
                    .Distinct()
                    .ToListAsync();
                
                var taskIds = await _context.Assignees
                    .Where(a => memberIds.Contains(a.Member_Id))
                    .Select(a => a.Task_Id)
                    .Distinct()
                    .ToListAsync();
                
                var tasks = await _context.Tasks
                    .Where(t => taskIds.Contains(t.Id) && t.Status != "Deleted")
                    .Include(t => t.Project)
                    .Include(t => t.Member)
                        .ThenInclude(m => m.User)
                    .ToListAsync();

                return Ok(new { success = true, tasks});

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
