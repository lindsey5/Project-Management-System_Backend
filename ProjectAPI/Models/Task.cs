using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class Task : BaseTask
{
    public DateTime Created_At { get; set; }
    public DateTime Updated_At { get; set; }
    public int Creator { get; set; }

    [ForeignKey("Creator")]
    public Member? Member { get; set; }

    [ForeignKey("Project_Id")]
    public Project? Project { get; set; }
    
    public ICollection<Assignee> Assignees { get; set; } = new List<Assignee>();
}
