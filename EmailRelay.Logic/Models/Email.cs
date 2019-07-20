using SendGrid.Helpers.Mail;
using System.Collections.Generic;
using System.Text;

namespace EmailRelay.Logic.Models
{
    /// <summary>
    /// Sendgrid parameters as per https://sendgrid.com/docs/for-developers/parsing-email/setting-up-the-inbound-parse-webhook/#default-parameters
    /// </summary>
    public class Email
    {
        public EmailAddress From { get; set; }

        public EmailAddress[] To { get; set; } = new EmailAddress[0];

        public EmailAddress[] Cc { get; set; } = new EmailAddress[0];

        public string Subject { get; set; }

        public string Html { get; set; }

        public string Text { get; set; }

        public string SenderIp { get; set; }

        public string SpamReport { get; set; }

        public string SpamScore { get; set; }

        public EmailAttachment[] Attachments { get; set; } = new EmailAttachment[0];

        /// <summary>
        /// https://sendgrid.com/docs/for-developers/parsing-email/inbound-email/#character-sets-and-header-decoding
        /// </summary>
        public Dictionary<string, string> Charsets { get; set; }

        public Encoding GetFromCharSet(string id)
            => Encoding.GetEncoding(Charsets[id]);
    }
}
