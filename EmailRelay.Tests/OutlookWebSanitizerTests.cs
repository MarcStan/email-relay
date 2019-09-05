﻿
using EmailRelay.Logic;
using EmailRelay.Logic.Models;
using EmailRelay.Logic.Sanitizers;
using FluentAssertions;
using NUnit.Framework;

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

    }
}
