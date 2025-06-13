namespace Domain.Common
{
    public abstract class Mention
    {
        public Guid Id { get; set; }
        public string Shortname { get; set; } = string.Empty;
    }
}
