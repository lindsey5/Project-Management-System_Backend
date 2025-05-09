using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class Member
{
    public int Id { get; set; }
    public int User_Id { get; set; }
    public int Project_Id { get; set;}
    public string Role { get; set; } = string.Empty;
    public DateTime Joined_At { get; set; }

    public string Status { get; set; } = string.Empty; 
    
    [ForeignKey(nameof(User_Id))]
    public User? User { get; set; }

    [ForeignKey(nameof(Project_Id))]
    public Project? Project { get; set; }

}
