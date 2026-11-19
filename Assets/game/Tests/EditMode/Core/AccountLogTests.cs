using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class AccountLogTests
    {
        [Test]
        public void SignedInNamesTheKind()
        {
            Assert.That(AccountLog.SignedIn(AccountKind.Epic), Is.EqualTo("[Account] signed in with the Epic account"));
            Assert.That(AccountLog.SignedIn(AccountKind.Device), Is.EqualTo("[Account] signed in with the device id"));
        }

        [Test]
        public void TheFallbackSaysTheDeviceIdIsUsed()
        {
            Assert.That(
                AccountLog.NoSavedLogin("EOS auth persistent login returned NotFound"),
                Is.EqualTo("[Account] no remembered Epic account (EOS auth persistent login returned NotFound); signing in with the device id"));
        }

        [Test]
        public void DeviceFailuresSayWhenTheyAreTriedAgain()
        {
            Assert.That(
                AccountLog.DeviceFailed("timed out", 60d),
                Is.EqualTo("[Account] the device id sign-in failed: timed out; retrying in 60s"));
        }

        [Test]
        public void RefreshLinesNameTheKind()
        {
            Assert.That(AccountLog.Refreshed(AccountKind.Epic), Is.EqualTo("[Account] refreshed the Epic account sign-in"));
            Assert.That(
                AccountLog.RefreshFailed(AccountKind.Device, "timed out"),
                Is.EqualTo("[Account] refreshing the device id sign-in failed: timed out; signing in again when it expires"));
        }

        [Test]
        public void NotWatchedSaysTheSignInWillNotBeRefreshed()
        {
            Assert.That(
                AccountLog.NotWatched(),
                Is.EqualTo("[Account] EOS connect notifications could not be registered; an expired sign-in will not be refreshed"));
        }

        [Test]
        public void ErrorsMustBeGiven()
        {
            Assert.Throws<ArgumentNullException>(() => AccountLog.EpicSignInFailed(null));
            Assert.Throws<ArgumentNullException>(() => AccountLog.Off(null));
            Assert.Throws<ArgumentNullException>(() => AccountLog.RefreshFailed(AccountKind.Epic, null));
        }
    }
}
