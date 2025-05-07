using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class Recent_opened_project
{
    public int Id { get; set; }
    public int User_Id { get; set; }
    public int Project_Id { get; set; }
    public DateTime Last_accessed { get; set; }

    [ForeignKey(nameof(Project_Id))]
    public Project? Project { get; set; }
}
