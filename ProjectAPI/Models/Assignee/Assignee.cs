using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models
{
    public class Assignee : AssigneeBaseDto
    {
        
        [ForeignKey("Member_Id")]
        public Member? Member { get; set; }

        [ForeignKey("Task_Id")]
        public Task? Task {get; set; }
    }
}
