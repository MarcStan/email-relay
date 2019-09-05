using EmailRelay.Logic;
using EmailRelay.Logic.Models;
using EmailRelay.Logic.Sanitizers;
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
        private IMetadataSanitizer[] GetDefaultSanitizers()
            => new[] { new FakeSanitizer() };

        [Test]
        public async Task MailFromExternalUserShouldBeRelayedToTarget()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, GetDefaultSanitizers());

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
            true,
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
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, GetDefaultSanitizers());

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
            }, "me@privatemail.example.com", "domain.com", true, CancellationToken.None);

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
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, GetDefaultSanitizers());

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
                Text = "Foo",
                Subject = "Relay for ext@user.foo: Inquiry",
                Spf = "pass",
                Dkim = "{@privatemail.example.com : pass}"
            }, "me@privatemail.example.com", "domain.com", true, CancellationToken.None);

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
        public async Task SendingEmailReplyFromTargetToDomainWithSpecialSubjectShouldSendAsDomainToExternalUserAndReplacePrivateMailInMetadataOfBody_customRelayTarget()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var parser = new SubjectParser("Email Relay:");
            var relay = new RelayLogic(client.Object, parser, logger.Object, new[] { new OutlookWebSanitizer(parser) });

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "me@live.com"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Text = @"This is my response

___________________________________________
From: me@domain.com <me@domain.com>
Sent: Tuesday, September 3, 2019 11:19:42 PM
To: me@live.com <me@live.com>
Subject: Email Relay: ext@user.foo: Test
 
This is the original message from someone",
                Subject = "Email Relay: ext@user.foo: Test",
                Spf = "pass",
                Dkim = "{@live.com : pass}"
            }, "me@live.com", "domain.com", true, CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "ext@user.foo" &&
                m.Personalizations[0].Subject == "Test" &&
                !m.Contents[0].Value.Contains("From: me@domain.com <me@domain.com>") &&
                !m.Contents[0].Value.Contains("To: me@live.com <me@live.com>") &&
                !m.Contents[0].Value.Contains("Subject: Email Relay: ext@user.foo: Test") &&
                m.Contents[0].Value.Contains("From: ext@user.foo <ext@user.foo>") &&
                m.Contents[0].Value.Contains("To: me@domain.com <me@domain.com>") &&
                m.Contents[0].Value.Contains("Subject: Test")),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SendingEmailReplyFromTargetToDomainWithSpecialSubjectShouldSendAsDomainToExternalUserAndReplacePrivateMailInMetadataOfBody_defaultRelayTarget()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, new[] { new OutlookWebSanitizer(parser) });

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "me@live.com"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Text = @"This is my response

___________________________________________
From: me@domain.com <me@domain.com>
Sent: Tuesday, September 3, 2019 11:19:42 PM
To: me@live.com <me@live.com>
Subject: Relay for ext@user.foo: Test
 
This is the original message from someone",
                Subject = "Relay for ext@user.foo: Test",
                Spf = "pass",
                Dkim = "{@live.com : pass}"
            }, "me@live.com", "domain.com", true, CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "ext@user.foo" &&
                m.Personalizations[0].Subject == "Test" &&
                !m.Contents[0].Value.Contains("From: me@domain.com <me@domain.com>") &&
                !m.Contents[0].Value.Contains("To: me@live.com <me@live.com>") &&
                !m.Contents[0].Value.Contains("Subject: Relay for ext@user.foo: Test") &&
                m.Contents[0].Value.Contains("From: ext@user.foo <ext@user.foo>") &&
                m.Contents[0].Value.Contains("To: me@domain.com <me@domain.com>") &&
                m.Contents[0].Value.Contains("Subject: Test")),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SendingEmailReplyFromTargetToDomainWithSpecialSubjectShouldSendAsDomainToExternalUserAndReplacePrivateMailInMetadataOfBody_WithSubjectPrefix()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, new[] { new OutlookWebSanitizer(parser) });

            await relay.RelayAsync(new Email
            {
                From = new EmailAddress
                {
                    Email = "me@live.com"
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Email = "me@domain.com"
                    }
                },
                Text = @"This is my response

___________________________________________
From: me@domain.com <me@domain.com>
Sent: Tuesday, September 3, 2019 11:19:42 PM
To: me@live.com <me@live.com>
Subject: RE: Relay for ext@user.foo: Test
 
This is the original message from someone",
                Subject = "RE: Relay for ext@user.foo: Test",
                Spf = "pass",
                Dkim = "{@live.com : pass}"
            }, "me@live.com", "domain.com", true, CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "ext@user.foo" &&
                m.Personalizations[0].Subject == "RE: Test" &&
                !m.Contents[0].Value.Contains("From: me@domain.com <me@domain.com>") &&
                !m.Contents[0].Value.Contains("To: me@live.com <me@live.com>") &&
                !m.Contents[0].Value.Contains("Subject: RE: Relay for ext@user.foo: Test") &&
                m.Contents[0].Value.Contains("From: ext@user.foo <ext@user.foo>") &&
                m.Contents[0].Value.Contains("To: me@domain.com <me@domain.com>") &&
                m.Contents[0].Value.Contains("Subject: RE: Test")),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SendingEmailFromTargetToDomainWithSpecialSubjectShouldSendWarningToOwnerIfSendAsDomainIsDisabled()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, GetDefaultSanitizers());

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
            }, "me@privatemail.example.com", "domain.com", false, CancellationToken.None);

            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "[WARNING] Relay for ext@user.foo: Inquiry"),
                It.IsAny<CancellationToken>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SendingEmailFromTargetToDomainWithWrongSubjectShouldSendRegularEmailToOwner()
        {
            var client = new Mock<ISendGridClient>();
            var logger = new Mock<ILogger>();
            var parser = new SubjectParser("this is the correct prefix for");
            var relay = new RelayLogic(client.Object, parser, logger.Object, GetDefaultSanitizers());

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
            }, "me@privatemail.example.com", "domain.com", true, CancellationToken.None);

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
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, GetDefaultSanitizers());

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
            }, "me@privatemail.example.com", "domain.com", true, CancellationToken.None);

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
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, GetDefaultSanitizers());

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
            true,
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
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, GetDefaultSanitizers());

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
            }, "me@privatemail.example.com", "domain.com", true, CancellationToken.None);

            // external user should not be allowed to send as domain just by sending well crafted subject!
            // warning email must be issued to owner
            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "[SPOOFWARNING] Relay for some@service.hack: Inquiry" &&
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
            var parser = new SubjectParser();
            var relay = new RelayLogic(client.Object, parser, logger.Object, GetDefaultSanitizers());

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
            }, "me@privatemail.example.com", "domain.com", true, CancellationToken.None);

            // external user should not be allowed to send as domain just by sending well crafted subject!
            // warning email must be issued to owner
            client.Verify(x => x.SendEmailAsync(It.Is<SendGridMessage>(m =>
                m.From.Email == "me@domain.com" &&
                m.Personalizations.Count == 1 &&
                m.Personalizations[0].Tos.Count == 1 &&
                m.Personalizations[0].Tos[0].Email == "me@privatemail.example.com" &&
                m.Personalizations[0].Subject == "[SPOOFWARNING] Relay for some@service.hack: Inquiry" &&
                m.Contents[0].Value.Contains("Someone tried to send an email in the name of the domain")),
                It.IsAny<CancellationToken>()));
            logger.Verify(x => x.Log(LogLevel.Critical, It.IsAny<EventId>(), It.IsAny<object>(), null, It.IsAny<Func<object, Exception, string>>()));

            client.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }
    }
}
