using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class Comment
{
    public int Id { get; set; }
    public int Task_Id { get; set; }
    public int Member_Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Date_time { get; set; }
    
    [ForeignKey(nameof(Member_Id))]
    public Member? Member { get; set; }
    
    [ForeignKey(nameof(Task_Id))]
    public Task? Task { get; set; }  
}
