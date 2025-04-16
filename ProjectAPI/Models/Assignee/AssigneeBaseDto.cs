using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models
{
    public class AssigneeBaseDto
    {
        public int Id { get; set; }
        public int Member_Id { get; set; }
        public int Task_Id { get; set; }

    }
}
