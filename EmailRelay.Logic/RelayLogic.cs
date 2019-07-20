using EmailRelay.Logic.Models;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmailRelay.Logic
{
    public class RelayLogic
    {
        private readonly ISendGridClient _client;
        private readonly ILogger _log;

        public RelayLogic(
            ISendGridClient client,
            ILogger log)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Requires the configuration of a target email.
        /// If provided emails can be relayed in two modes:
        /// 1. external user sends email to domain -> will be forwarded by the domain email with subject "Relay for external user"
        /// 2. you send an email (must be from configured target email) to the domain with subject "Relay for email" -> will create a new email from the domain to "email" and send it in the name of the domain
        /// </summary>
        /// <param name="email"></param>
        /// <param name="relayTargetEmail">The email that is configured as owner of the domain. Only he may send emails in the name of the domain.</param>
        /// <param name="domain"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task RelayAsync(Email email, string relayTargetEmail, string domain, CancellationToken cancellationToken)
        {
            if (!domain.StartsWith("@"))
                domain = "@" + domain;
            // only supports one recipient right now
            var recipients = email.To.Concat(email.Cc).Select(e => e.Email).Distinct();
            var from = email.From.Email;
            // find the first @domain email as we'll be sending in its name
            // important to match by domain as user could CC any number of others and put domain not-first
            var to = recipients.FirstOrDefault(_ => _.EndsWith(domain, StringComparison.OrdinalIgnoreCase)) ?? throw new NotSupportedException($"Unable to process email without at least one {domain} entry");

            var subject = new SubjectParser().Parse(email.Subject);
            // check if subject contains "Relay for email"
            if (!string.IsNullOrEmpty(subject.RelayTarget))
            {
                // safety check, we don't want external senders to be able to send as the domain
                if (!from.Equals(relayTargetEmail, StringComparison.OrdinalIgnoreCase))
                {
                    // possibly external user tried to send email log and abort
                    _log.LogCritical($"Unauthorized sender {email.From} tried to send email in the name of the domain via subject: {email.Subject}");
                    // relay to target with warning
                    await SendEmailAsync(to, relayTargetEmail, $"[WARNING] {subject.Prefix}Relay for {from}: {subject.Subject}",
                        $"Someone tried to send an email in the name of the domain by using the 'Relay for {to}' subject. Their email was: {from}. Original message below.<br /><br />{email.Html ?? email.Text}", email.Attachments, cancellationToken);
                    return;
                }
                // send in name of the domain
                await SendEmailAsync(to, subject.RelayTarget, subject.Prefix + subject.Subject, email.Html ?? email.Text, email.Attachments, cancellationToken);
            }
            else
            {
                // regular email by external user -> relay to target
                await SendEmailAsync(to, relayTargetEmail, $"{subject.Prefix}Relay for {from}: {subject.Subject}", email.Html ?? email.Text, email.Attachments, cancellationToken);
            }
        }

        private async Task SendEmailAsync(string from, string to, string subject, string content, EmailAttachment[] attachments, CancellationToken cancellationToken)
        {
            var mail = MailHelper.CreateSingleEmail(new EmailAddress(from), new EmailAddress(to), subject, null, content);
            foreach (var attachment in attachments)
            {
                mail.AddAttachment(new Attachment
                {
                    ContentId = attachment.ContentId,
                    Content = attachment.Base64Data,
                    Filename = attachment.FileName,
                    Type = attachment.ContentType
                });
            }
            await _client.SendEmailAsync(mail, cancellationToken);
        }
    }
}
