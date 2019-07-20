using SendGrid.Helpers.Mail;
using System;
using System.Linq;

namespace EmailRelay.Logic
{
    public static class EmailAddressParser
    {
        public static EmailAddress[] ParseEmailAddresses(string rawEmailAddresses)
        {
            if (string.IsNullOrEmpty(rawEmailAddresses))
                return Array.Empty<EmailAddress>();

            // format:
            // "name" <foo@example.com>, "bar" <bar@example.com"
            // but can't just split on , due to this:
            // "first, last" <foo@example.com>, "bar" <bar@example.com"
            // also raw emails are a possiblity too so <> are not always there
            // foo@example.com, "me" <me@example.com>, ..

            var emails = rawEmailAddresses
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            // fixup any splits that happended inside the quoted names
            int? nameStartTagIndex = null;
            for (int i = 0; i < emails.Count; i++)
            {
                // always has to have two quotes for a valid name
                // only has one if name contained , -> remember start and then merge to end
                if (emails[i].Count(c => c == '"') == 1)
                {
                    if (nameStartTagIndex.HasValue)
                    {
                        // found endtag, reappend all previous (may be more than one)
                        emails[nameStartTagIndex.Value] = string.Join(",", Enumerable.Range(nameStartTagIndex.Value, i - nameStartTagIndex.Value + 1).Select(_ => emails[_]));
                        emails.RemoveRange(nameStartTagIndex.Value + 1, i - nameStartTagIndex.Value);
                        i -= i - nameStartTagIndex.Value;
                        nameStartTagIndex = null;
                    }
                    else
                    {
                        nameStartTagIndex = i;
                    }
                }
            }
            return emails
                .Select(ParseEmailAddress)
                .Where(address => address != null)
                .ToArray();
        }

        /// <summary>
        /// Attempts to parse an email of the format: "name" &lt;foo@example.com%gt;
        /// Also supports plain email addresses directly
        /// </summary>
        /// <param name="rawEmailAddresses"></param>
        public static EmailAddress ParseEmailAddress(string rawEmailAddress)
        {
            if (string.IsNullOrEmpty(rawEmailAddress))
                return null;

            var parts = rawEmailAddress.Split(new[] { '<', '>' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return null;

            var email = parts.Length == 2 ? parts[1].Trim() : parts[0].Trim();
            var name = parts.Length == 2 ? parts[0].Replace("\"", string.Empty).Trim() : string.Empty;
            return new EmailAddress { Email = email, Name = name };
        }
    }
}
