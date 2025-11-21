using System.ComponentModel.DataAnnotations;

namespace user_service.Model
{
    public class User
    {
        public Guid Id { get; set; }
        public Guid AuthId { get; set; }
        public string Username { get; set; }
        public string Address { get; set; }
    }
}
