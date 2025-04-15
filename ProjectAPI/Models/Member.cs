using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models;

public class Member
{
    public int Id { get; set; }
    public int User_Id { get; set; }
    public int Project_Id { get; set;}

    [ForeignKey("User_Id")]
    public User? User { get; set; }

}
