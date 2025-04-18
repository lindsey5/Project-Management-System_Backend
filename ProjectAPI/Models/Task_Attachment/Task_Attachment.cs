using System;

namespace ProjectAPI.Models.Task_Attachment;

public class Task_Attachment
{
    public int Id { get; set; }
    public int Task_Id { get; set; }

    public string Name { get; set; } = string.Empty;
    
    public byte[]? Content { get; set; } 
    public string Type { get; set; } = string.Empty;

}
