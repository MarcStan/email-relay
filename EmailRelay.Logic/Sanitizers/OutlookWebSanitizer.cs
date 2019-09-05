using EmailRelay.Logic.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmailRelay.Logic.Sanitizers
{
    /// <summary>
    /// Sanitizer for emails sent with the outlook web interface
    /// </summary>
    public class OutlookWebSanitizer : IMetadataSanitizer
    {
        private readonly SubjectParser _subjectParser;

        public OutlookWebSanitizer(SubjectParser subjectParser)
        {
            _subjectParser = subjectParser ?? throw new ArgumentNullException(nameof(subjectParser));
        }


        private string[] _supportedDomains => new[]
        {
            "@live.com",
            "@outlook.com",
            "@hotmail.com"
        };

        public bool CanSanitizeContentFrom(string email)
        {
            return _supportedDomains.Any(d => email.EndsWith(d, StringComparison.OrdinalIgnoreCase));
        }

        public bool TrySanitizePlainText(ref string content, SubjectModel subject, string relayTargetEmail, string to)
        {
            // when replying from private account the email and relay prefix will be in the body
            // since many email clients copy the body into the reply in plain text
            // the receiver will see the relay target email

            // e.g.
            // 1. sender@example.com sends email to marc@marcstan.net
            // 2. email is redirected as "from marc@marcstan.net" to marcstan@live.com
            // 3. reply is sent from marcstan@live.com to marc@marcstan.net
            // 4. (after sanity check) server sends out the email from marc@marcstan.net to sender@example.com
            // sent email would the contain the body below:

            /*
    This is my response

    ___________________________________________
    From: marc@marcstan.net <marc@marcstan.net>
    Sent: Tuesday, September 3, 2019 11:19:42 PM
    To: marcstan@live.com <marcstan@live.com>
    Subject: Email Relay: sender@example.com: Test

    This is the original message from someone

            */

            // note that
            // A) it contains marcstan@live.com (which sender@example.com would not expect)
            // B) it contains "Email Relay: " prefix in subject line inside the email which sender would not expect (actual subject will not contain it)

            // solution:
            // find the From: Sent: To: Subject: block and string replace
            /*
             * Regex based on the body above
             */
            // email format "email <email>" works because sendgrid has no concept of contacts so it can't resolve those to actual names - ever
            var regex = new Regex(
                $"From:.*?(?<from>{to} <{to}>|{to})\r?\nSent: .*\r?\n" +
                $"To: (?<to>{relayTargetEmail} <{relayTargetEmail}>|{relayTargetEmail})\r?\n" +
                $"Subject: {subject.Prefix}(?<subject>{_subjectParser.Prefix}\\s?{subject.RelayTarget}:\\s)", RegexOptions.Multiline);
            var match = regex.Match(content);
            if (!match.Success)
            {
                // two options
                // 1. first email and contains no metadata
                // 2. format changed

                // mail contains metadata, abort
                if (content.Contains("From:") &&
                    content.Contains("Sent:") &&
                    content.Contains("To:") &&
                    content.Contains("Subject:"))
                    return false;
                // no metadata, assume initial email
            }
            // replace all private information with the expected
            var fromGroup = match.Groups["from"];
            var toGroup = match.Groups["to"];
            var subjectGroup = match.Groups["subject"];

            var orderedByIndex = new[]
            {
                new { group = fromGroup, replacement = $"{subject.RelayTarget} <{subject.RelayTarget}>" },
                new { group = toGroup, replacement = $"{to} <{to}>" },
                new { group = subjectGroup, replacement = "" }
            }.OrderByDescending(x => x.group.Index);
            // reverse order by index to keep correct offset for earlier groups
            foreach (var item in orderedByIndex)
            {
                content =
                    content.Substring(0, item.group.Index) + item.replacement +
                    content.Substring(item.group.Index + item.group.Length);
            }
            return true;
        }

        public bool TrySanitizeHtml(ref string content, SubjectModel subject, string relayTargetEmail, string to)
        {
            // same concept as plain text just a tiny bit bigger regex
            // based on this block:

            /*
<div id=\"divRplyFwdMsg\" dir=\"ltr\"><font face=\"Calibri, sans-serif\" style=\"font-size:11pt\" color=\"#000000\"><b>From:</b> me@mydomain.com &lt;me@mydomain.com&gt;<br>
<b>Sent:</b> 01 September 2019 10:10<br>
<b>To:</b> me@myprivateemail.com &lt;me@myprivateemail.com&gt;<br>
<b>Subject:</b> Relay for ext@user.foo: Subject1</font>
             */

            var regex = new Regex(
                $"From:.*?(?<from>{to} &lt;{to}&gt;|{to}).*?\n.*?Sent:.*\r?\n" +
                $".*?To:.*?(?<to>{relayTargetEmail} &lt;{relayTargetEmail}&gt;|{relayTargetEmail}).*\r?\n" +
                $".*?Subject:.*?{subject.Prefix}(?<subject>{_subjectParser.Prefix}\\s?{subject.RelayTarget}).*?:\\s+.*"
                , RegexOptions.Multiline);
            var match = regex.Match(content);
            if (!match.Success)
            {
                // two options
                // 1. first email and contains no metadata
                // 2. format changed

                // mail contains metadata, abort
                if (content.Contains("From:") &&
                    content.Contains("Sent:") &&
                    content.Contains("To:") &&
                    content.Contains("Subject:"))
                    return false;
                // no metadata, assume initial email
            }
            // replace all private information with the expected
            var fromGroup = match.Groups["from"];
            var toGroup = match.Groups["to"];
            var subjectGroup = match.Groups["subject"];

            var orderedByIndex = new[]
            {
                new { group = fromGroup, replacement = $"{subject.RelayTarget} <{subject.RelayTarget}>" },
                new { group = toGroup, replacement = $"{to} <{to}>" },
                new { group = subjectGroup, replacement = "" }
            }.OrderByDescending(x => x.group.Index);
            // reverse order by index to keep correct offset for earlier groups
            foreach (var item in orderedByIndex)
            {
                content =
                    content.Substring(0, item.group.Index) + item.replacement +
                    content.Substring(item.group.Index + item.group.Length);
            }
            return true;
        }
    }
}
