using EmailRelay.Logic.Models;
using System.Text.RegularExpressions;

namespace EmailRelay.Logic
{
    public class SubjectParser
    {
        /// <summary>
        /// Subject parser
        /// </summary>
        /// <param name="prefix">The prefix to match. Defaults to "Relay for"</param>
        public SubjectParser(string prefix = null)
        {
            Prefix = prefix;
            if (string.IsNullOrEmpty(Prefix))
                Prefix = "Relay for";
        }

        public string Prefix { get; }

        public SubjectModel Parse(string subject)
        {
            // looking for "Relay for email"
            // may be preceeded by Fwd spam
            var regex = new Regex($"(.*?){Prefix} ([^:\\s]*?):(.*)");

            var match = regex.Match(subject);
            if (!match.Success)
                return new SubjectModel { Subject = subject };

            var prefix = match.Groups[1].Value;
            var relayTarget = match.Groups[2].Value;
            subject = match.Groups[3].Value.Trim();

            var m = Parse(subject);
            // remove any further Relay for.. parts recursively
            if (m.RelayTarget != null)
                subject = m.Prefix + m.Subject;

            return new SubjectModel
            {
                Prefix = prefix,
                RelayTarget = relayTarget,
                Subject = subject
            };
        }
    }
}
