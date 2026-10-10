// Interface gửi email (Mailpit khi dev, Resend khi production)
namespace Comink.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng gửi email. Cài đặt dùng SMTP (dev: Mailpit, prod: Resend).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thông tin một email cần gửi.
/// </summary>
public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null);
