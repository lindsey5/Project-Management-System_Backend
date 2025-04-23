using System;
using ProjectAPI.Models;

namespace ProjectAPI.DTOs;

public class CommentCreateDto
{
    public int Task_Id { get; set; }
    public string Content { get; set; } = string.Empty;

}