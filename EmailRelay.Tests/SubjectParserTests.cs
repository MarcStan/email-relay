using EmailRelay.Logic;
using FluentAssertions;
using NUnit.Framework;

namespace EmailRelay.Tests
{
    public class SubjectParserTests
    {
        [TestCase("Inquiry")]
        [TestCase("Request")]
        [TestCase("Relay notification")]
        public void ParseRegularSubjects(string subject)
        {
            var r = new SubjectParser().Parse(subject);
            r.Prefix.Should().BeNullOrEmpty();
            r.RelayTarget.Should().BeNullOrEmpty();
            r.Subject.Should().Be(subject);
        }

        [TestCase("Inquiry")]
        [TestCase("Relay request")]
        [TestCase("Relay for foo@example.com")]
        public void ParseRelaySubjects(string subject)
        {
            var r = new SubjectParser().Parse($"Relay for me@example.com: {subject}");
            r.Prefix.Should().BeNullOrEmpty();
            r.RelayTarget.Should().Be("me@example.com");
            r.Subject.Should().Be(subject);
        }

        [TestCase("Inquiry")]
        [TestCase("Relay request")]
        [TestCase("Relay for foo@example.com")]
        public void ParseCustomPrefix(string subject)
        {
            var r = new SubjectParser("Email for").Parse($"Email for me@example.com: {subject}");
            r.Prefix.Should().BeNullOrEmpty();
            r.RelayTarget.Should().Be("me@example.com");
            r.Subject.Should().Be(subject);
        }

        [TestCase("Fwd:", "Inquiry")]
        [TestCase("Re:", "Inquiry")]
        [TestCase("Fw:", "Inquiry")]
        [TestCase("RE:", "Inquiry")]
        [TestCase("FW:", "Inquiry")]
        [TestCase("FWD:", "Inquiry")]
        [TestCase("RE: RE: ", "Inquiry")]
        [TestCase("RE: RE: RE: ", "Inquiry")]
        [TestCase("RE: FWD: RE: RE:", "Inquiry")]
        public void ParseRelaySubjects(string prefix, string subject)
        {
            var r = new SubjectParser().Parse($"{prefix}Relay for me@example.com: {subject}");
            r.Prefix.Should().Be(prefix);
            r.RelayTarget.Should().Be("me@example.com");
            r.Subject.Should().Be(subject);
        }

        [TestCase("Inquiry")]
        [TestCase("Relay request")]
        [TestCase("Relay for foo@example.com")]
        public void MultipleRelaysShouldBeParsedToOne(string subject)
        {
            var r = new SubjectParser().Parse($"Relay for me@example.com: Relay for me@example.com: {subject}");
            r.Prefix.Should().BeNullOrEmpty();
            r.RelayTarget.Should().Be("me@example.com");
            r.Subject.Should().Be(subject);
        }

        [Test]
        public void ManyRelaysShouldBeParsedToOne()
        {
            var r = new SubjectParser().Parse("Relay for me@example.com: Relay for me2@example.com: Relay for me3@example.com: Relay for me4@example.com: Subject");
            r.Prefix.Should().BeNullOrEmpty();
            r.RelayTarget.Should().Be("me@example.com");
            r.Subject.Should().Be("Subject");
        }
    }
}
