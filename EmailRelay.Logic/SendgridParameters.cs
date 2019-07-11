namespace EmailRelay.Logic
{
    public class SendgridParameters
    {
        public string From { get; set; }

        public string To { get; set; }

        public string Subject { get; set; }

        public string Content { get; set; }
    }
}
