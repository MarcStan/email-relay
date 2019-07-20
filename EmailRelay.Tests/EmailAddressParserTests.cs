using EmailRelay.Logic;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NUnit.Framework;
using SendGrid.Helpers.Mail;

namespace EmailRelay.Tests
{
    public class EmailAddressParserTests
    {
        [TestCase("foo@example.com")]
        [TestCase("v@a.example.com")]
        [TestCase("v@a.co.uk")]
        public void ParseValidEmail(string email)
        {
            var e = EmailAddressParser.ParseEmailAddress($"\"First, middle, lastname\" <{email}>");
            e.Name.Should().Be("First, middle, lastname");
            e.Email.Should().Be(email);

            e = EmailAddressParser.ParseEmailAddress($"\"First\" <{email}>");
            e.Name.Should().Be("First");
            e.Email.Should().Be(email);

            e = EmailAddressParser.ParseEmailAddress(email);
            e.Name.Should().Be("");
            e.Email.Should().Be(email);
        }

        [TestCase("foo@example.com")]
        [TestCase("v@a.example.com")]
        [TestCase("v@a.co.uk")]
        public void ParseMultipleValidEmails(string email)
        {
            void verify(EmailAddress[] addr, params string[] expectedNames)
            {
                addr.Should().HaveCount(2);
                for (int i = 0; i < addr.Length; i++)
                {
                    addr[i].Email.Should().Be(email);
                    addr[i].Name.Should().Be(expectedNames[i]);
                }
            }

            verify(EmailAddressParser.ParseEmailAddresses($"\"First, middle, lastname\" <{email}>, \"Second\" <{email}>"), "First, middle, lastname", "Second");
            verify(EmailAddressParser.ParseEmailAddresses($"\"First, middle, lastname\" <{email}>, \"First\" <{email}>"), "First, middle, lastname", "First");
            verify(EmailAddressParser.ParseEmailAddresses($"\"First, middle, lastname\" <{email}>, {email}"), "First, middle, lastname", "");
        }
    }
}
