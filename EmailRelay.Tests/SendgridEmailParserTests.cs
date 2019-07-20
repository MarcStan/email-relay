using EmailRelay.Logic;
using EmailRelay.Logic.Models;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EmailRelay.Tests
{
    public class SendgridEmailParserTests
    {
        [Test]
        public void ParsingSimpleFormShouldWork()
        {
            var parser = new SendgridEmailParser();

            using (var form = Build(new Dictionary<string, object>
            {
                {"from", "from@example.com" },
                {"to", "to@example.com" },
                {"subject", "subj" },
                {"html", "text" }
            }))
            {
                var result = parser.Parse(form);
                result.From.Email.Should().Be("from@example.com");
                result.To[0].Email.Should().Be("to@example.com");
                result.Subject.Should().Be("subj");
                result.Html.Should().Be("text");
            }
        }

        [Test]
        public void ParsingDisplayNamesShouldWork()
        {
            var parser = new SendgridEmailParser();

            using (var form = Build(new Dictionary<string, object>
            {
                {"from", "from <from@example.com>" },
                {"to", "no name <to@example.com>" },
                {"subject", "subj" },
                {"html", "text" }
            }))
            {
                var result = parser.Parse(form);
                result.From.Email.Should().Be("from@example.com");
                result.To[0].Email.Should().Be("to@example.com");
                result.Subject.Should().Be("subj");
                result.Html.Should().Be("text");
            }
        }

        [Test]
        public void HtmlShouldBePreferredOverText()
        {
            var parser = new SendgridEmailParser();

            const string actualContent = "Only this should be submitted";
            var data = new Dictionary<string, object>
            {
                {"from", "from <from@example.com>" },
                {"to", "no name <to@example.com>" },
                {"subject", "subj" },
                {"text", "other content" },
                {"html", @"<html>
<head>
<meta http-equiv=""Content-Type"" content=""text/html; charset=iso-8859-1"">
<style type=""text/css"" style=""display:none;""> P {margin-top:0;margin-bottom:0;} </style>
</head>
<body dir=""ltr"">
<div style=""font-family:Calibri,Helvetica,sans-serif; font-size:12pt; color:rgb(0,0,0)"">" +
actualContent +
@"<br>
</div>
<div style=""font-family:Calibri,Helvetica,sans-serif; font-size:12pt; color:rgb(0,0,0)"">
</div>
<div style=""font-family:Calibri,Helvetica,sans-serif; font-size:12pt; color:rgb(0,0,0)"">
<br>
</div>
<div id=""Signature"">
<div id=""divtagdefaultwrapper"" style=""font-size:12pt; color:#000000; font-family:Calibri,Arial,Helvetica,sans-serif"">
Regards,
<div>Sender</div>
</div>
</div>
</body>
</html>" }
            };
            using (var form = Build(data))
            {
                var result = parser.Parse(form);
                result.From.Email.Should().Be("from@example.com");
                result.To[0].Email.Should().Be("to@example.com");
                result.Subject.Should().Be("subj");
                result.Html.Should().Be((string)data["html"]);
            }
        }

        [Test]
        public void TextShouldBeFallbackIfHtmlFails()
        {
            var parser = new SendgridEmailParser();

            using (var form = Build(new Dictionary<string, object>
            {
                {"from", "from <from@example.com>" },
                {"to", "no name <to@example.com>" },
                {"subject", "subj" },
                {"text", "other content" },
                {"html", "" }
            }))
            {
                var result = parser.Parse(form);
                result.From.Email.Should().Be("from@example.com");
                result.To[0].Email.Should().Be("to@example.com");
                result.Subject.Should().Be("subj");
                result.Html.Should().Be("");
                result.Text.Should().Be("other content");
            }
        }

        [Test]
        public void AttachmentsShouldBeCorrectlyParsed()
        {
            var parser = new SendgridEmailParser();

            using (var form = Build(new Dictionary<string, object>
            {
                {"from", "from <from@example.com>" },
                {"to", "no name <to@example.com>" },
                {"subject", "subj" },
                {"html", "other content" },
                {"attachment-info", JsonConvert.SerializeObject(new
                    {
                        attachment1 = new
                        {
                            filename = "en.txt",
                            type = "text/plain"
                        },
                        attachment2 = new
                        {
                            filename = "img.jpg",
                            type = "image/jpeg"
                        }
                    })
                },
                {"attachment1", new EmailAttachment
                {
                    FileName = "en.txt",
                    ContentType = "text/plain",
                    Base64Data = "This is the actual content"
                } },
                {"attachment2", new EmailAttachment
                {
                    FileName = "img.jpg",
                    ContentType = "image/jpeg",
                    Base64Data = "large base64 payload"
                } }
            }))
            {
                var result = parser.Parse(form);
                result.From.Email.Should().Be("from@example.com");
                result.To[0].Email.Should().Be("to@example.com");
                result.Subject.Should().Be("subj");
                result.Html.Should().Be("other content");
                result.Attachments.Should().HaveCount(2);
                result.Attachments[0].ContentType.Should().Be("text/plain");
                result.Attachments[0].FileName.Should().Be("en.txt");
                result.Attachments[0].Base64Data.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("This is the actual content")));
                result.Attachments[1].ContentType.Should().Be("image/jpeg");
                result.Attachments[1].FileName.Should().Be("img.jpg");
                result.Attachments[1].Base64Data.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("large base64 payload")));
            }
        }

        [Test]
        public void AttachmentsShouldBeCorrectlyParsedFromActualPayload()
        {
            var parser = new SendgridEmailParser();

            using (var form = new MemoryStream())
            {
                var content = File.ReadAllText("Data/multipart.txt");
                var writer = new StreamWriter(form);
                writer.Write(content);
                writer.Flush();
                form.Position = 0;
                var result = parser.Parse(form);
                result.From.Email.Should().Be("marcstan@live.com");
                result.From.Name.Should().Be("Marc Stan");
                result.To[0].Email.Should().Be("marc@marcstan.net");
                result.To[0].Name.Should().Be("marc@marcstan.net");
                result.Subject.Should().Be("Subject1");
                result.Text.Should().Be("Content1");
                result.Attachments.Should().HaveCount(1);
                result.Attachments[0].ContentType.Should().Be("text/plain");
                result.Attachments[0].FileName.Should().Be("en.txt");
                result.Attachments[0].Base64Data.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("this is plaintext")));
            }
        }

        [Test]
        public void RawHtmlShouldBeFallbackIfHtmlParsingFailsAndTextMissing()
        {
            var parser = new SendgridEmailParser();

            const string actualContent = "Only this should be submitted";
            var rawHtml = @"<html>
<head>
<meta http-equiv=""Content-Type"" content=""text/html; charset=iso-8859-1"">
<style type=""text/css"" style=""display:none;""> P {margin-top:0;margin-bottom:0;} </style>
</head>
<bodyinvalid dir=""ltr"">
<div style=""font-family:Calibri,Helvetica,sans-serif; font-size:12pt; color:rgb(0,0,0)"">" +
actualContent +
@"<br>
</div>
<div style=""font-family:Calibri,Helvetica,sans-serif; font-size:12pt; color:rgb(0,0,0)"">
</div>
<div style=""font-family:Calibri,Helvetica,sans-serif; font-size:12pt; color:rgb(0,0,0)"">
<br>
</div>
<div id=""Signature"">
<div id=""divtagdefaultwrapper"" style=""font-size:12pt; color:#000000; font-family:Calibri,Arial,Helvetica,sans-serif"">
Regards,
<div>Sender</div>
</div>
</div>
</body>
</html>";
            using (var form = Build(new Dictionary<string, object>
            {
                {"from", "from <from@example.com>" },
                {"to", "no name <to@example.com>" },
                {"subject", "subj" },
                {"html", rawHtml }
            }))
            {
                var result = parser.Parse(form);
                result.From.Email.Should().Be("from@example.com");
                result.To[0].Email.Should().Be("to@example.com");
                result.Subject.Should().Be("subj");
                result.Html.Should().Be(rawHtml);
            }
        }

        /// <summary>
        /// Helper to create multipart formdata from a dictionary.
        /// Value must be string for all entries except for <see cref="EmailAttachment"/> for attachments.
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        private MemoryStream Build(Dictionary<string, object> dictionary)
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms, Encoding.UTF8);
            const string boundary = "--xYzZY";
            foreach (var p in dictionary)
            {
                writer.WriteLine(boundary);
                writer.Write($"Content-Disposition: form-data; name=\"{p.Key}\"");
                if (p.Value is string value)
                {
                    writer.WriteLine();
                    writer.WriteLine();
                    writer.WriteLine(value);
                }
                else if (p.Value is EmailAttachment attachment)
                {
                    writer.WriteLine($"; filename=\"{attachment.FileName}\"");
                    writer.WriteLine($"Content-Type: {attachment.ContentType}");
                    writer.WriteLine();
                    writer.WriteLine(attachment.Base64Data);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported type {p.Value.GetType()} as dictionary value");
                }
            }
            writer.WriteLine($"{boundary}--");
            writer.Flush();
            ms.Position = 0;
            return ms;
        }
    }
}
