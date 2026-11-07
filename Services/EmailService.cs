using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using OnlineRecipeSharing.Controllers;
using System.Net.Mail;

namespace OnlineRecipeSharing.Services
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "TasteShare";
        public bool EnableSsl { get; set; } = true;
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string username, string resetLink)
        {
            var subject = "Password Reset Request - TasteShare";
            var htmlBody = CreatePasswordResetHtml(username, resetLink);
            var textBody = CreatePasswordResetText(username, resetLink);

            await SendEmailAsync(toEmail, subject, htmlBody, textBody);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string username)
        {
            var subject = "Welcome to TasteShare!";
            var htmlBody = CreateWelcomeHtml(username);
            var textBody = CreateWelcomeText(username);

            await SendEmailAsync(toEmail, subject, htmlBody, textBody);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlContent, string textContent = null)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
                message.To.Add(new MailboxAddress(toEmail, toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlContent,
                    TextBody = textContent ?? ConvertHtmlToText(htmlContent)
                };

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new MailKit.Net.Smtp.SmtpClient();

                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort,
                    _emailSettings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

                if (!string.IsNullOrEmpty(_emailSettings.SmtpUsername) &&
                    !string.IsNullOrEmpty(_emailSettings.SmtpPassword))
                {
                    await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {Email}", toEmail);
                throw new EmailException($"Failed to send email to {toEmail}", ex);
            }
        }

        private string CreatePasswordResetHtml(string username, string resetLink)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #28a745, #20c997); padding: 20px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
                        .content {{ padding: 30px; background: #f9f9f9; border-radius: 0 0 10px 10px; }}
                        .button {{ display: inline-block; padding: 12px 24px; background-color: #28a745; color: white; text-decoration: none; border-radius: 5px; font-weight: bold; }}
                        .expiry-note {{ color: #666; font-size: 0.9em; margin-top: 20px; }}
                        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee; color: #777; font-size: 0.9em; text-align: center; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>TasteShare</h1>
                            <p>Password Reset Request</p>
                        </div>
                        <div class='content'>
                            <p>Hello {System.Net.WebUtility.HtmlEncode(username)},</p>
                            <p>We received a request to reset your password for your TasteShare account.</p>
                            <p>Click the button below to reset your password:</p>
                            <p style='text-align: center; margin: 30px 0;'>
                                <a href='{System.Net.WebUtility.HtmlEncode(resetLink)}' class='button'>Reset Your Password</a>
                            </p>
                            <p class='expiry-note'>
                                <strong>Note:</strong> This password reset link will expire in 1 hour.
                                If you didn't request this reset, please ignore this email.
                            </p>
                            <div class='footer'>
                                <p>TasteShare Recipe Sharing Platform</p>
                                <p>If you're having trouble clicking the button, copy and paste this URL:</p>
                                <p style='word-break: break-all;'>{System.Net.WebUtility.HtmlEncode(resetLink)}</p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string CreatePasswordResetText(string username, string resetLink)
        {
            return $@"
                PASSWORD RESET REQUEST - TASTESHARE

                Hello {username},

                We received a request to reset your password for your TasteShare account.

                Please use the following link to reset your password:
                {resetLink}

                This link will expire in 1 hour.

                If you didn't request this password reset, please ignore this email.

                Best regards,
                The TasteShare Team";
        }

        private string CreateWelcomeHtml(string username)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #28a745, #20c997); padding: 20px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
                        .content {{ padding: 30px; background: #f9f9f9; border-radius: 0 0 10px 10px; }}
                        .welcome {{ font-size: 24px; color: #28a745; margin-bottom: 20px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>Welcome to TasteShare!</h1>
                        </div>
                        <div class='content'>
                            <div class='welcome'>Hello {System.Net.WebUtility.HtmlEncode(username)}!</div>
                            <p>Thank you for joining TasteShare, your new favorite recipe sharing platform!</p>
                            <p>Get started by:</p>
                            <ul>
                                <li>📖 Browsing delicious recipes</li>
                                <li>📝 Sharing your own creations</li>
                                <li>💬 Connecting with food lovers</li>
                            </ul>
                            <p>Happy cooking! 🍳</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string CreateWelcomeText(string username)
        {
            return $@"
                WELCOME TO TASTESHARE!

                Hello {username}!

                Thank you for joining TasteShare, your new favorite recipe sharing platform!

                Get started by:
                - Browsing delicious recipes
                - Sharing your own creations
                - Connecting with food lovers

                Happy cooking!

                The TasteShare Team";
        }

        private string ConvertHtmlToText(string html)
        {
            return System.Net.WebUtility.HtmlDecode(html
                .Replace("<br>", "\n")
                .Replace("<br/>", "\n")
                .Replace("<br />", "\n")
                .Replace("<p>", "\n")
                .Replace("</p>", "\n")
                .Replace("<div>", "\n")
                .Replace("</div>", "\n")
                .Replace("<li>", "• ")
                .Replace("</li>", "\n")
                .Replace("<ul>", "\n")
                .Replace("</ul>", "\n")
                .Replace("<ol>", "\n")
                .Replace("</ol>", "\n")
                .Replace("<strong>", "")
                .Replace("</strong>", "")
                .Replace("<b>", "")
                .Replace("</b>", "")
                .Replace("<em>", "")
                .Replace("</em>", "")
                .Replace("<i>", "")
                .Replace("</i>", "")
                .Replace("<h1>", "\n")
                .Replace("</h1>", "\n")
                .Replace("<h2>", "\n")
                .Replace("</h2>", "\n")
                .Replace("<h3>", "\n")
                .Replace("</h3>", "\n")
                .Replace("<h4>", "\n")
                .Replace("</h4>", "\n")
                .Replace("<h5>", "\n")
                .Replace("</h5>", "\n")
                .Replace("<h6>", "\n")
                .Replace("</h6>", "\n")
                .Replace("&nbsp;", " ")
                .Replace("  ", " ")
                .Replace("\n\n", "\n")
                .Replace("\n\n", "\n")
                .Trim());
        }
    }
}