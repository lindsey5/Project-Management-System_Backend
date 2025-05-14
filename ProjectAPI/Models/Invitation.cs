
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class Invitation
{
    public int Id { get; set; }
    public int Project_id { get; set; }
    public int User_id { get; set; }
    public int Created_by { get; set; }
    
    public string Status { get; set; } = string.Empty;

    [ForeignKey(nameof(Project_id))]
    public Project? Project { get; set; }

}
