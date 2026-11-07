// Services/IEmailService.cs
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace OnlineRecipeSharing.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string username, string resetLink);
        Task SendWelcomeEmailAsync(string toEmail, string username); // Optional: Add welcome email
        Task SendEmailAsync(string toEmail, string subject, string htmlContent, string textContent = null);
    }
}
