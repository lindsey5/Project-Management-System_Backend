using System;
using ProjectAPI.Models;

namespace ProjectAPI.DTOs;

public class AssigneesUpdateDto
{
    public List<Assignee> AssigneesToAdd { get; set; } = new List<Assignee>();
    public List<Assignee> AssigneesToRemove { get; set; } = new List<Assignee>();

}
