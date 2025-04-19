using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectAPI.Models
{
    public class Project
    {
        public int Id { get; set; } // Primary key
        public string Project_code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateOnly Start_date { get; set; }
        public DateOnly End_date { get; set; }
        public DateTime Created_At { get; set; }
        public int User_id { get; set; } // Foreign key

        [ForeignKey(nameof(User_id))]
        public User? User { get; set; }

        public ICollection<Member> Members { get; set; } = new List<Member>();

    }
}
