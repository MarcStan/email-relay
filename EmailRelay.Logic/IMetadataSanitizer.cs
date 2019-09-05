using EmailRelay.Logic.Models;

namespace EmailRelay.Logic
{
    /// <summary>
    /// when replying from private account the email and relay prefix may be in the body since many email clients copy the header into the reply in plain text.
    /// The receiver will then see the relay target email.
    /// </summary>
    /// <example>
    /// external@user.foo sends email to me@mydomain.com
    /// -> this relay then sends email from me@mydomain.com to me@myprivateemail.com
    /// if I respond then email clients usually copy the metadata into the response
    /// this might look like "me@mydomain.com sent email to you <me@privateemail.com> at %date%"
    /// -> private information that the external users shouldn't see
    /// Instead sanitize it to show e.g. "external@user.foo sent email to you <me@mydomain.com> at %data%"
    /// </example>
    public interface IMetadataSanitizer
    {
        /// <summary>
        /// Returns true if this instance is able to sanitize content of the specific email provider.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        bool CanSanitizeContentFrom(string email);

        bool TrySanitizePlainText(ref string content, SubjectModel subject, string relayTargetEmail, string to);

        bool TrySanitizeHtml(ref string content, SubjectModel subject, string relayTargetEmail, string to);
    }
}
