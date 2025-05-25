using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {

        private readonly ApplicationDBContext _context;

        public EmailController(ApplicationDBContext context)
        {
            _context = context;
        }

        private void sendEmail(int code, string email)
        {
            var fromAddress = new MailAddress(Environment.GetEnvironmentVariable("EMAIL") ?? "", "ProJex");
            var toAddress = new MailAddress(email);
            string fromPassword = Environment.GetEnvironmentVariable("PASSWORD") ?? "";

            const string subject = "Your Verification Code";
            string body = $"Your verification code is: {code}";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body
            })
            {
                smtp.Send(message);
            }
        }

        private int GenerateVerificationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999);
        }

        [HttpPost("signup/verification-code")]
        public async Task<IActionResult> SendSignupVerificationEmail([FromQuery] string email)
        {
            try
            {
                if (email == null) return BadRequest(new { success = false, message = "Email is required" });

                var isExist = await _context.Users.AnyAsync(u => u.Email == email);

                if (isExist) return Conflict(new { success = false, message = "Email is already registered" });

                int code = GenerateVerificationCode();

                sendEmail(code, email);

                return Ok(new { success = true, verification_code = code });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to send email", error = ex.Message });
            }
        }
        
        [HttpPost()]
        public async Task<IActionResult> SendVerificationCode([FromQuery] string email)
        {
            try
            {
                if(email == null) return BadRequest(new { success = false, message = "Email is required"});

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

                if(user == null) return NotFound(new { success = false, message = "Email not found"});

                if(string.IsNullOrEmpty(user.Password)) if (user.Password == null) return BadRequest(new { success = false, message = "Changing password is not allowed for Google-linked accounts." });

                int code = GenerateVerificationCode();

                sendEmail(code, email);

                return Ok(new { success = true, verification_code = code });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to send email", error = ex.Message });
            }
        }
    }
}
