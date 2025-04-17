using System;
using System.ComponentModel.DataAnnotations;

namespace ProjectAPI.Models;

public class TaskCreateDto : TaskBaseDto
{
    [Required]
    public List<int> AssigneesMemberId { get; set; } = new List<int>();
}
