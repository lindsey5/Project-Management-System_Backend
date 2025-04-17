using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
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

                var isAuthorize = await _context.Members.FirstOrDefaultAsync(m => m.User_Id == Convert.ToInt32(idClaim.Value) && m.Project_Id == task.Project_Id);
                
                if(isAuthorize == null) return Unauthorized(new { success = false, message = "You're not part of this project", task.Project_Id });

                task.Status = UpdatedTask.Status;
                task.Task_Name = UpdatedTask.Task_Name;
                task.Description = UpdatedTask.Description;
                task.Priority = UpdatedTask.Priority;
                task.Due_date = UpdatedTask.Due_date;

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
            
            if(isAuthorize == null) return Unauthorized(new { success = false, message = "You're not part of this project", project_id });

            var tasks = await _context.Tasks
                .Where(t => t.Project_Id == project_id)
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

            if (idClaim == null) return Unauthorized(new { success = false, message = "ID not found in token." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Convert.ToInt32(idClaim.Value));

            if (user == null) return Unauthorized(new { message = "User not found." }); 

            if(taskCreateDto.AssigneesMemberId.Count() == 0) return BadRequest("AssigneesMemberId is required");

            if(await _context.Projects.FirstOrDefaultAsync(p => p.User_id == user.Id) == null) return BadRequest("Creating task failed."); 

            try {
                var task = new Task{
                    Task_Name = taskCreateDto.Task_Name,
                    Description = taskCreateDto.Description,
                    Due_date = taskCreateDto.Due_date,
                    Priority = taskCreateDto.Priority,
                    Status = taskCreateDto.Status ?? "To Do",
                    Project_Id = taskCreateDto.Project_Id,
                };

                _context.Tasks.Add(task);

                await _context.SaveChangesAsync();

                var Assignees = await _assigneeService.CreateAssignees(_context, taskCreateDto.AssigneesMemberId, task.Id, taskCreateDto.Project_Id);

                if(Assignees != null && Assignees.Count > 0){
                    return Ok(new {success = "true", task});
                }
                return BadRequest(new {message = "Error creating a task", taskCreateDto});
            }catch (DbUpdateException ex) when (ex.InnerException is MySqlException mySqlEx && mySqlEx.Number == 1452){
                
                return BadRequest(ex);
            }
            catch (Exception ex){
                
                return StatusCode(500, new { Error = ex.Message });
            }

        }
    }
}
