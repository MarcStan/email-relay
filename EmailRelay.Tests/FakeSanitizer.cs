using EmailRelay.Logic;
using EmailRelay.Logic.Models;

namespace EmailRelay.Tests
{
    public class FakeSanitizer : IMetadataSanitizer
    {
        public bool CanSanitizeContentFrom(string email)
            => true;

        public bool TrySanitizeHtml(ref string content, SubjectModel subject, string relayTargetEmail, string to)
            => true;

        public bool TrySanitizePlainText(ref string content, SubjectModel subject, string relayTargetEmail, string to)
            => true;
    }
}
