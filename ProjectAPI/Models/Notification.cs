using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class Notification
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? Task_id { get; set; }
    public int? Project_id { get; set; }
    public int User_id { get; set; }
    public int Created_by { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime Date_time { get; set; }
    
    [ForeignKey(nameof(Created_by))]
    public User? User { get; set; }

}
