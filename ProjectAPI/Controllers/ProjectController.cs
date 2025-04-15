using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ProjectAPI.Models;
using ProjectAPI.Services;
using Microsoft.EntityFrameworkCore;

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

            // Fetch the project by its ID, including the related user (if needed)
            var project = await _context.Projects
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Project_code == Project_code);

            // If the project is not found, return a NotFound response
            if (project == null) return NotFound(new { message = "Project not found." });

            if(project.User_id == Convert.ToInt32(idClaim.Value)) return Ok(new { success = true, project});

            var ProjectMember = await _context.Members.FirstOrDefaultAsync(m => m.User_Id == Convert.ToInt32(idClaim.Value) && m.Project_Id == project.Id);
            
            if(ProjectMember == null) return Unauthorized(new { success = false, message = "User not found in the project"});

            // Return the found project 
            return Ok(new { success = true, project });
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProject(int id)
        {
            // Fetch the project by its ID, including the related user (if needed)
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);

            // If the project is not found, return a NotFound response
            if (project == null)
            {
                return NotFound(new { message = "Project not found." });
            }

            // Return the found project
            return Ok(new { success = true, project });
        }

        [Authorize]
        [HttpGet()]
        public async Task<IActionResult> GetProjects()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null) return Unauthorized(new { success = false, message = "ID not found in token." });

            var projects = await _context.Projects
            .Where(p => p.User_id == Convert.ToInt32(idClaim.Value))
            .Include(p => p.User)
            .ToListAsync();

            return Ok(new { success = true, projects });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] Project project)
        {
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
                Category = project.Category,
                Status = "Active",
                User_id = user.Id,
                Project_code = code
            };

            // Add the new project to the context and save changes asynchronously
            _context.Projects.Add(newProject);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = newProject });
        }
    }
}
