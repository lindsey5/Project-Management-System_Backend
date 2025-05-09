using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectAPI.Models
{
    public class User
    {
        public int Id { get; set; } // Primary key
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; } = string.Empty;
    
        public string Password { get; set; } = string.Empty;
        public string Firstname { get; set; } = string.Empty;
        public string Lastname { get; set; } = string.Empty;
        public byte[]? Profile_pic { get; set; }

    }
}
