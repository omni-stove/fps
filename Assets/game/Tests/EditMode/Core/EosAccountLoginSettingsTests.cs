using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class EosAccountLoginSettingsTests
    {
        [Test]
        public void NothingWrittenMeansTheDeviceIdOnly()
        {
            var settings = EosAccountLoginSettings.Parse(null, "  ");

            Assert.That(settings, Is.SameAs(EosAccountLoginSettings.Disabled));
            Assert.That(settings.EpicAccount, Is.False);
            Assert.That(settings.HasDevAuth, Is.False);
            Assert.That(settings.Problem, Is.Null);
        }

        [TestCase("true")]
        [TestCase("1")]
        [TestCase(" ON ")]
        [TestCase("Yes")]
        public void SwitchedOnUsesTheAccountPortal(string value)
        {
            var settings = EosAccountLoginSettings.Parse(value, null);

            Assert.That(settings.EpicAccount, Is.True);
            Assert.That(settings.HasDevAuth, Is.False);
            Assert.That(settings.DevAuthHost, Is.Null);
            Assert.That(settings.Problem, Is.Null);
        }

        [TestCase("false")]
        [TestCase("0")]
        [TestCase("off")]
        [TestCase("NO")]
        public void SwitchedOffIsTheDeviceIdOnly(string value)
        {
            Assert.That(EosAccountLoginSettings.Parse(value, null), Is.SameAs(EosAccountLoginSettings.Disabled));
        }

        [Test]
        public void AnUnreadableSwitchTurnsEpicAccountsOffAndSaysWhy()
        {
            var settings = EosAccountLoginSettings.Parse("maybe", null);

            Assert.That(settings.EpicAccount, Is.False);
            Assert.That(settings.Problem, Is.EqualTo("EOS_EPIC_ACCOUNT_LOGIN must be true or false"));
        }

        [Test]
        public void TheDevAuthToolIsSplitAtTheLastSlash()
        {
            var settings = EosAccountLoginSettings.Parse("true", " localhost:6547/Player One ");

            Assert.That(settings.EpicAccount, Is.True);
            Assert.That(settings.HasDevAuth, Is.True);
            Assert.That(settings.DevAuthHost, Is.EqualTo("localhost:6547"));
            Assert.That(settings.DevAuthCredential, Is.EqualTo("Player One"));
        }

        [TestCase("localhost:6547")]
        [TestCase("localhost:6547/")]
        [TestCase("/Player")]
        [TestCase("localhost/Player")]
        public void AMalformedDevAuthToolTurnsEpicAccountsOff(string value)
        {
            var settings = EosAccountLoginSettings.Parse("true", value);

            Assert.That(settings.EpicAccount, Is.False);
            Assert.That(settings.Problem, Is.EqualTo("EOS_EPIC_DEV_AUTH must be written as host:port/credential"));
        }

        [Test]
        public void ADevAuthToolWithoutTheSwitchSaysTheSwitchIsOff()
        {
            var settings = EosAccountLoginSettings.Parse(null, "localhost:6547/Player");

            Assert.That(settings.EpicAccount, Is.False);
            Assert.That(settings.Problem, Is.EqualTo("EOS_EPIC_DEV_AUTH is set but EOS_EPIC_ACCOUNT_LOGIN is not on"));
        }

        [Test]
        public void EnabledNeedsBothDevAuthPartsOrNeither()
        {
            Assert.Throws<ArgumentException>(() => EosAccountLoginSettings.Enabled("localhost:6547", null));
            Assert.That(EosAccountLoginSettings.Enabled().HasDevAuth, Is.False);
        }

        [Test]
        public void InvalidNeedsAReason()
        {
            Assert.Throws<ArgumentException>(() => EosAccountLoginSettings.Invalid(" "));
        }

        [Test]
        public void ToStringDoesNotShowTheCredentialName()
        {
            var settings = EosAccountLoginSettings.Parse("true", "localhost:6547/Secret");

            Assert.That(settings.ToString(), Is.EqualTo("EosAccountLoginSettings(epic, dev auth)"));
        }
    }
}
