
using EmailRelay.Logic;
using EmailRelay.Logic.Models;
using EmailRelay.Logic.Sanitizers;
using FluentAssertions;
using NUnit.Framework;
using System.IO;

namespace EmailRelay.Tests
{
    public class OutlookWebSanitizerTests
    {
        [Test]
        public void SanitizePlainTextShouldWork()
        {
            var parser = new SubjectParser();
            var sanitizer = new OutlookWebSanitizer(parser);

            var content = @"This is my response

___________________________________________
From: me@domain.com <me@domain.com>
Sent: Tuesday, September 3, 2019 11:19:42 PM
To: me@live.com <me@live.com>
Subject: RE: Relay for ext@user.foo: Test
 
This is the original message from someone";
            sanitizer.TrySanitizePlainText(ref content, new SubjectModel
            {
                Prefix = "RE: ",
                RelayTarget = "ext@user.foo",
                Subject = "Test"
            }, "me@live.com", "me@domain.com").Should().BeTrue();
        }

        [Test]
        public void SanitizePlainTextShouldFailIfNotMatched()
        {
            var parser = new SubjectParser();
            var sanitizer = new OutlookWebSanitizer(parser);

            var content = @"This is my response

___________________________________________
From: me@domain.com <me@domain.com>
expects all 4 in order, by adding some random stuff inbetween it should fail to match
Sent: Tuesday, September 3, 2019 11:19:42 PM
To: me@live.com <me@live.com>
Subject: RE: Relay for ext@user.foo: Test
 
This is the original message from someone";
            sanitizer.TrySanitizePlainText(ref content, new SubjectModel
            {
                Prefix = "RE: ",
                RelayTarget = "ext@user.foo",
                Subject = "Test"
            }, "me@live.com", "me@domain.com").Should().BeFalse();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SanitizeHtmlShouldWork(bool escapeNewlines)
        {
            var parser = new SubjectParser();
            var sanitizer = new OutlookWebSanitizer(parser);

            var content = File.ReadAllText("Data/outlook-html-email.txt");
            if (escapeNewlines)
            {
                // sendgrid escapes newlines so both should work
                content = content.Replace("\r\n", "\\r\\n");
            }
            sanitizer.TrySanitizeHtml(ref content, new SubjectModel
            {
                Prefix = "",
                RelayTarget = "ext@user.foo",
                Subject = "Test"
            }, "me@live.com", "me@mydomain.com").Should().BeTrue();
        }

        [Test]
        public void SanitizeHtmlWithPrefixShouldWork()
        {
            var parser = new SubjectParser();
            var sanitizer = new OutlookWebSanitizer(parser);

            var content = File.ReadAllText("Data/outlook-html-email.txt").Replace("Relay for ", "RE: Relay for ");
            sanitizer.TrySanitizeHtml(ref content, new SubjectModel
            {
                Prefix = "RE: ",
                RelayTarget = "ext@user.foo",
                Subject = "Test"
            }, "me@live.com", "me@mydomain.com").Should().BeTrue();
        }

        [Test]
        public void SanitizeHtmlShouldWorkWithSubjectPrefix()
        {
            var parser = new SubjectParser();
            var sanitizer = new OutlookWebSanitizer(parser);

            // when first responding to email the new subject will have "RE: .."
            // while the old subject(copied into the body) doesn't have it -> should parse just fine
            var content = File.ReadAllText("Data/outlook-html-email.txt");
            sanitizer.TrySanitizeHtml(ref content, new SubjectModel
            {
                Prefix = "RE: ",
                RelayTarget = "ext@user.foo",
                Subject = "Test"
            }, "me@live.com", "me@mydomain.com").Should().BeTrue();
        }

        [Test]
        public void SanitizeHtmlShouldFailIfNotMatched()
        {
            var parser = new SubjectParser();
            var sanitizer = new OutlookWebSanitizer(parser);

            var content = File.ReadAllText("Data/outlook-html-email.txt").Replace("me@mydomain.com", "me@mismatcheddomain.com");
            sanitizer.TrySanitizeHtml(ref content, new SubjectModel
            {
                Prefix = "RE: ",
                RelayTarget = "ext@user.foo",
                Subject = "Test"
            }, "me@live.com", "me@mydomain.com").Should().BeFalse();
            content.Should().Contain("me@live.com", "because no replacement occured");
        }

        [Test]
        public void SanitizeInitialMailShouldWorkWithoutReplacement()
        {
            var parser = new SubjectParser();
            var sanitizer = new OutlookWebSanitizer(parser);

            var content = "Hello";
            sanitizer.TrySanitizeHtml(ref content, new SubjectModel
            {
                Prefix = "",
                RelayTarget = "ext@user.foo",
                Subject = "Test"
            }, "me@live.com", "me@mydomain.com").Should().BeTrue();
            content.Should().Be("Hello");

            sanitizer.TrySanitizePlainText(ref content, new SubjectModel
            {
                Prefix = "",
                RelayTarget = "ext@user.foo",
                Subject = "Test"
            }, "me@live.com", "me@mydomain.com").Should().BeTrue();
            content.Should().Be("Hello");
        }
    }
}
