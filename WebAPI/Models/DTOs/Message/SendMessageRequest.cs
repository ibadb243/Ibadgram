namespace WebAPI.Models.DTOs.Message
{
    public class SendMessageRequest
    {
        public Guid ChatId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
