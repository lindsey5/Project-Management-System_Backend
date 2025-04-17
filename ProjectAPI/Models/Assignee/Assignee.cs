using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ProjectAPI.Models
{
    public class Assignee : AssigneeBaseDto
    {
        
        [ForeignKey(nameof(Member_Id))]
        public Member? Member { get; set; }

        [ForeignKey(nameof(Task_Id))]
        public Task? Task { get; set; }
    }
}
