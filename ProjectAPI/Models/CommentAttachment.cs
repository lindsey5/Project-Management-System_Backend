using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class CommentAttachment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Comment_Id { get; set; }
    public byte[]? Content { get; set; }
    public string Type { get; set; } = string.Empty;

}
