using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Recent_Opened_ProjectController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        public Recent_Opened_ProjectController(ApplicationDBContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpPost("{projectId}")]
        public async Task<IActionResult> OpenProject(int projectId)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found" });

            var recentProject = await _context.Recent_Opened_Projects
                .FirstOrDefaultAsync(rp => rp.User_Id == userId && rp.Project_Id == projectId);

            if (recentProject == null)
            {
                recentProject = new Recent_opened_project
                {
                    User_Id = userId,
                    Project_Id = projectId,
                    Last_accessed = DateTime.Now
                };
                _context.Recent_Opened_Projects.Add(recentProject);
            }
            else
            {
                recentProject.Last_accessed = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, recentProject, message = "Project opened successfully." });
        }

    }
}
