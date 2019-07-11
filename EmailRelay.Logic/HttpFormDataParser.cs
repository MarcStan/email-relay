using Microsoft.AspNetCore.Http;
using System;

namespace EmailRelay.Logic
{
    public class HttpFormDataParser
    {
        public SendgridParameters Deserialize(IFormCollection form)
        {
            var param = new SendgridParameters();
            param.From = ParseEmail(form["from"]);
            param.To = ParseEmail(form["to"]);
            param.Subject = form["subject"];
            param.Content = form["html"];
            if (string.IsNullOrWhiteSpace(param.Content))
                param.Content = form["text"];

            return param;
        }

        private string ParseEmail(string email)
        {
            if (!email.Contains("<"))
            {
                // may just be a regular email
                if (email.Contains("@"))
                    return email; // good enough

                throw new NotSupportedException($"Invalid input received for display name. Expected 'some name <email>' but found: {email}");
            }

            email = email.Substring(email.LastIndexOf('<') + 1);
            if (email.Contains(">"))
                email = email.Substring(0, email.IndexOf('>'));
            return email.Trim();
        }
    }
}
