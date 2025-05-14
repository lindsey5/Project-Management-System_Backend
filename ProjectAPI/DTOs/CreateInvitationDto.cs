using System;
using System.ComponentModel.DataAnnotations;

namespace ProjectAPI.DTOs;

public class CreateInvitationDto
{
    public int Project_id { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
