using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RequestController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        public RequestController(ApplicationDBContext context){
            _context = context;
        }
        
        public class RequestDto
        {
            [Required]
            public string Project_Code { get; set; } = string.Empty;
        }

        [Authorize]
        [HttpPost()]public async Task<IActionResult> CreateRequest([FromBody] RequestDto requestDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new {
                        success = false,
                        message = "Validation errors",
                        errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                    });
                }

                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Project_code == requestDto.Project_Code);
                    
                if (project == null)
                    return NotFound(new { 
                        success = false, 
                        message = "Project not found" 
                });

                bool isOwner = await _context.Projects
                    .AnyAsync(p => p.Id == project.Id && p.User_id == userId);
                    
                bool isMember = await _context.Members
                    .AnyAsync(m => m.Project_Id == project.Id && m.User_Id == userId);

                if (isOwner || isMember)
                    return Conflict(new { 
                        success = false, 
                        message = "User is already part of this project",
                    });

                var existingRequest = await _context.Requests
                    .FirstOrDefaultAsync(r => 
                        r.Project_Id == project.Id && 
                        r.User_Id == userId &&
                        r.Status == "Pending");

                if (existingRequest != null)
                    return Conflict(new {
                        success = false,
                        message = "Pending request already exists",
                        requestId = existingRequest.Id
                    });

                var request = new Models.Request
                {
                    Project_Id = project.Id,
                    User_Id = userId,
                    Status = "Pending",
                    Request_Date = DateTime.UtcNow
                };

                _context.Requests.Add(request);
                await _context.SaveChangesAsync();

                // 7. Return response
                return Ok( new {
                        success = true,
                        message = "Request created successfully"
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = "An internal error occurred",
                });
            }
        }
    }
    
}
