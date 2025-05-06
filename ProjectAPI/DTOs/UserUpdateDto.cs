using System;

namespace ProjectAPI.DTOs;

public class UserUpdateDto
{
    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
    public byte[]? Profile_pic { get; set; }

}
