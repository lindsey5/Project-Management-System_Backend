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
            var isMember = await _context.Members.AnyAsync(m => 
                m.User_Id == userId && 
                m.Project_Id == project.Id &&
                m.Status == "Active"
            );

            if (!isOwner && !isMember)
                return Unauthorized(new { success = false, message = "You must be a member or admin." });

            var members = await _context.Members
                .Where(m => m.Project_Id == project.Id && m.Status == "Active")
                .Include(m => m.User)
                .Include(m => m.User_Added_by)
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

            if (member == null)
            {
                return NotFound(new { message = "Project not found." });
            }

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
                
                if(project == null) return NotFound(new { success = false, message = "Project not found"});

                var isAdmin = await _context.Members.AnyAsync(m => m.User_Id == userId && m.Project_Id == project.Id && m.Status == "Active" && m.Role == "Admin");

                if(!isAdmin) return Unauthorized(new { success = false, message = "Deletion is restricted to admin or member only."});

                member.Status = "Inactive";
                member.Role = "Member";

                var assignees = await _context.Assignees
                    .Where(a => a.Member_Id == member.Id)
                    .ToListAsync();
                _context.RemoveRange(assignees);
                
                var recentProject = await _context.Recent_Opened_Projects
                    .FirstOrDefaultAsync(rp => rp.User_Id == member.User_Id && rp.Project_Id == project.Id);

                if(recentProject != null) _context.Recent_Opened_Projects.Remove(recentProject);

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

                    _context.Task_Histories.Add(new Task_History
                        {
                            Task_Id = null,
                            Project_Id = project.Id,
                            Prev_Value = null,
                            New_Value = null,
                            Action_Description = $"{user.Firstname} {user.Lastname} removed {member.User.Firstname} {member.User.Lastname}.",
                            Date_Time = DateTime.Now,
                    });

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
        [HttpDelete("left/{project_id}")]
        public async Task<IActionResult> LeaveProject(int project_id)
        {
            try{
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
                if(idClaim == null || !int.TryParse(idClaim.Value, out int userId)) return Unauthorized(new { message = "ID not found in token." });
                    
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return Unauthorized(new { success = false, message = "Unauthorized: User account does not exist." });

                var member = await _context.Members
                    .Include(m => m.User)
                    .Include(m => m.Project)
                    .FirstOrDefaultAsync(m => m.User_Id == userId && m.Project_Id == project_id);

                if (member == null) return NotFound(new { message = "User does not belong to this project." });

                member.Status = "Inactive";
                member.Role = "Member";

                var assignees = await _context.Assignees
                    .Where(a => a.Member_Id == member.Id)
                    .ToListAsync();
                _context.RemoveRange(assignees);

                var recentProject = await _context.Recent_Opened_Projects
                    .FirstOrDefaultAsync(rp => rp.User_Id == userId && rp.Project_Id == project_id);

                if(recentProject != null) _context.Recent_Opened_Projects.Remove(recentProject);

                if(member.User !=null && member.Project != null){
                    var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == project_id);
                    
                    if(project != null){
                        _context.Task_Histories.Add(new Task_History
                            {
                                Task_Id = null,
                                Project_Id = project.Id,
                                Prev_Value = null,
                                New_Value = null,
                                Action_Description = $"{user.Firstname} {user.Lastname} has left in the project.",
                                Date_Time = DateTime.Now,
                        });
                        
                        var admins = await _context.Members
                        .Include(m => m.User)
                        .Where(m => m.Role == "Admin" && m.Project_Id == project.Id)
                        .ToListAsync();

                        foreach(var admin in admins){
                            if(admin.User == null) continue;

                            var newNotification = new Notification
                            {
                                Message = $"{member.User.Firstname} {member.User.Lastname} has left in the project \"{project.Title}\"",
                                User_id = admin.User.Id,
                                Task_id = null,
                                Project_id = project.Id,
                                Type = "LeftFromProject",
                                Created_by = userId,
                                IsRead = false,
                                Date_time = DateTime.Now,
                                User = user
                            };
                                        
                            _context.Notifications.Add(newNotification);

                            if(_userConnectionService.GetConnections().TryGetValue(admin.User.Email, out var connectionId)){
                                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                            }
                        }
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
                
                var isAdmin = await _context.Members.AnyAsync(m => 
                    m.User_Id == userId && 
                    m.Role == "Admin" && 
                    m.Project_Id == project.Id &&
                    m.Status == "Active"
                );

                if(!isAdmin) return Unauthorized(new { success = false, message = "Member creation failed: Admin-only action"});
                
                var joinedMember = await _context.Members
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.User_Id == member.User_Id && m.Project_Id == member.Project_Id);
                
                var newTaskHistory = new Task_History
                {
                    Task_Id = null,
                    Project_Id = project.Id,
                    Prev_Value = null,
                    New_Value = null,
                    Date_Time = DateTime.Now,
                };

                var newNotification = new Notification
                {
                    Message = $"You're request has been accepted in project \"{project.Title}\"",
                    Task_id = null,
                    Project_id = project.Id,
                    Type = "RequestAccepted",
                    Created_by = userId,
                    IsRead = false,
                    Date_time = DateTime.Now,
                    User = user
                };

                if(joinedMember != null && joinedMember.Status == "Active") {
                    return Conflict(new { success = false, message = "User is already joined."});

                }else if(joinedMember != null){
                    joinedMember.Status = "Active";
                    joinedMember.Role = member.Role ?? "Member";
                    joinedMember.Added_by = userId;
                    joinedMember.Joined_At = DateTime.Now;

                    if(joinedMember != null && joinedMember.User !=null){
                        newNotification.User_id = joinedMember.User.Id;
                        newTaskHistory.Action_Description = $" {user.Firstname} {user.Lastname} has accepted the join request of {joinedMember.User.Firstname} {joinedMember.User.Lastname}.";
                    }
                }else{
                    var newMember = new Member{
                        Project_Id = member.Project_Id,
                        User_Id = member.User_Id,
                        Role = member.Role ?? "Member",
                        Joined_At = DateTime.Now,
                        Added_by = userId,
                        Status = "Active"
                    };
                    _context.Members.Add(newMember);
                    await _context.SaveChangesAsync();

                    var newMemberUser = await _context.Users.FindAsync(member.User_Id);

                    joinedMember = new Member
                    {
                        Id = newMember.Id,
                        Project_Id = newMember.Project_Id,
                        User_Id = newMember.User_Id,
                        Role = newMember.Role,
                        Joined_At = newMember.Joined_At,
                        Added_by = userId,
                        Status = newMember.Status,
                        User = newMemberUser 
                    };

                    if(newMemberUser != null){
                        newNotification.User_id = newMemberUser.Id;
                        newTaskHistory.Action_Description = $"{user.Firstname} {user.Lastname} has accepted the join request of {newMemberUser.Firstname} {newMemberUser.Lastname}.";
                    }
                }

                if(joinedMember != null && joinedMember.User != null){
                    var recentProject = new Recent_opened_project
                    {
                        User_Id = joinedMember.User_Id,
                        Project_Id = project.Id,
                        Last_accessed = DateTime.Now
                    };
                    _context.Recent_Opened_Projects.Add(recentProject);
                    _context.Notifications.Add(newNotification);
                    _context.Task_Histories.Add(newTaskHistory);

                    if(_userConnectionService.GetConnections().TryGetValue(joinedMember.User.Email, out var connectionId)){
                        await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTaskNotification", 1, newNotification);
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "New member successfully created", newMember = joinedMember});
            }catch(Exception ex){
                return StatusCode(500, new { 
                    success = false, 
                    message = "An error occurred while processing your request",
                    error = ex.Message 
                });
            }
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMember([FromBody] MemberUpdateDto updatedMember, int id)
        {
            if (updatedMember == null) return BadRequest(new { message = "Member data is missing." });

            var member = await _context.Members.Include(m => m.User).FirstOrDefaultAsync(m => m.Id == id);

            if(member == null || member.User == null) return NotFound(new { success = false, message = "Member not found"});

            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
            if(idClaim == null || !int.TryParse(idClaim.Value, out int userId)) return Unauthorized(new { message = "ID not found in token." });
                
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return Unauthorized(new { success = false, message = "Unauthorized: User account does not exist." });
                
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == member.Project_Id);

            if(project == null) return NotFound(new { success = false, message = "Project doesn't exist"});
                
            var isAdmin = await _context.Members.AnyAsync(m => 
                m.User_Id == userId && 
                m.Role == "Admin" && 
                m.Project_Id == project.Id &&
                m.Status == "Active"
            );

            if(!isAdmin) return Unauthorized(new { success = false, message = "Update failed: Admin-only action"});
            
            if(updatedMember.Role != member.Role){
                _context.Task_Histories.Add(new Task_History
                {
                    Task_Id = null,
                    Project_Id = project.Id,
                    Prev_Value = null,
                    New_Value = null,
                    Action_Description = $"{member.User.Firstname} {member.User.Lastname}'s role was updated to \"{updatedMember.Role}\" by {user.Firstname} {user.Lastname}.",
                    Date_Time = DateTime.Now,
                });

                var newNotification = new Notification
                {
                    Message = $"Your role in the project \"{project.Title}\" has been updated to {updatedMember.Role} by {member.User.Firstname} {member.User.Lastname}.",
                    User_id = member.User.Id,
                    Task_id = null,
                    Project_id = project.Id,
                    Type = "RoleUpdated",
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
            member.Status = updatedMember.Status;
            member.Role = updatedMember.Role; 
            await _context.SaveChangesAsync();

            return Ok(new { success = true, member, updatedMember}); 
        }

    }
}
