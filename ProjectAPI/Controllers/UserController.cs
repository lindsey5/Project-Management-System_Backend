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
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (idClaim == null)
                return Unauthorized(new { message = "Id not found in token." });

            int userId = int.Parse(idClaim.Value);

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);

            return Ok(new { user });
        }


    }
}
