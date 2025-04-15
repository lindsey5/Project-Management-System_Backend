namespace ProjectAPI.Models;

public abstract class TaskBaseDto
{
    public int Id { get; set; } // Primary key
    public int Project_Id { get; set; }
    public string Task_Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Due_date { get; set; }
}
