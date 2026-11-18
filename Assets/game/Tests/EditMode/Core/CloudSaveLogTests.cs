using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CloudSaveLogTests
    {
        [Test]
        public void LoginRefreshedIsTagged()
        {
            Assert.That(CloudSaveLog.LoginRefreshed(), Is.EqualTo("[CloudSave] refreshed the EOS login"));
        }

        [Test]
        public void LoginRefreshFailedCarriesTheError()
        {
            Assert.That(
                CloudSaveLog.LoginRefreshFailed("timed out"),
                Is.EqualTo("[CloudSave] refreshing the EOS login failed: timed out; signing in again when it expires"));
        }

        [Test]
        public void LoginRefreshFailedRejectsNullError()
        {
            Assert.Throws<ArgumentNullException>(() => CloudSaveLog.LoginRefreshFailed(null));
        }

        [Test]
        public void LoginNotWatchedSaysTheLoginWillNotBeRefreshed()
        {
            Assert.That(
                CloudSaveLog.LoginNotWatched(),
                Is.EqualTo("[CloudSave] EOS connect notifications could not be registered; an expired login will not be refreshed"));
        }
    }
}
