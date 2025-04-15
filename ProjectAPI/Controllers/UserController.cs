using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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
    public IActionResult GetProfile()
    {
        // Get the email from the claims
        var emailClaim = User.FindFirst(ClaimTypes.Email);
        Console.WriteLine(emailClaim);

        if (emailClaim == null)
            return Unauthorized(new { message = "Email not found in token." });

        var user = _context.Users.FirstOrDefault(u => u.Email == emailClaim.Value);

        if (user == null)
            return NotFound(new { message = "User not found." });

        var base64Image = user.Profile_pic != null ? Convert.ToBase64String(user.Profile_pic) : null;
        return Ok(new { 
            email = user.Email, 
            firstname = user.Firstname, 
            lastname = user.Lastname, 
            byteArray = user.Profile_pic,
            profile_pic = $"data:image/jpeg;base64,{base64Image}" 
        });
    }

    }
}
