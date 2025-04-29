using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;
using ProjectAPI.Services;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MemberController : ControllerBase
    {

        private readonly ApplicationDBContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserConnectionService _userConnectionService;

         public MemberController(
            ApplicationDBContext context,
            IHubContext<NotificationHub> hubContext,
            UserConnectionService userConnectionService
        )
        {
            _context = context;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
        }

        [Authorize]
        [HttpGet()]
        public async Task<IActionResult> GetMembers([FromQuery] string project_code)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "ID not found in token." });


            var project = await _context.Projects
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Project_code == project_code);

            if (project == null)
                return NotFound(new { success = false, message = "Project not found." });

            // Check if user is either the project owner or a member
            var isOwner = project.User_id == userId;
            var isMember = await _context.Members.AnyAsync(m => m.User_Id == userId && m.Project_Id == project.Id);

            if (!isOwner && !isMember)
                return Unauthorized(new { success = false, message = "You must be a member or admin." });

            var members = await _context.Members
                .Where(m => m.Project_Id == project.Id && m.Status == "Active")
                .Include(m => m.User)
                .ToListAsync();

            return Ok(new { success = true, members });
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMemberById(int id)
        {
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMember(int id)
        {
            try{
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
                if(idClaim == null || !int.TryParse(idClaim.Value, out int userId)) return Unauthorized(new { message = "ID not found in token." });
                    
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return Unauthorized(new { success = false, message = "Unauthorized: User account does not exist." });

                var member = await _context.Members
                    .Include(m => m.User)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (member == null) return NotFound(new { message = "Member not found." });

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == member.Project_Id);

                if(project == null) return Unauthorized(new { success = false, message = "Deletion is restricted to admin or member only."});

                member.Status = "Inactive";

                var assignees = await _context.Assignees
                    .Where(a => a.Member_Id == member.Id)
                    .ToListAsync();
                _context.RemoveRange(assignees);

                if(member.User !=null){
                    var newNotification = new Notification
                    {
                        Message = $"You have been removed in project \"{project.Title}\"",
                        User_id = member.User.Id,
                        Task_id = null,
                        Project_id = project.Id,
                        Type = "RemovedToProject",
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
        
                await _context.SaveChangesAsync();
    
                return Ok(new { success = true, message = "Member successfully removed" });
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
        public async Task<IActionResult> CreateMember([FromBody] Member member)
        {
            try{
                if (member == null) return BadRequest(new { message = "Member data is missing." });

                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
                if(idClaim == null || !int.TryParse(idClaim.Value, out int userId)) return Unauthorized(new { message = "ID not found in token." });
                
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return Unauthorized(new { success = false, message = "Unauthorized: User account does not exist." });
                
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == member.Project_Id);

                if(project == null) return NotFound(new { success = false, message = "Project doesn't exist"});
                
                var isAdmin = await _context.Members.AnyAsync(m => m.User_Id == userId && m.Role == "Admin" && m.Project_Id == project.Id);

                if(!isAdmin) return Unauthorized(new { success = false, message = "Member creation failed: Admin-only action"});
                
                var joinedMember = await _context.Members
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.User_Id == member.User_Id && m.Project_Id == member.Project_Id);
                
                if(joinedMember != null && joinedMember.Status == "Active") {
                    return Conflict(new { success = false, message = "User is already joined."});
                }else if(joinedMember != null){
                    joinedMember.Status = "Active";
                    if(joinedMember != null && joinedMember.User !=null){
                        var newNotification = new Notification
                        {
                            Message = $"You have been added in project \"{project.Title}\"",
                            User_id = joinedMember.User.Id,
                            Task_id = null,
                            Project_id = project.Id,
                            Type = "AddedToProject",
                            Created_by = userId,
                            IsRead = false,
                            Date_time = DateTime.Now,
                            User = user
                        };
                                
                        _context.Notifications.Add(newNotification);

                        if(_userConnectionService.GetConnections().TryGetValue(joinedMember.User.Email, out var connectionId)){
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                        }
                    }
                }else{
                    var newMember = new Member{
                        Project_Id = member.Project_Id,
                        User_Id = member.User_Id,
                        Role = "Member",
                        Joined_At = DateTime.Now,
                        Status = "Active"
                    };
                    _context.Members.Add(newMember);

                    var createdMember = await _context.Members
                        .Include(m => m.User)
                        .FirstOrDefaultAsync(m => m.Id == newMember.Id);

                    if(createdMember != null && createdMember.User !=null){
                        var newNotification = new Notification
                        {
                            Message = $"You have been added in project \"{project.Title}\"",
                            User_id = createdMember.User.Id,
                            Task_id = null,
                            Project_id = project.Id,
                            Type = "AddedToProject",
                            Created_by = userId,
                            IsRead = false,
                            Date_time = DateTime.Now,
                            User = user
                        };
                                
                        _context.Notifications.Add(newNotification);

                        if(_userConnectionService.GetConnections().TryGetValue(createdMember.User.Email, out var connectionId)){
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                 return Ok(new { success = true, message = "New member successfully created"});
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
