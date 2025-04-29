using System;

namespace ProjectAPI.Models;

public class ProjectDto
{
    public int Id { get; set; } 
    public string Project_code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly Start_date { get; set; }
    public DateOnly End_date { get; set; }
    public DateTime Created_At { get; set; }
    public int User_id { get; set; } 

}
