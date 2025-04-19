using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public TaskController(ApplicationDBContext context, AssigneeService assigneeService)
        {
            _context = context;
            _assigneeService = assigneeService;
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask([FromBody] Task UpdatedTask, int id)
        {
            try{
                var task = await _context.Tasks.FindAsync(id);

                if (task == null) return NotFound(new { success = false, message = "Task not found"});
                
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);

                if(user == null) return NotFound(new { success = false, message = "User not found."}); 

                var isAuthorize = await _context.Members.FirstOrDefaultAsync(m => m.User_Id == Convert.ToInt32(idClaim.Value) && m.Project_Id == task.Project_Id);
                
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
                        Task_Id = task.Id
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

            var isAuthorize = await _context.Members.FirstOrDefaultAsync(m => m.User_Id == Convert.ToInt32(idClaim.Value) && m.Project_Id == project_id);
            
            if(isAuthorize == null) return Unauthorized(new { success = false, message = "Access is restricted to members only." });

            var tasks = await _context.Tasks
                .Where(t => t.Project_Id == project_id)
                .Include(t => t.Member)
                    .ThenInclude(m => m.User)
                .Include(t => t.Assignees)
                    .ThenInclude(a => a.Member)
                        .ThenInclude(m => m.User)
                .ToListAsync();

            return Ok(new { success = true, tasks});
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTaskById(int id)
        {
            // Fetch the project by its ID, including the related user (if needed)
            var task = await _context.Tasks.FirstOrDefaultAsync(p => p.Id == id);

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
            
            var member = await _context.Members.FirstOrDefaultAsync(m => m.Project_Id == taskCreateDto.Project_Id && m.User_Id == userId && m.Role == "Admin");

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
                return Ok(new {success = "true", task});
                
            }catch (DbUpdateException ex) when (ex.InnerException is MySqlException mySqlEx && mySqlEx.Number == 1452){
                
                return BadRequest(ex);
            }
            catch (Exception ex){
                
                return StatusCode(500, new { Error = ex.Message });
            }

        }
    }
}
