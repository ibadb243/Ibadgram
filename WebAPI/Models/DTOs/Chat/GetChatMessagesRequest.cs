namespace WebAPI.Models.DTOs.Chat
{
    public class GetChatMessagesRequest
    {
        public int Limit { get; set; } = 10;
        public int Offset { get; set; } = 0;
    }
}
