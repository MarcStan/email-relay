using EmailRelay.Logic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;

namespace EmailRelay.Tests
{
    public class HttpFormDataParserTests
    {
        [Test]
        public void ParsingSimpleFormShouldWork()
        {
            var parser = new HttpFormDataParser();

            var form = Build(new Dictionary<string, string>
            {
                {"from", "from@example.com" },
                {"to", "to@example.com" },
                {"subject", "subj" },
                {"text", "text" }
            });
            var result = parser.Deserialize(form);
            result.From.Should().Be("from@example.com");
            result.To.Should().Be("to@example.com");
            result.Subject.Should().Be("subj");
            result.Content.Should().Be("text");
        }

        [Test]
        public void ParsingDisplayNamesShouldWork()
        {
            var parser = new HttpFormDataParser();

            var form = Build(new Dictionary<string, string>
            {
                {"from", "from <from@example.com>" },
                {"to", "no name <to@example.com>" },
                {"subject", "subj" },
                {"text", "text" }
            });
            var result = parser.Deserialize(form);
            result.From.Should().Be("from@example.com");
            result.To.Should().Be("to@example.com");
            result.Subject.Should().Be("subj");
            result.Content.Should().Be("text");
        }

        [Test]
        public void HtmlShouldBePreferredOverText()
        {
            var parser = new HttpFormDataParser();

            const string actualContent = "Only this should be submitted";
            var form = Build(new Dictionary<string, string>
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
            });
            var result = parser.Deserialize(form);
            result.From.Should().Be("from@example.com");
            result.To.Should().Be("to@example.com");
            result.Subject.Should().Be("subj");
            result.Content.Should().Be(form["html"]);
        }

        [Test]
        public void TextShouldBeFallbackIfHtmlFails()
        {
            var parser = new HttpFormDataParser();

            var form = Build(new Dictionary<string, string>
            {
                {"from", "from <from@example.com>" },
                {"to", "no name <to@example.com>" },
                {"subject", "subj" },
                {"text", "other content" },
                {"html", "" }
            });
            var result = parser.Deserialize(form);
            result.From.Should().Be("from@example.com");
            result.To.Should().Be("to@example.com");
            result.Subject.Should().Be("subj");
            result.Content.Should().Be("other content");
        }

        [Test]
        public void RawHtmlShouldBeFallbackIfHtmlParsingFailsAndTextMissing()
        {
            var parser = new HttpFormDataParser();

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
            var form = Build(new Dictionary<string, string>
            {
                {"from", "from <from@example.com>" },
                {"to", "no name <to@example.com>" },
                {"subject", "subj" },
                {"html", rawHtml }
            });
            var result = parser.Deserialize(form);
            result.From.Should().Be("from@example.com");
            result.To.Should().Be("to@example.com");
            result.Subject.Should().Be("subj");
            result.Content.Should().Be(rawHtml);
        }

        private IFormCollection Build(Dictionary<string, string> dictionary)
        {
            return new DictionaryBasedFormCollection(dictionary);
        }

        private class DictionaryBasedFormCollection : IFormCollection
        {
            private readonly Dictionary<string, string> _data;

            public DictionaryBasedFormCollection(Dictionary<string, string> data)
            {
                _data = data;
            }

            public StringValues this[string key] => ContainsKey(key) ? _data[key] : null;

            public int Count => _data.Count;

            public ICollection<string> Keys => _data.Keys;

            public IFormFileCollection Files => throw new NotImplementedException();

            public bool ContainsKey(string key)
                => _data.ContainsKey(key);

            public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            public bool TryGetValue(string key, out StringValues value)
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }
    }
}
