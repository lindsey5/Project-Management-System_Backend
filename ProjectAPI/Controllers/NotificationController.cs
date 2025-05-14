using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly ApplicationDBContext _context;

        public NotificationController(ApplicationDBContext context){
            _context = context;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetNotifications(int page = 1, int limit = 10)
        {
            try{
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
                if(idClaim == null || !int.TryParse(idClaim.Value, out int userId)) return Unauthorized(new { message = "ID not found in token." });
                
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var totalNotifications = await _context.Notifications
                .Where(n => n.User_id == userId)
                .CountAsync();

                var notifications = await _context.Notifications
                .Include(n => n.User)
                .Include(n => n.Invitation)
                    .ThenInclude(i => i.Project)
                .Where(n => n.User_id == userId)
                .OrderByDescending(n => n.Date_time)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

                var unreadNotifications = notifications.Count(n => !n.IsRead);

                return Ok(new { 
                    success = true, 
                    notifications,
                    page, 
                    totalPages = (int)Math.Ceiling((double)totalNotifications / limit),
                    totalNotifications,
                    unreadNotifications,
                });

            }catch(Exception ex){
                return StatusCode(500, new { 
                    success = false, 
                    message = "An error occurred while processing your request",
                    error = ex.Message 
                });
            }
        }

        [Authorize]
        [HttpPut]
        public async Task<IActionResult> UpdateNotifications()
        {
            try{
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
                if(idClaim == null || !int.TryParse(idClaim.Value, out int userId)) return Unauthorized(new { message = "ID not found in token." });
                
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var notifications = await _context.Notifications
                .Where(n => n.IsRead == false && n.User_id == user.Id)
                .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                _context.Notifications.UpdateRange(notifications);

                await _context.SaveChangesAsync();

                return Ok(new { success = false, message = "Notifications successfully updated"});
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
