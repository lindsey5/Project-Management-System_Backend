using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.DTOs;
using ProjectAPI.Models;
using ProjectAPI.Services;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvitationController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserConnectionService _userConnectionService;

        public InvitationController(
            ApplicationDBContext context, 
            IHubContext<NotificationHub> hubContext,
            UserConnectionService userConnectionService)
        {
            _context = context;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
        }

        [Authorize]
        [HttpPost()]
        public async Task<IActionResult> CreateInvitation(CreateInvitationDto invitationDto){
            try
                {
                    var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                        return Unauthorized(new { success = false, message = "Invalid user token" });

                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                        return NotFound(new { success = false, message = "User not found" });
                    
                    var isAdmin = await _context.Members
                        .AnyAsync(m => m.Role == "Admin" && m.User_Id == userId && m.Project_Id == invitationDto.Project_id);

                    if(!isAdmin) return Unauthorized(new { success = false, message = "Permission Denied: Only administrators are allowed to perform this action."});
                    
                    var receiver = await _context.Users.FirstOrDefaultAsync(u => u.Email == invitationDto.Email);

                    if(receiver == null) return NotFound( new { success = false, message = "Email does not exist."});

                    var project = await _context.Projects.FindAsync(invitationDto.Project_id);

                    if(project == null) return NotFound( new { success = false, message = "Project ID not found"});

                   if (userId == receiver.Id) return BadRequest(new { success =  false, message =  "You cannot send an invitation to your own account." });

                    var isMember = await _context.Members.AnyAsync(m => m.Project_Id == project.Id && m.User_Id == receiver.Id && m.Status == "Active");

                    if(isMember) return Conflict( new { success = false, message = "The user is already part of the project."}); 

                    var newInvitation = new Invitation{
                        Project_id = project.Id,
                        User_id = receiver.Id,
                        Created_by = userId,
                        Status = "Pending"
                    };

                    _context.Invitations.Add(newInvitation);

                    await _context.SaveChangesAsync();

                    var notification = new Notification
                    {
                        Message = invitationDto.Message,
                        User_id = receiver.Id,
                        Task_id = null,
                        Invitation_id = newInvitation.Id,
                        Project_id = project.Id,
                        Type = "InvitationSent",
                        Created_by = userId,
                        IsRead = false,
                        Date_time = DateTime.Now,
                        User = user
                    };

                    _context.Notifications.Add(notification);

                    if (_userConnectionService.GetConnections().TryGetValue(receiver.Email, out var connectionId))
                    {
                        await _hubContext.Clients.Client(connectionId)
                            .SendAsync("ReceiveTaskNotification", 1, notification);
                    }

                    await _context.SaveChangesAsync();

                    return Ok(new { success = true, newInvitation});
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
        [HttpPut("accept/{id}")]
        public async Task<IActionResult> AccepInvitation(int id){
            try
                {
                    var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
                        return Unauthorized(new { success = false, message = "Invalid user token" });

                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                        return NotFound(new { success = false, message = "User not found" });
                    
                    var invitation = await _context.Invitations.Include(i => i.Project).FirstOrDefaultAsync(i => i.Id == id);
                    
                    if(invitation == null) return NotFound(new { success = false, message = "Invitation id does'nt exist."}); 

                    if(invitation.Project != null){
                        var invitationCreator = await _context.Users.FindAsync(invitation.Created_by);

                        var notification = new Notification
                        {
                            Message = $"{user.Firstname} {user.Lastname} has accepted your invitation in a project \"{invitation.Project.Title}\"",
                            User_id = invitation.Created_by,
                            Task_id = null,
                            Invitation_id = null,
                            Project_id = invitation.Project.Id ,
                            Type = "InvitationAccepted",
                            Created_by = userId,
                            IsRead = false,
                            Date_time = DateTime.Now,
                            User = user
                        };

                        var newTaskHistory = new Task_History
                        {
                            Task_Id = null,
                            Project_Id = invitation.Project_id,
                            Prev_Value = null,
                            New_Value = null,
                            Date_Time = DateTime.Now,
                            Action_Description = $"{user.Firstname} {user.Lastname} has accepted the invitation of {invitationCreator?.Firstname} {invitationCreator?.Lastname}."
                        };
                        
                        var joinedMember = await _context.Members.FirstOrDefaultAsync(m => m.User_Id == userId && m.Project_Id == invitation.Project.Id);
                        
                        if(joinedMember != null && joinedMember.Status == "Active") return Conflict(new { success = false, message = "You are already part of this project"});
                        else if(joinedMember != null){
                            joinedMember.Status = "Active";
                            joinedMember.Role = "Member";
                            joinedMember.Added_by = invitation.Created_by;
                            joinedMember.Joined_At = DateTime.Now;      
                        }else{
                            var newMember = new Member{
                                Project_Id = invitation.Project_id,
                                User_Id = userId,
                                Role = "Member",
                                Joined_At = DateTime.Now,
                                Added_by = userId,
                                Status = "Active"
                            };

                            _context.Members.Add(newMember);
                        }

                        var projectInvitations = await _context.Invitations
                            .Where(i => i.Project_id == invitation.Project.Id && i.User_id == userId)
                            .ToListAsync();

                        foreach(var projectInvitation in projectInvitations){
                            projectInvitation.Status = "Accepted";
                        }

                         var recentProject = new Recent_opened_project
                            {
                                User_Id = userId,
                                Project_Id = invitation.Project.Id,
                                Last_accessed = DateTime.Now
                            };
                            
                         _context.Recent_Opened_Projects.Add(recentProject);
                        _context.Task_Histories.Add(newTaskHistory);
                         _context.Notifications.Add(notification);

                        if (invitationCreator != null && _userConnectionService.GetConnections().TryGetValue(invitationCreator.Email, out var connectionId))
                        {
                            await _hubContext.Clients.Client(connectionId)
                                .SendAsync("ReceiveTaskNotification", 1, notification);
                        }

                        await _context.SaveChangesAsync();

                        return Ok(new { success = true, message = "Invitation successfully accepted", invitation});
                    }
                        
                    return BadRequest(new { success = false, message = "Project doesn't exist anymore"});
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

    }
}
