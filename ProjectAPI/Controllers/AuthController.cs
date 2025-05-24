using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;
using ProjectAPI.Services;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly AuthService _authService;

        public AuthController(ApplicationDBContext context, AuthService authService)
        {
            _context = context;
            _authService = authService;
            _passwordHasher = new PasswordHasher<User>();
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] User loginUser)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginUser.Email);
            if (user == null)
            {
                _context.Users.Add(loginUser);
                await _context.SaveChangesAsync();
                user = loginUser;
            }

            var token = _authService.GenerateJwtToken(user);

            return Ok(new
            {
                success = true,
                message = "Login successful.",
                token,
            });
        }

        [HttpPost("signup")]
        public async Task<ActionResult> SignUp([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.Firstname)) return BadRequest(new { message = "Firstname is required" });
            if (string.IsNullOrEmpty(user.Lastname)) return BadRequest(new { message = "Lastname is required" });
            if (string.IsNullOrEmpty(user.Email)) return BadRequest(new { message = "Email is required" });
            if (string.IsNullOrEmpty(user.Password)) return BadRequest(new { message = "Password is required" });

            var isEmailExist = await _context.Users.AnyAsync(u => u.Email == user.Email);
            if (isEmailExist) return BadRequest(new { message = "Email already exists." });

            user.Password = _passwordHasher.HashPassword(user, user.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _authService.GenerateJwtToken(user);

            return Ok(new { success = true, user, token });
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] User loginUser)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginUser.Email);
            if (user == null) return Unauthorized(new { success = false, message = "Email doesn't exist" });

            var result = _passwordHasher.VerifyHashedPassword(user, user.Password, loginUser.Password);
            if (result == PasswordVerificationResult.Failed) return Unauthorized(new { success = false, message = "Incorrect email or password." });

            var token = _authService.GenerateJwtToken(user);

            return Ok(new
            {
                success = true,
                message = "Login successful.",
                token
            });
        }

        // POST: api/auth/logout
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt");
            return Ok(new { message = "Logged out successfully" });
        }
        
        [HttpPost("signup")]
        public async Task<ActionResult> SignUp([FromBody] User user)
        {
            if(string.IsNullOrEmpty(user.Firstname)) return BadRequest(new { message= "Firstname is required"});
            if(string.IsNullOrEmpty(user.Lastname)) return BadRequest(new { message= "Lastname is required"});
            if(string.IsNullOrEmpty(user.Email)) return BadRequest(new { message = "Email is required"});
            if(string.IsNullOrEmpty(user.Password)) return BadRequest(new { message = "Password is required"});

            var isEmailExist = await _context.Users.AnyAsync(u => u.Email == user.Email);
            if (isEmailExist) return BadRequest(new { message = "Email already exists." });

            user.Password = _passwordHasher.HashPassword(user, user.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _authService.GenerateJwtToken(user);

            return Ok(new { success = true, user, token}); 
        }
    }
}
