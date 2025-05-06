using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ProjectAPI.DTOs;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDBContext _context;

         public UserController(ApplicationDBContext context)
        {
            _context = context;
        }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

        if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

        var user = await _context.Users.FindAsync(userId);

        if(user == null) return NotFound(new { success = false, message = "User not found."}); 

        var base64Image = user.Profile_pic != null ? Convert.ToBase64String(user.Profile_pic) : null;
        
        return Ok(new { 
            email = user.Email, 
            firstname = user.Firstname, 
            lastname = user.Lastname, 
            profile_pic = $"data:image/jpeg;base64,{base64Image}" 
        });
    }

    [Authorize]
    [HttpPut()]
    public async Task<IActionResult> UpdateProfile([FromBody] UserUpdateDto userUpdateDto)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

        if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                    return Unauthorized(new { success = false, message = "Invalid user token" });

        var user = await _context.Users.FindAsync(userId);

        if(user == null) return NotFound(new { success = false, message = "User not found."}); 

        user.Firstname = userUpdateDto.Firstname;
        user.Lastname = userUpdateDto.Lastname;

        if (userUpdateDto.Profile_pic != null && userUpdateDto.Profile_pic.Length > 0)
        {
            user.Profile_pic = userUpdateDto.Profile_pic;
        }

        await _context.SaveChangesAsync();
        
        return Ok(new { 
            success = true,
            user
        });
    }

    }
}
