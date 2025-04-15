using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class Task : TaskBaseDto
{
    [ForeignKey("Project_Id")]
    public Project? Project { get; set; }

    public ICollection<Assignee>? Assignees { get; set; } = new List<Assignee>();
}
