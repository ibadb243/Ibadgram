namespace WebAPI.Models.DTOs.Chat
{
    public class CreateGroupRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPrivate { get; set; } = false;
        public string? Shortname { get; set; }
    }
}
