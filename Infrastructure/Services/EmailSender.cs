using Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _emailSettings;

        public EmailSender()
        {
            _emailSettings = new EmailSettings();
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
			try
			{
                using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port);
                client.EnableSsl = _emailSettings.EnableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.Username, _emailSettings.Nickname),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
            }
			catch (Exception ex)
			{
                throw new InvalidOperationException($"Failed to send email to {email}", ex);
            }
        }

        public class EmailSettings
        {
            public string SmtpServer { get; set; } = "smtp.gmail.com";
            public int Port { get; set; } = 587;
            public string Username { get; set; } = "ibadgram.app@gmail.com";
            public string Password { get; set; } = "olah rorp epkd vssp";
            public string Nickname { get; set; } = "Ibadgram";
            public bool EnableSsl { get; set; } = true;
        }
    }
}
