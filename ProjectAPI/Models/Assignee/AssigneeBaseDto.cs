using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models
{
    public class AssigneeBaseDto
    {
        public int Id { get; set; }
        public int User_Id { get; set; }
        public int Task_Id { get; set; }

    }
}
