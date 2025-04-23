using System;

namespace ProjectAPI.DTOs;

public class CommentAttachmentDto
{
    public int Comment_Id { get; set; }
    public IFormFile? File { get; set; }
}
