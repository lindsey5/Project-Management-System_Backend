using System;

namespace ProjectAPI.DTOs;

public class PasswordDto
{
    public string Password { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
