using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ProjectAPI.Models;
using ProjectAPI.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.AspNetCore.SignalR;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        private readonly ProjectService _projectService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserConnectionService _userConnectionService;

         public ProjectController(
            ApplicationDBContext context, 
            ProjectService projectService,
            IHubContext<NotificationHub> hubContext,
            UserConnectionService userConnectionService
         )
        {
            _context = context;
            _projectService = projectService;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
        }

        [Authorize]
        [HttpGet("authorize")]
        public async Task<IActionResult> GetAuthorization([FromQuery] string Project_code)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null) return Unauthorized(new { success = false, message = "ID not found in token." });

            var project = await _context.Projects
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Project_code == Project_code);

            // If the project is not found, return a NotFound response
            if (project == null) return NotFound(new { message = "Project not found." });

            var ProjectMember = await _context.Members
                .FirstOrDefaultAsync(m => 
                    m.User_Id == Convert.ToInt32(idClaim.Value) && 
                    m.Project_Id == project.Id &&
                    m.Status == "Active"
                    );
            
            if(ProjectMember == null) return Unauthorized(new { success = false, message = "User not found in the project"});

            // Return the found project 
            return Ok(new { success = true, project, role = ProjectMember.Role });
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProjectById(int id)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            var user = await _context.Users.FindAsync(userId);

            if(user == null) return Unauthorized(new { success = false, message = "User not found"});

            var isMember = await _context.Members.AnyAsync(m => 
                m.Project_Id == id && 
                m.User_Id == userId &&
                m.Status == "Active"
            );

            if(!isMember) return Unauthorized(new { success = false, message = "Unauthorized: Access is restricted to project members only."});
        
            var project = await _context.Projects.FindAsync(id);

            return Ok(new { success = true , project});
        }

        [Authorize]
        [HttpGet("/user")]
        public async Task<IActionResult> GetUserProjects()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            var projects = await _context.Projects
                .Where(p => p.User_id == userId)
                .ToListAsync();

            return Ok(new { success = true, projects });
        }

        [Authorize]
        [HttpGet()]
        public async Task<IActionResult> GetProjects()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            var projectIds = await _context.Members
                .Where(m => m.User_Id == userId && m.Status == "Active")
                .Select(m => m.Project_Id)
                .Distinct()
                .ToListAsync();

            var memberProjects = await _context.Projects
                .Where(p => projectIds.Contains(p.Id))
                .Include(p => p.User)
                .ToListAsync();

            return Ok(new { 
                success = true, 
                projects = memberProjects
            });
        }

        [Authorize]
        [HttpPut()]
        public async Task<IActionResult> UpdateProject([FromBody] Project updatedProject)
        {
            try{
                if (updatedProject == null) return BadRequest(new { message = "Project data is missing." });
                // Get the user ID from the claims
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId)) 
                    return Unauthorized(new { message = "ID not found in token." });
                
                var user = await _context.Users.FindAsync(userId);

                if(user == null) return NotFound(new { success = false, message = "User not found"});

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == updatedProject.Id);
                
                if(project == null) return NotFound(new { success = false, message = "Project not found."} );

                var isAdmin = await _context.Members.AnyAsync(m => m.Project_Id == project.Id && m.User_Id == userId && m.Role == "Admin");

                if(!isAdmin) return Unauthorized(new { success = false, message = "Only admin is authorized"});

                var members = await _context.Members
                    .Include(m => m.User)
                    .Where(m => m.Project_Id == project.Id && m.User_Id != userId)
                    .ToListAsync();

                var changes = new List<Task_History>();

                void AddHistory(string description, string? prev, string next)
                {
                    changes.Add(new Task_History
                    {
                        Action_Description = $"{user.Firstname} {description} to {next}",
                        Prev_Value = prev,
                        New_Value = next,
                        Date_Time = DateTime.Now,
                        Task_Id = null,
                        Project_Id = project.Id,
                    });
                }

                if (project.Title != updatedProject.Title)
                    AddHistory("changed the project title", project.Title, updatedProject.Title);

                if (project.Description != updatedProject.Description)
                    AddHistory("changed the project description", project.Description, updatedProject.Description);

                if (project.Type != updatedProject.Type)
                    AddHistory("changed the type", project.Type, updatedProject.Type);

                if (project.Start_date != updatedProject.Start_date)
                    AddHistory("changed the project start date", 
                            project.Start_date.ToString("yyyy-MM-dd"), 
                            updatedProject.Start_date.ToString("yyyy-MM-dd"));

                if (project.End_date != updatedProject.End_date)
                    AddHistory("changed the project end date", 
                            project.End_date.ToString("yyyy-MM-dd"), 
                            updatedProject.End_date.ToString("yyyy-MM-dd"));

                if (project.Status != updatedProject.Status)
                    AddHistory("changed the project status", project.Status, updatedProject.Status);

                _context.Task_Histories.AddRange(changes);

                if(changes.Count > 0){
                    foreach(var member in members){
                        if(member == null || member.User == null) continue;
                        
                        var builder = new StringBuilder();
                        builder.AppendLine($"Project \"{project.Title}\" has been updated: ");

                        foreach(var change in changes){
                            builder.AppendLine($"{change.Action_Description} from \"{change.Prev_Value}\" to \"{change.New_Value}\"");
                        }

                        string message = builder.ToString();
                        var newNotification = new Notification
                        {
                            Message = message,
                            User_id = member.User.Id,
                            Task_id = null,
                            Project_id = project.Id,
                            Type = "ProjectUpdated",
                            Created_by = userId,
                            IsRead = false,
                            Date_time = DateTime.Now,
                            User = user
                        };

                        _context.Notifications.Add(newNotification);

                        if(_userConnectionService.GetConnections().TryGetValue(member.User.Email, out var connectionId)){
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                        }
                    }
                }

                project.Title = updatedProject.Title;
                project.Description = updatedProject.Description;
                project.Type = updatedProject.Type;
                project.Start_date = updatedProject.Start_date;
                project.End_date = updatedProject.End_date;
                project.Status = updatedProject.Status;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, updatedProject, changes});

            }catch(Exception ex){
                return StatusCode(500, new { 
                    success = false, 
                    message = "An error occurred while processing your request",
                    error = ex.Message 
                });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] Project project)
        {
            try{
                // Validate the incoming model
                if (project == null) return BadRequest(new { message = "Project data is missing." });

                // Get the user ID from the claims
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId)) 
                    return Unauthorized(new { message = "ID not found in token." });

                // Generate unique project code
                string code = _projectService.GenerateUniqueProjectCode();

                // Create a new project object
                var newProject = new Project
                {
                    Title = project.Title,
                    Description = project.Description,
                    Start_date = project.Start_date,
                    End_date = project.End_date,
                    Created_At = DateTime.Now,
                    Type = project.Type,
                    Status = "Active",
                    User_id = userId,
                    Project_code = code
                };

                // Add the new project to the context and save changes asynchronously
                _context.Projects.Add(newProject);
                await _context.SaveChangesAsync();

                _context.Members.Add(new Member{
                    Project_Id = newProject.Id,
                    User_Id = userId,
                    Role = "Admin",
                    Joined_At = DateTime.Now,
                    Status = "Active"
                });
                await _context.SaveChangesAsync();

                return Ok(new { success = true, data = newProject });

            }catch(Exception ex){
                return StatusCode(500, new { 
                    success = false, 
                    message = "An error occurred while processing your request",
                    error = ex.Message 
                });
            }
        }
    }
}
