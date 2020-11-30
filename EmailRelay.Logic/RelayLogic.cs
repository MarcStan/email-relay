using EmailRelay.Logic.Models;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Errors.Model;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EmailRelay.Logic
{
    public class RelayLogic
    {
        private readonly ISendGridClient _client;
        private readonly ILogger _log;
        private readonly SubjectParser _subjectParser;
        private readonly IEnumerable<IMetadataSanitizer> _sanitizers;

        public RelayLogic(
            ISendGridClient client,
            SubjectParser subjectParser,
            ILogger log,
            IEnumerable<IMetadataSanitizer> sanitizers)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _subjectParser = subjectParser ?? throw new ArgumentNullException(nameof(subjectParser));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _sanitizers = sanitizers ?? throw new ArgumentNullException(nameof(sanitizers));
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
        public async Task RelayAsync(Email email, string relayTargetEmail, string domain, bool canSendAsDomain, CancellationToken cancellationToken)
        {
            // @ required or else "example.com" would match @foo.example.com" as well
            if (!domain.StartsWith("@"))
                domain = "@" + domain;

            var from = email.From.Email;

            // only supports one recipient right now
            // find the first @domain email as we'll be sending in its name
            // important to match by domain as user could CC any number of others and put domain not-first
            var recipients = email.To.Concat(email.Cc).Select(e => e.Email).Distinct();
            // use fallback incase email was BCC'ed to us
            var to = recipients.FirstOrDefault(_ => _.EndsWith(domain, StringComparison.OrdinalIgnoreCase)) ?? "unknown" + domain;

            var subject = _subjectParser.Parse(email.Subject);
            // check if subject contains required prefix "Relay for email"
            // if not, then the relay target will be null
            if (string.IsNullOrEmpty(subject.RelayTarget))
            {
                // regular email by external user -> relay to target
                await SendEmailAsync(to, relayTargetEmail,
                    $"{subject.Prefix}{_subjectParser.Prefix} {from}: {subject.Subject}",
                    email.Html ?? email.Text,
                    email.Attachments,
                    cancellationToken);
                return;
            }

            // sending as domain since we have a relaytarget
            if (!canSendAsDomain)
            {
                await SendEmailAsync(to, relayTargetEmail, $"[WARNING] {email.Subject}",
                    "Sending as domain is disabled! Use appSetting 'SendAsDomain: true' to enable it. Original message:<br/>"
                    + email.Html ?? email.Text,
                    email.Attachments,
                    cancellationToken);
                return;
            }

            // safety check, we don't want external senders to be able to spoof owner & send as the domain
            var auth = IsAuthorizedSender(email, relayTargetEmail);
            if (auth != RelayAuthResult.Authorized)
            {
                // possibly external user tried to send email. log and abort
                _log.LogCritical($"Unauthorized sender {from} tried to send email in the name of the domain via subject: {email.Subject}. Auth result was {auth} (SPF: {email.Spf}, DKIM: {email.Dkim})");
                // relay to target with warning
                await SendEmailAsync(to, relayTargetEmail, $"[SPOOFWARNING] {email.Subject}",
                    $"Someone tried to send an email in the name of the domain by using the '{_subjectParser.Prefix} {to}' subject. Their email was: {from}. <br />" +
                    $"Auth result was {auth} (SPF: {email.Spf}, DKIM: {email.Dkim}). Original message below.<br /><br />" +
                    $"{email.Html ?? email.Text}",
                    email.Attachments,
                    cancellationToken);
                return;
            }

            // sanitize email content
            var sanitizer = _sanitizers.FirstOrDefault(s => s.CanSanitizeContentFrom(relayTargetEmail));
            if (sanitizer == null)
            {
                _log.LogCritical($"Unable to sanitize content from email. Could not find matching sanitizer for domain {relayTargetEmail}");
                // respond to owner
                await SendEmailAsync(to, relayTargetEmail, $"[SANITIZE] {email.Subject}",
                    "Failed to sanitize the email content (and did not send it to the target). <br />" +
                    $"Could not find a sanitizer for domain {relayTargetEmail}. Original content:<br /><br />" +
                    email.Html ?? email.Text,
                    email.Attachments,
                    cancellationToken);
                return;
            }

            string content;
            // prefer html over plaintext
            if (!string.IsNullOrEmpty(email.Html))
            {
                content = email.Html;
                if (!sanitizer.TrySanitizeHtml(ref content, subject, relayTargetEmail, to))
                {
                    // possibly external user tried to send email. log and abort
                    _log.LogCritical("Unable to sanitize html content from email. Could not find block with private information. Assuming the format changed and did not send the email");
                    // respond to owner
                    await SendEmailAsync(to, relayTargetEmail, $"[SANITIZE] {email.Subject}",
                        "Failed to sanitize the email (html) content (and did not send it to the target). <br />" +
                        "Could not find the section with private information. Assuming the format changed. Original content below.<br /><br />" +
                        content,
                        email.Attachments,
                        cancellationToken);
                    return;
                }
            }
            else
            {
                content = email.Text;
                if (!sanitizer.TrySanitizePlainText(ref content, subject, relayTargetEmail, to))
                {
                    // possibly external user tried to send email. log and abort
                    _log.LogCritical("Unable to sanitize content from email. Could not find block with private information. Assuming the format changed and did not send the email");
                    // respond to owner
                    await SendEmailAsync(to, relayTargetEmail, $"[SANITIZE] {email.Subject}",
                        "Failed to sanitize the email (plain text) content (and did not send it to the target). <br />" +
                        "Could not find the section with private information. Assuming the format changed. Original content below.<br /><br />" +
                        content,
                        email.Attachments,
                        cancellationToken);
                    return;
                }
            }

            // send in name of the domain
            await SendEmailAsync(to, subject.RelayTarget, subject.Prefix + subject.Subject, content, email.Attachments, cancellationToken);
        }

        private RelayAuthResult IsAuthorizedSender(Email email, string relayTargetEmail)
        {
            if (!email.From.Email.Equals(relayTargetEmail, StringComparison.OrdinalIgnoreCase))
                return RelayAuthResult.InvalidSender;

            // senders are easily faked, verify DKIM and SPF
            // luckily Sendgrid does the verification already, so it's a simply string compare
            if (!"pass".Equals(email.Spf, StringComparison.OrdinalIgnoreCase))
                return RelayAuthResult.SpfFail;

            var fromDomain = email.From.Email.Substring(email.From.Email.IndexOf('@'));
            var expected = $"{{{fromDomain} : pass}}";
            if (!expected.Equals(email.Dkim, StringComparison.OrdinalIgnoreCase))
                return RelayAuthResult.DkimFail;

            return RelayAuthResult.Authorized;
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
            var response = await _client.SendEmailAsync(mail, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                var errorResponse = await response.Body.ReadAsStringAsync();
                throw new BadRequestException($"Sendgrid did not accept. The response was: {response.StatusCode}." + Environment.NewLine + errorResponse);
            }
        }
    }
}
