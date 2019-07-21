using EmailRelay.Logic;
using EmailRelay.Logic.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmailRelay.Tests
{
    public class RelayLogicTests
    {
        [Test]
        public async Task MailFromExternalUserShouldBeRelayedToTarget()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var relay = new RelayLogic(client.Object, new SubjectParser(), logger.Object);

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "ext@user.foo"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Html = "Foo",
                Subject = "Inquiry"
            },
            "me@privatemail.example.com",
            "domain.com",
            CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "Relay for ext@user.foo: Inquiry"),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SendingEmailFromTargetToDomainShouldBeRelayedBackToTarget()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var relay = new RelayLogic(client.Object, new SubjectParser(), logger.Object);

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "me@privatemail.example.com"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Html = "Foo",
                Subject = "Inquiry"
            }, "me@privatemail.example.com", "domain.com", CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "Relay for me@privatemail.example.com: Inquiry"),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SendingEmailFromTargetToDomainWithSpecialSubjectShouldSendAsDomainToExternalUser()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var relay = new RelayLogic(client.Object, new SubjectParser(), logger.Object);

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "me@privatemail.example.com"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Html = "Foo",
                Subject = "Relay for ext@user.foo: Inquiry",
                Spf = "pass",
                Dkim = "{@privatemail.example.com : pass}"
            }, "me@privatemail.example.com", "domain.com", CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "ext@user.foo" &&
                m.Personalizations[0].Subject == "Inquiry"),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SendingEmailFromTargetToDomainWithWrongSubjectShouldSendRegularEmailToOwner()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var relay = new RelayLogic(client.Object, new SubjectParser("this is the correct prefix for"), logger.Object);

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "me@privatemail.example.com"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Html = "Foo",
                Subject = "this is the wrong prefix for ext@user.foo: Inquiry",
                Spf = "pass",
                Dkim = "{@privatemail.example.com : pass}"
            }, "me@privatemail.example.com", "domain.com", CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "this is the correct prefix for me@privatemail.example.com: this is the wrong prefix for ext@user.foo: Inquiry"),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }
        [Test]
        public async Task SendingEmailFromTargetToDomainWithSpecialSubjectShouldSendAsDomainToSelf()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var relay = new RelayLogic(client.Object, new SubjectParser(), logger.Object);

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "me@privatemail.example.com"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Html = "Foo",
                Subject = "Relay for me@privatemail.example.com: Inquiry",
                Spf = "pass",
                Dkim = "{@privatemail.example.com : pass}"
            }, "me@privatemail.example.com", "domain.com", CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "Inquiry"),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task MailWithMultipleRecipientsShouldBeSentToFirstDomain()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var relay = new RelayLogic(client.Object, new SubjectParser(), logger.Object);

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "ext@user.foo"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "someoneelse@notmydomain.com"
                    },
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Cc = new[]
                {
                    new EmailAddress
                    {
                        Email = "another@domain.com"
                    }
                },
                Html = "Foo",
                Subject = "Inquiry"
            },
            "me@privatemail.example.com",
            "domain.com",
            CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "Relay for ext@user.foo: Inquiry"),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SpecialSubjectFromExternalUserShouldBeLoggedAndNotSendFromDomain()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var relay = new RelayLogic(client.Object, new SubjectParser(), logger.Object);

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "ext@user.foo"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Html = "Foo",
                Subject = "Relay for some@service.hack: Inquiry"
            }, "me@privatemail.example.com", "domain.com", CancellationToken.None);

            // external user should not be allowed to send as domain just by sending well crafted subject!
            // warning email must be issued to owner
            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "[WARNING] Relay for ext@user.foo: Inquiry" &&
                m.Contents[0].Value.Contains("Someone tried to send an email in the name of the domain")),
                It.IsAny<CancellationToken>()));
            logger.Verify(x => x.Log(LogLevel.Critical, It.IsAny<EventId>(), It.IsAny<object>(), null, It.IsAny<Func<object, Exception, string>>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SpecialSubjectFromSpoofedUserShouldBeLoggedAndNotSendFromDomain()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var relay = new RelayLogic(client.Object, new SubjectParser(), logger.Object);

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Name = "spoofed",
                    Email = "me@privatemail.example.com"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Html = "Foo",
                Subject = "Relay for some@service.hack: Inquiry",
                Dkim = "none",
                Spf = "softfail"
            }, "me@privatemail.example.com", "domain.com", CancellationToken.None);

            // external user should not be allowed to send as domain just by sending well crafted subject!
            // warning email must be issued to owner
            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "[WARNING] Relay for (SPOOFED) me@privatemail.example.com: Inquiry" &&
                m.Contents[0].Value.Contains("Someone tried to send an email in the name of the domain")),
                It.IsAny<CancellationToken>()));
            logger.Verify(x => x.Log(LogLevel.Critical, It.IsAny<EventId>(), It.IsAny<object>(), null, It.IsAny<Func<object, Exception, string>>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }
    }
}
