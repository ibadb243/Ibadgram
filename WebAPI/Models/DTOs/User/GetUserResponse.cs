namespace WebAPI.Models.DTOs.User
{
    public class GetUserResponse
    {
        public string Firstname { get; set; }
        public string? Lastname { get; set; }
        public string Shortname { get; set; }
        public string? Bio { get; set; }
        public string Status { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }
}
