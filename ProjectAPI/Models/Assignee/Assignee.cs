using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models
{
    public class Assignee : AssigneeBaseDto
    {
        
        [ForeignKey("User_Id")]
        public User? User { get; set; }

        [ForeignKey("Task_Id")]
        public Task? Task {get; set; }
    }
}
