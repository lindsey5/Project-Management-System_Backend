using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRequest(int id, string status = "Pending"){
            try
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var request = await _context.Requests.FindAsync(id);
                if (request == null)
                    return NotFound(new { success = false, message = "Request not found" });

                var isAuthorized = await _context.Members
                    .AnyAsync(m => m.Project_Id == request.Project_Id && m.User_Id == userId && m.Role == "Admin");
                if (!isAuthorized)
                    return Unauthorized(new { success = false, message = "Only project admin can update." });

                request.Status = status;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, request });
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
        [HttpGet("{project_id}")]
        public async Task<IActionResult> GetRequests(
            int project_id, 
            string searchTerm = "", 
            string status = "", 
            int page = 1, int limit = 10
        )
        {
            try{
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });
                
                var project = await _context.Projects.FindAsync(project_id); 
                if(project == null) 
                    return NotFound(new { success = false, message = "Project not found"});

                bool isAdmin = await _context.Members
                    .AnyAsync(m => m.Project_Id == project.Id 
                        && m.User_Id == user.Id && m.Role == "Admin");
                
                if(!isAdmin) return Unauthorized(new { success = false, message = "Only admin is authorized"});

                var loweredSearchTerm = searchTerm?.ToLower();
                var totalRequests = await _context.Requests
                    .Include(r => r.User)
                    .Where(r =>  
                        r.Project_Id == project_id &&
                        (string.IsNullOrEmpty(status) || r.Status == status) && ( 
                        string.IsNullOrEmpty(loweredSearchTerm) || 
                        r.User.Email.ToLower().Contains(loweredSearchTerm) ||
                        r.User.Firstname.ToLower().Contains(loweredSearchTerm) ||
                        r.User.Lastname.ToLower().Contains(loweredSearchTerm)
                    ))
                    .CountAsync();

                var requests = await _context.Requests
                    .Include(r => r.User)
                    .Where(r => 
                        r.Project_Id == project_id && 
                        (string.IsNullOrEmpty(status) || r.Status == status)
                        && (
                            string.IsNullOrEmpty(loweredSearchTerm) || 
                            r.User.Email.ToLower().Contains(loweredSearchTerm) ||
                            r.User.Firstname.ToLower().Contains(loweredSearchTerm) ||
                            r.User.Lastname.ToLower().Contains(loweredSearchTerm)
                        )
                    )
                    .OrderByDescending(d => d.Request_Date)
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .ToListAsync();

                return Ok(new { 
                    success = true, 
                    page,
                    limit,
                    totalPages = (int)Math.Ceiling((double)totalRequests / limit),
                    totalRequests,
                    requests,
                });

            }catch(Exception ex){
                return StatusCode(500, new { 
                    success = false, 
                    message = "An internal error occurred",
                });
            }
        }


        [Authorize]
        [HttpPost()]
        public async Task<IActionResult> CreateRequest([FromBody] RequestDto requestDto)
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

                var project = await _context.Projects
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Project_code == requestDto.Project_Code);
                    
                if (project == null)
                    return NotFound(new { 
                        success = false, 
                        message = "Project not found" 
                });

                if (await _context.Members
                    .AnyAsync(m => m.Project_Id == project.Id && m.User_Id == userId && m.Status == "Active"))
                    return Conflict(new { 
                        success = false, 
                        message = "You're already part of this project",
                    });

                var existingRequest = await _context.Requests
                    .FirstOrDefaultAsync(r => 
                        r.Project_Id == project.Id && 
                        r.User_Id == userId &&
                        r.Status == "Pending");

                if (existingRequest != null)
                    return Conflict(new {
                        success = false,
                        message = "You've already submitted a request",
                        requestId = existingRequest.Id
                    });

                var request = new Models.Request
                {
                    Project_Id = project.Id,
                    User_Id = userId,
                    Status = "Pending",
                    Request_Date = DateTime.Now
                };

                _context.Requests.Add(request);
                await _context.SaveChangesAsync();

                // 7. Return response
                return Ok( new {
                        success = true,
                        message = "Request created successfully",
                        project
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"An internal error occurred, {ex.Message}",
                });
            }
        }
    }
    
}
