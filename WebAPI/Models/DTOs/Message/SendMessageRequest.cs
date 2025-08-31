namespace WebAPI.Models.DTOs.Message
{
    public class SendMessageRequest
    {
        public Guid chatId { get; set; }
        public string text { get; set; } = string.Empty;
    }
}
