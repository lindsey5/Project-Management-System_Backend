using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ProjectAPI.Models;
using ProjectAPI.Services;
using Microsoft.EntityFrameworkCore;
using Task = ProjectAPI.Models.Task;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        private readonly ProjectService _projectService;
         public ProjectController(ApplicationDBContext context, ProjectService projectService)
        {
            _context = context;
            _projectService = projectService;
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

            var ProjectMember = await _context.Members.FirstOrDefaultAsync(m => m.User_Id == Convert.ToInt32(idClaim.Value) && m.Project_Id == project.Id);
            
            if(ProjectMember == null) return Unauthorized(new { success = false, message = "User not found in the project"});

            // Return the found project 
            return Ok(new { success = true, project, role = ProjectMember.Role });
        }

        [Authorize]
        [HttpGet()]
        public async Task<IActionResult> GetProjects()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            var memberProjectIds = await _context.Members
                .Where(m => m.User_Id == userId)
                .Select(m => m.Project_Id)
                .Distinct()
                .ToListAsync();

            var memberProjects = await _context.Projects
                .Where(p => memberProjectIds.Contains(p.Id))
                .Include(p => p.User)
                .ToListAsync();

            return Ok(new { 
                success = true, 
                projects = memberProjects
            });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] Project project)
        {
            try{
                // Validate the incoming model
                if (project == null)
                {
                    return BadRequest(new { message = "Project data is missing." });
                }

                // Get the user ID from the claims
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (idClaim == null) 
                    return Unauthorized(new { message = "ID not found in token." });

                // Fetch the user asynchronously
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Convert.ToInt32(idClaim.Value));

                if (user == null) 
                    return Unauthorized(new { message = "User not found." });

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
                    User_id = user.Id,
                    Project_code = code
                };

                // Add the new project to the context and save changes asynchronously
                _context.Projects.Add(newProject);
                await _context.SaveChangesAsync();

                _context.Members.Add(new Member{
                    Project_Id = newProject.Id,
                    User_Id = user.Id,
                    Role = "Admin",
                    Joined_At = DateTime.Now
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
