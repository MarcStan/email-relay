namespace EmailRelay.Logic.Models
{
    /// <summary>
    /// For relaying subject with special format "...Relay for user@example.com: Actual subject" may be parsed with this model.
    /// </summary>
    public class SubjectModel
    {
        /// <summary>
        /// The prefix before the Relay for.. message.
        /// The usual "Re: Re: Fwd:" spam if any.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// The actual email for whom the message was relayed (or is to be relayed).
        /// If empty then no "Relay for.." could be parsed from the email header.
        /// </summary>
        public string RelayTarget { get; set; }

        /// <summary>
        /// The actual subject
        /// </summary>
        public string Subject { get; set; }
    }
}
