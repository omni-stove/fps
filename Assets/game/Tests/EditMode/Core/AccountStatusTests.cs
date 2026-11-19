using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class AccountStatusTests
    {
        [Test]
        public void TheDefaultIsSignedOutAndIdleWithoutEpicAccounts()
        {
            var status = default(AccountStatus);

            Assert.That(status.Kind, Is.EqualTo(AccountKind.None));
            Assert.That(status.Stage, Is.EqualTo(AccountStage.Idle));
            Assert.That(status.LastFailure, Is.EqualTo(AccountSignInFailure.None));
            Assert.That(status.CanSignInWithEpic, Is.False);
            Assert.That(status.CanSignOut, Is.False);
            Assert.That(status.IsBusy, Is.False);
        }

        [Test]
        public void OnTheDeviceIdAnEpicAccountCanBeSignedInTo()
        {
            var status = new AccountStatus(AccountKind.Device, AccountStage.Idle, AccountSignInFailure.None, true);

            Assert.That(status.CanSignInWithEpic, Is.True);
            Assert.That(status.CanSignOut, Is.False);
        }

        [Test]
        public void OnAnEpicAccountOnlySigningOutIsOffered()
        {
            var status = new AccountStatus(AccountKind.Epic, AccountStage.Idle, AccountSignInFailure.None, true);

            Assert.That(status.CanSignInWithEpic, Is.False);
            Assert.That(status.CanSignOut, Is.True);
        }

        [Test]
        public void NothingCanBeStartedWhileBusy()
        {
            var signingIn = new AccountStatus(AccountKind.Device, AccountStage.SigningIn, AccountSignInFailure.None, true);
            var signingOut = new AccountStatus(AccountKind.Epic, AccountStage.SigningOut, AccountSignInFailure.None, true);

            Assert.That(signingIn.IsBusy, Is.True);
            Assert.That(signingIn.CanSignInWithEpic, Is.False);
            Assert.That(signingOut.CanSignOut, Is.False);
        }

        [Test]
        public void EqualityComparesEveryField()
        {
            var status = new AccountStatus(AccountKind.Device, AccountStage.Idle, AccountSignInFailure.Cancelled, true);

            Assert.That(status, Is.EqualTo(new AccountStatus(AccountKind.Device, AccountStage.Idle, AccountSignInFailure.Cancelled, true)));
            Assert.That(status == new AccountStatus(AccountKind.Device, AccountStage.Idle, AccountSignInFailure.None, true), Is.False);
            Assert.That(status != new AccountStatus(AccountKind.Device, AccountStage.Idle, AccountSignInFailure.Cancelled, false), Is.True);
            Assert.That(status.ToString(), Is.EqualTo("account=Device stage=Idle failure=Cancelled epic"));
        }

        [Test]
        public void UndefinedValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AccountStatus((AccountKind)9, AccountStage.Idle, AccountSignInFailure.None, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AccountStatus(AccountKind.None, (AccountStage)9, AccountSignInFailure.None, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AccountStatus(AccountKind.None, AccountStage.Idle, (AccountSignInFailure)9, false));
        }

        [Test]
        public void LoginResultsCarryTheirOutcome()
        {
            Assert.That(AccountLoginResult.SignedIn().Succeeded, Is.True);
            Assert.That(AccountLoginResult.SignedIn().Error, Is.Null);
            Assert.That(AccountLoginResult.Cancelled().Outcome, Is.EqualTo(AccountLoginOutcome.Cancelled));
            Assert.That(AccountLoginResult.NoSavedLogin("not found").ToString(), Is.EqualTo("NoSavedLogin: not found"));
            Assert.That(AccountLoginResult.Failed("x"), Is.EqualTo(AccountLoginResult.Failed("x")));
            Assert.Throws<ArgumentException>(() => AccountLoginResult.Failed(" "));
            Assert.Throws<ArgumentException>(() => AccountLoginResult.NoSavedLogin(null));
        }
    }
}
