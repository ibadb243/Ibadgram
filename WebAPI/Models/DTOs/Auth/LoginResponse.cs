namespace WebAPI.Models.DTOs.Auth
{
    public class LoginResponse
    {
        public Guid UserId { get; set; }
        public string Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Bio { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}
