using System;
using System.ComponentModel.DataAnnotations.Schema;
namespace ProjectAPI.Models;

public class Task_History
{   
    public int Id { get; set; }
    public int Task_Id { get; set; }
    public int Project_Id { get; set; }
    public string? Prev_Value { get; set; }
    public string? New_Value { get; set; }
    public string Action_Description { get; set; } = string.Empty;
    public DateTime Date_Time { get; set; }

    [ForeignKey(nameof(Task_Id))]
    public Task? Task { get; set; }
}
