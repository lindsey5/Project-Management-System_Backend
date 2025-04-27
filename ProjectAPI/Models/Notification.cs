using System;

namespace ProjectAPI.Models;

public class Notification
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Task_id { get; set; }
    public int Project_id { get; set; }
    public int User_id { get; set; }
    public string Notification_type { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }

}
