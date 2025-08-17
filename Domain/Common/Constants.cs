namespace Domain.Common.Constants
{
    public static class UserConstants
    {
        public const int FirstnameMinLength = 1;
        public const int FirstnameMaxLength = 64;

        public const int LastnameLength = 64;

        public const int BioLength = 2048;

        public const int PasswordMinLength = 8;
        public const int PasswordMaxLength = 24;
    }

    public static class EmailConstants
    {
        public readonly static string[] AllowedDomains = new[]
        {
            "gmail.com",
            "yahoo.com",
            "yandex.ru",
            "mail.ru",
        };

        public const int EmailConfirmationTokenLength = 6;
        public readonly static TimeSpan EmailConfirmationTokenExpiry = new TimeSpan(0, 5, 0);
    }

    public static class ShortnameConstants
    {
        public const int MinLength = 4;
        public const int MaxLength = 64;
    }

    public static class ChatConstants
    {
        public const int NameMinLength = 1;
        public const int NameMaxLength = 128;

        public const int DescriptionLength = 1024;
    }

    public static class MessageConstants
    {
        public const int MinLength = 1;
        public const int MaxLength = 1024;
    }
}
