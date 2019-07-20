using EmailRelay.Logic.Extensions;
using EmailRelay.Logic.Models;
using HttpMultipartParser;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EmailRelay.Logic
{
    public class SendgridEmailParser
    {
        /// <summary>
        /// Parses a raw multipart form from the stream as a sendgrid email according to https://sendgrid.com/docs/for-developers/parsing-email/setting-up-the-inbound-parse-webhook/#default-parameters
        /// Adapted from https://github.com/KoditkarVedant/sendgrid-inbound
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        public Email Parse(MemoryStream body)
        {
            var parser = new MultipartFormDataParser(body, Encoding.UTF8);

            var charsets = JObject.Parse(parser.GetParameterValue("charsets", "{}"))
                .Properties()
                .ToDictionary(p => p.Name, p => Encoding.GetEncoding(p.Value.ToString()));

            // Create a dictionary of parsers, one parser for each desired encoding.
            // This is necessary because MultipartFormDataParser can only handle one
            // encoding and SendGrid can use different encodings for parameters such
            // as "from", "to", "text" and "html".
            var encodedParsers = charsets
                .Where(c => c.Value != Encoding.UTF8)
                .Select(c => c.Value)
                .Distinct()
                .Select(encoding =>
                {
                    body.Position = 0;
                    return new
                    {
                        Encoding = encoding,
                        Parser = new MultipartFormDataParser(body, encoding)
                    };
                })
                .Union(new[]
                {
                    new { Encoding = Encoding.UTF8, Parser = parser }
                })
                .ToDictionary(ep => ep.Encoding, ep => ep.Parser);

            // convert the raw formats so we can apply the correct encoders. the pre-parsed Sendgrid values (in Envelope) are forced to UTF-8
            var rawFrom = GetEncodedValue("from", charsets, encodedParsers, string.Empty);
            var from = EmailAddressParser.ParseEmailAddress(rawFrom);

            var rawTo = GetEncodedValue("to", charsets, encodedParsers, string.Empty);
            var to = EmailAddressParser.ParseEmailAddresses(rawTo);

            var rawCc = GetEncodedValue("cc", charsets, encodedParsers, string.Empty);
            var cc = EmailAddressParser.ParseEmailAddresses(rawCc);

            // will have attachment1...attachmentX properties depending on attachment count
            // conver to array
            var attachmentInfoAsJObject = JObject.Parse(parser.GetParameterValue("attachment-info", "{}"));
            var attachments = attachmentInfoAsJObject
                .Properties()
                .Select(prop =>
                {
                    var attachment = prop.Value.ToObject<EmailAttachment>();
                    attachment.Id = prop.Name;

                    var file = parser.Files.FirstOrDefault(f => f.Name == prop.Name);
                    if (file != null)
                    {
                        attachment.Base64Data = file.Data.ConvertToBase64();
                        if (string.IsNullOrEmpty(attachment.ContentType))
                            attachment.ContentType = file.ContentType;
                        if (string.IsNullOrEmpty(attachment.FileName))
                            attachment.FileName = file.FileName;
                    }

                    return attachment;
                }).ToArray();

            return new Email
            {
                // serializer friendly format
                Charsets = charsets.ToDictionary(p => p.Key, p => p.Value.WebName),
                From = from,
                To = to,
                Cc = cc,
                Subject = GetEncodedValue("subject", charsets, encodedParsers, null),
                Html = GetEncodedValue("html", charsets, encodedParsers, null),
                Text = GetEncodedValue("text", charsets, encodedParsers, null),
                Attachments = attachments,
                SenderIp = GetEncodedValue("sender_ip", charsets, encodedParsers, null),
                SpamReport = GetEncodedValue("spam_report", charsets, encodedParsers, null),
                SpamScore = GetEncodedValue("spam_score", charsets, encodedParsers, null)
            };
        }

        private static string GetEncodedValue(
            string parameterName,
            IEnumerable<KeyValuePair<string, Encoding>> charsets,
            IDictionary<Encoding, MultipartFormDataParser> encodedParsers,
            string defaultValue = null)
        {
            var encoding = charsets.FirstOrDefault(c => c.Key == parameterName).Value ?? Encoding.UTF8;
            var parser = encodedParsers[encoding];
            return parser.GetParameterValue(parameterName, defaultValue);
        }
    }
}
