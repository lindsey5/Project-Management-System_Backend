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


        public int GenerateVerificationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999); 
        }

        [HttpPost("signup/verification-code")]
        public async Task<IActionResult> SendSignupVerificationEmail([FromQuery] string email)
        {
            try
            {
                if(email == null) return BadRequest(new { success = false, message = "Email is required"});

                var isExist = await _context.Users.AnyAsync(u => u.Email == email);

                if(isExist) return Conflict(new { success = false, message = "Email is already registered"});

                int code = GenerateVerificationCode();
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

                return Ok(new { success = true, verification_code = code });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to send email", error = ex.Message });
            }
        }
    }
}
