using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MemberController : ControllerBase
    {

        private readonly ApplicationDBContext _context;
         public MemberController(ApplicationDBContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMemberById(int id)
        {
            // Fetch the project by its ID, including the related user (if needed)
            var member = await _context.Members
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            // If the project is not found, return a NotFound response
            if (member == null)
            {
                return NotFound(new { message = "Project not found." });
            }

            // Return the found project
            return Ok(new { success = true, member });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateMember([FromBody] Member member)
        {
            try{
                if (member == null) return BadRequest(new { message = "Member data is missing." });

                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
                if(idClaim == null ) return Unauthorized(new { message = "ID not found in token." });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Convert.ToInt32(idClaim.Value));

                if (user == null) return Unauthorized(new { message = "User not found." });
                
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == member.Project_Id && (p.User_id == user.Id));

                if(project == null || (project == null && 
                await _context.Members.FirstOrDefaultAsync(m => 
                    m.Role == "Admin" && 
                    m.User_Id == user.Id && 
                    m.Project_Id == member.Project_Id) == null)) return Unauthorized(new { success = false, message = "Member creation failed: Admin-only action"});
                
                if(await _context.Members.FirstOrDefaultAsync(m => m.User_Id == member.User_Id && m.Project_Id == member.Project_Id) != null) return Conflict(new { success = false, message = "User is already joined."});

                var newMember = new Member{
                    Project_Id = member.Project_Id,
                    User_Id = member.User_Id,
                    Role = "Member"
                };
                _context.Members.Add(newMember);
                await _context.SaveChangesAsync();

                var createdMember = await _context.Members
                    .Include(m => m.User)
                    .FirstOrDefaultAsync(m => m.Id == newMember.Id);
                
                return CreatedAtAction(nameof(GetMemberById), new { id = newMember.Id }, createdMember);
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
