using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class MemberUpdateDto
{
    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = "Active"; 

}
