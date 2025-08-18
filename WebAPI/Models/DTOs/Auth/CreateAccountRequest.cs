namespace WebAPI.Models.DTOs.Auth
{
    public class CreateAccountRequest
    {
        public string Firstname { get; set; }
        public string? Lastname { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
