using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CloudSaveLogTests
    {
        [Test]
        public void OffCarriesTheReason()
        {
            Assert.That(CloudSaveLog.Off("no EOS credentials"), Is.EqualTo("[CloudSave] local only: no EOS credentials"));
        }

        [Test]
        public void OffRejectsNullReason()
        {
            Assert.Throws<ArgumentNullException>(() => CloudSaveLog.Off(null));
        }

        [Test]
        public void SignedOutSaysSavesStayLocal()
        {
            Assert.That(CloudSaveLog.SignedOut(), Is.EqualTo("[CloudSave] signed out; saving locally until the next sign-in"));
        }
    }
}
