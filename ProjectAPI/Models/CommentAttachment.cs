using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class CommentAttachment
{
    public int Id { get; set; }
    public int Comment_Id { get; set; }
    public byte[]? Content { get; set; }

}
