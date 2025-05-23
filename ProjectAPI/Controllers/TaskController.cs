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

                var member = await _context.Members.FirstOrDefaultAsync(m => m.User_Id == userId && m.Project_Id == task.Project_Id); 

                if(member == null) return Unauthorized(new { success = false, message = "User is not part of the project." });

                var isAssignee = await _context.Assignees.AnyAsync(a => a.Task_Id == id && a.Member_Id == member.Id);
                
                if(member.Role != "Admin" && member.Role != "Editor" && !isAssignee) return Unauthorized( new { success = false, message = "Access is only for admins, editors and assignees" });
                
                var changes = new List<Task_History>();

                void AddHistory(string description, string? prev, string next)
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

                if (task.Start_date != UpdatedTask.Start_date)
                    AddHistory("changed the start date", task.Start_date.ToString(), UpdatedTask.Start_date.ToString());

                if (task.Due_date != UpdatedTask.Due_date)
                    AddHistory("changed the due date", task.Due_date.ToString(), UpdatedTask.Due_date.ToString());

                _context.Task_Histories.AddRange(changes);
                
                task.Status = UpdatedTask.Status;
                task.Task_Name = UpdatedTask.Task_Name;
                task.Description = UpdatedTask.Description;
                task.Priority = UpdatedTask.Priority;
                task.Start_date = UpdatedTask.Start_date;
                task.Due_date = UpdatedTask.Due_date;
                task.Updated_At = DateTime.Now;
                

                if(changes.Count > 0){
                    foreach(var assignee in task.Assignees){
                        if(assignee.Member == null || task?.Project == null || assignee.Member.User == null || assignee.Member.User.Id == userId) continue;
                        
                        var builder = new StringBuilder();
                        builder.AppendLine($"Task \"{task.Task_Name}\" in project \"{task?.Project.Title}\" has been updated");

                        foreach(var change in changes){
                            builder.AppendLine($"{change.Action_Description} from \"{change.Prev_Value}\" to \"{change.New_Value}\"");
                        }

                        string message = builder.ToString();
                        var newNotification = new Notification
                        {
                            Message = message,
                            User_id = assignee.Member.User.Id,
                            Task_id = task?.Id,
                            Project_id = task?.Project_Id,
                            Type ="TaskUpdated",
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
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, updatedTask = task});

            }catch(Exception ex){
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
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

                var member = await _context.Members.FirstOrDefaultAsync(m => m.User_Id == userId && m.Project_Id == task.Project_Id); 

                if(member == null) return Unauthorized(new { success = false, message = "User is not part of the project." });

                if(member.Role != "Admin" && member.Role != "Editor") return Unauthorized( new { success = false, message = "Access is only for admins and editors" });
                
                _context.Task_Histories.Add(new Task_History
                {
                    Action_Description = $"Task \"{task.Task_Name}\" has been deleted by {user.Firstname} {user.Lastname}",
                    Prev_Value = null,
                    New_Value = "Deleted",
                    Date_Time = DateTime.Now,
                    Task_Id = task.Id,
                    Project_Id = task.Project_Id,
                });

                 _context.Tasks.Remove(task);

                foreach(var assignee in task.Assignees){
                    if(assignee.Member == null || task?.Project == null || assignee.Member.User == null || assignee.Member.User.Id == userId) continue;
                        var newNotification = new Notification
                        {
                            Message = $"Task \"{task.Task_Name}\" in project \"{task?.Project.Title}\" has been deleted",
                            User_id = assignee.Member.User.Id,
                            Task_id = task?.Id,
                            Project_id = task?.Project_Id,
                            Type = "TaskDeleted",
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
                .OrderBy(t => t.Start_date)
                .Include(t => t.Comments)
                .Include(t => t.Member)
                    .ThenInclude(m => m.User)
                .Include(t => t.Assignees)
                    .ThenInclude(a => a.Member)
                        .ThenInclude(m => m.User)
                .ToListAsync();

            return Ok(new { success = true, tasks });
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTaskById(int id)
        {
            // Fetch the project by its ID, including the related user (if needed)
            var task = await _context.Tasks
            .Include(t => t.Assignees)
                .ThenInclude(a => a.Member)
                    .ThenInclude(a => a.User)
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
                .User_Id == userId && ( m.Role == "Admin" || m.Role == "Editor") && m.Status == "Active" 
            );

            if(member == null ) return Unauthorized(new { success = false, message = "Access is restricted to administrators and editors only."});

            try {
                var task = new Task{
                    Task_Name = taskCreateDto.Task_Name,
                    Description = taskCreateDto.Description,
                    Start_date = taskCreateDto.Start_date,
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

                _context.Task_Histories.Add(new Task_History
                {
                    Action_Description = $"{user.Firstname} {user.Lastname} created a task",
                    Prev_Value = null,
                    New_Value = null,
                    Date_Time = DateTime.Now,
                    Task_Id = task.Id,
                    Project_Id = task.Project_Id,
                });

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
                            User = user
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
        public async Task<IActionResult> GetUserTasks(
            int page = 1,
            int limit = 30,
            string searchTerm = "",
            string status = "All",
            string projectStatus = "Active"
        ){
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

                var totalTasks = await _context.Tasks
                    .Include(t => t.Project)
                    .Where(t => 
                        taskIds.Contains(t.Id) && t.Status != "Deleted" &&
                        (projectStatus != "All" ? t.Project.Status == projectStatus : true) &&
                        (status != "All" ? t.Status == status : true) && 
                        t.Task_Name.ToLower().Contains((searchTerm ?? "").ToLower()) &&
                        t.Project.Title.ToLower().Contains((searchTerm ?? "").ToLower())
                    )
                    .CountAsync();
                
                var tasks = await _context.Tasks
                    .Include(t => t.Project)
                    .Include(t => t.Member)
                        .ThenInclude(m => m.User)
                    .Where(t => 
                        taskIds.Contains(t.Id) && t.Status != "Deleted" &&
                        (projectStatus != "All" && t.Project != null ? t.Project.Status == projectStatus : true) &&
                        (status != "All" ? t.Status == status : true) && 
                        t.Task_Name.ToLower().Contains((searchTerm ?? "").ToLower()) &&
                        t.Project.Title.ToLower().Contains((searchTerm ?? "").ToLower())
                    )
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .ToListAsync();

                return Ok(new { 
                    success = true, 
                    page,
                    limit,
                    totalPages = (int)Math.Ceiling((double)totalTasks / limit),
                    totalTasks,
                    tasks,
                });

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
        [HttpGet("user/all")]
        public async Task<IActionResult> GetAllUserTasks(){
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

                var totalTasks = await _context.Tasks
                    .Include(t => t.Project)
                    .Where(t => 
                        taskIds.Contains(t.Id) && t.Status != "Deleted" 
                    )
                    .CountAsync();
                
                var tasks = await _context.Tasks
                    .Include(t => t.Project)
                    .Include(t => t.Member)
                        .ThenInclude(m => m.User)
                    .Where(t => 
                        taskIds.Contains(t.Id) && t.Status != "Deleted")
                    .ToListAsync();

                return Ok(new { success = true, tasks,});

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
