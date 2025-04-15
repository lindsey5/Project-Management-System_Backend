using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class Request
{
    public int Id { get; set; }
    public int User_Id { get; set; }
    public int Project_Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Request_Date { get; set; }

    [ForeignKey("User_Id")]
    public User? User { get; set; }
}
