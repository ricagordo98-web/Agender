using Agender.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Agender.Web.Auth;

public class NoOpEmailSender : IEmailSender<AgenderUser>
{
    public Task SendConfirmationLinkAsync(AgenderUser user, string email, string confirmationLink) => Task.CompletedTask;

    public Task SendPasswordResetLinkAsync(AgenderUser user, string email, string resetLink) => Task.CompletedTask;

    public Task SendPasswordResetCodeAsync(AgenderUser user, string email, string resetCode) => Task.CompletedTask;
}
