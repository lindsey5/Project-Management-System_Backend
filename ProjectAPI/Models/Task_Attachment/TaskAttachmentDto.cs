using System;

namespace ProjectAPI.Models.Task_Attachment;

public class TaskAttachmentDto
{
    public int Task_Id { get; set; }
    public IFormFile? File { get; set; }
}