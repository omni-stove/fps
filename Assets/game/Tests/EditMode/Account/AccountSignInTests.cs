using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class AccountSignInTests
    {
        private FakeAccountBackend _backend;
        private AccountSignIn _signIn;
        private List<AccountStatus> _changes;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeAccountBackend();
            _signIn = new AccountSignIn(_backend);
            _changes = new List<AccountStatus>();
            _signIn.StatusChanged += status => _changes.Add(status);
        }

        [TearDown]
        public void TearDown()
        {
            _signIn.Dispose();
            GameSignals.Account.Clear();
            GameSignals.AccountControl.Clear();
        }

        [Test]
        public void WithoutTheServiceItTurnsOffOnceAndNeverSignsIn()
        {
            _backend.Available = false;

            LogAssert.Expect(LogType.Log, AccountLog.Off("no EOS credentials"));
            _signIn.Tick(0d);
            _signIn.Tick(100d);

            Assert.That(_signIn.IsOff, Is.True);
            Assert.That(_signIn.IsAvailable, Is.False);
            Assert.That(_backend.Prepares, Is.EqualTo(1));
            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare" }));
            Assert.That(_backend.Ticks, Is.Zero);
            Assert.That(_signIn.SignInWithEpic(), Is.False);
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void AtStartupARememberedEpicAccountSignsInWithoutTheDeviceId()
        {
            _backend.RestoreResult = AccountLoginResult.SignedIn();

            LogAssert.Expect(LogType.Log, AccountLog.Restoring());
            LogAssert.Expect(LogType.Log, AccountLog.SignedIn(AccountKind.Epic));
            _signIn.Tick(0d);

            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "restore" }));
            Assert.That(_signIn.Status, Is.EqualTo(new AccountStatus(AccountKind.Epic, AccountStage.Idle, AccountSignInFailure.None, true)));
            Assert.That(_signIn.Status.CanSignOut, Is.True);
            Assert.That(_signIn.Status.CanSignInWithEpic, Is.False);
        }

        [Test]
        public void WithoutARememberedEpicAccountItFallsBackToTheDeviceId()
        {
            LogAssert.Expect(LogType.Log, AccountLog.Restoring());
            LogAssert.Expect(LogType.Log, AccountLog.NoSavedLogin("not found"));
            LogAssert.Expect(LogType.Log, AccountLog.SignedIn(AccountKind.Device));
            _signIn.Tick(0d);

            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "restore", "device" }));
            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.Device));
            Assert.That(_signIn.Status.CanSignInWithEpic, Is.True);
        }

        [Test]
        public void WhenEpicAccountsAreNotSetUpOnlyTheDeviceIdIsUsed()
        {
            _backend.EpicAccount = false;

            _signIn.Tick(0d);

            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "device" }));
            Assert.That(_signIn.Status, Is.EqualTo(new AccountStatus(AccountKind.Device, AccountStage.Idle, AccountSignInFailure.None, false)));
            Assert.That(_signIn.SignInWithEpic(), Is.False);
        }

        [Test]
        public void AFailedRestoreStillPlaysWithTheDeviceIdAndTriesTheEpicAccountAgainNextTime()
        {
            _backend.RestoreResult = AccountLoginResult.Failed("EOS auth login failed: NoConnection");

            LogAssert.Expect(LogType.Warning, AccountLog.RestoreFailed("EOS auth login failed: NoConnection"));
            _signIn.Tick(0d);
            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.Device));

            _backend.SignedInKind = AccountKind.None;
            _backend.RestoreResult = AccountLoginResult.SignedIn();
            LogAssert.Expect(LogType.Log, AccountLog.Lost(AccountKind.Device));
            _signIn.Tick(1d);

            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "restore", "device", "restore" }));
            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.Epic));
        }

        [Test]
        public void AFailedDeviceSignInWaitsBeforeTryingAgain()
        {
            _backend.EpicAccount = false;
            _backend.DeviceResult = AccountLoginResult.Failed("creating the EOS device id failed: NoConnection");
            _signIn.RetrySeconds = 60d;

            LogAssert.Expect(LogType.Warning, AccountLog.DeviceFailed("creating the EOS device id failed: NoConnection", 60d));
            _signIn.Tick(10d);
            _signIn.Tick(69d);
            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "device" }));
            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.None));

            _backend.DeviceResult = AccountLoginResult.SignedIn();
            _signIn.Tick(70d);

            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "device", "device" }));
            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.Device));
        }

        [Test]
        public void SigningInWithEpicSwitchesFromTheDeviceIdAndShowsTheStagesOnTheWay()
        {
            _signIn.Tick(0d);
            _changes.Clear();
            _backend.Hold = true;

            Assert.That(_signIn.SignInWithEpic(), Is.True);
            Assert.That(_signIn.Status.Stage, Is.EqualTo(AccountStage.SigningIn));
            Assert.That(_signIn.SignInWithEpic(), Is.False, "a second press while signing in does nothing");
            Assert.That(_signIn.SignOut(), Is.False);

            LogAssert.Expect(LogType.Log, AccountLog.SignedIn(AccountKind.Epic));
            _backend.Release();

            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "restore", "device", "epic" }));
            Assert.That(_changes, Is.EqualTo(new[]
            {
                new AccountStatus(AccountKind.Device, AccountStage.SigningIn, AccountSignInFailure.None, true),
                new AccountStatus(AccountKind.Epic, AccountStage.Idle, AccountSignInFailure.None, true),
            }));
        }

        [Test]
        public void ACancelledEpicSignInKeepsTheDeviceIdAndSaysSo()
        {
            _signIn.Tick(0d);
            _backend.EpicResult = AccountLoginResult.Cancelled();

            LogAssert.Expect(LogType.Log, AccountLog.EpicSignInCancelled());
            Assert.That(_signIn.SignInWithEpic(), Is.True);

            Assert.That(_signIn.Status, Is.EqualTo(new AccountStatus(AccountKind.Device, AccountStage.Idle, AccountSignInFailure.Cancelled, true)));
        }

        [Test]
        public void AFailedEpicSignInKeepsTheDeviceIdAndTheNextTryClearsTheFailure()
        {
            _signIn.Tick(0d);
            _backend.EpicResult = AccountLoginResult.Failed("EOS auth login failed: InvalidParameters");

            LogAssert.Expect(LogType.Warning, AccountLog.EpicSignInFailed("EOS auth login failed: InvalidParameters"));
            _signIn.SignInWithEpic();
            Assert.That(_signIn.Status.LastFailure, Is.EqualTo(AccountSignInFailure.Failed));
            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.Device));

            _backend.Hold = true;
            _signIn.SignInWithEpic();

            Assert.That(_signIn.Status.LastFailure, Is.EqualTo(AccountSignInFailure.None));
        }

        [Test]
        public void SigningOutGoesBackToTheDeviceIdAndDoesNotRestoreTheEpicAccount()
        {
            _backend.RestoreResult = AccountLoginResult.SignedIn();
            _signIn.Tick(0d);

            LogAssert.Expect(LogType.Log, AccountLog.SignedOut());
            Assert.That(_signIn.SignOut(), Is.True);
            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.None));
            Assert.That(_signIn.Status.Stage, Is.EqualTo(AccountStage.Idle));

            _signIn.Tick(1d);

            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "restore", "sign-out", "device" }));
            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.Device));
            Assert.That(_signIn.SignOut(), Is.False, "the device id cannot be signed out of");
        }

        [Test]
        public void ASignOutThatDoesNotFinishCleanlyStillLeavesTheEpicAccount()
        {
            _backend.RestoreResult = AccountLoginResult.SignedIn();
            _signIn.Tick(0d);
            _backend.SignOutError = "EOS auth logout failed: NoConnection";

            LogAssert.Expect(LogType.Warning, AccountLog.SignOutFailed("EOS auth logout failed: NoConnection"));
            _signIn.SignOut();
            _signIn.Tick(1d);

            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.Device));
        }

        [Test]
        public void AnEndedSignInIsSignedInAgainWithTheSameKind()
        {
            _signIn.Tick(0d);
            _signIn.SignInWithEpic();
            _backend.RestoreResult = AccountLoginResult.SignedIn();
            _backend.SignedInKind = AccountKind.None;

            LogAssert.Expect(LogType.Log, AccountLog.Lost(AccountKind.Epic));
            _signIn.Tick(1d);

            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "restore", "device", "epic", "restore" }));
            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.Epic));
        }

        [Test]
        public void ALoginMadeElsewhereShowsUpWhileTheStartupSignInIsStillRunning()
        {
            _backend.Hold = true;
            _signIn.Tick(0d);
            _backend.SignedInKind = AccountKind.Device;

            _signIn.Tick(1d);

            Assert.That(_signIn.Status.Kind, Is.EqualTo(AccountKind.Device));
            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "restore" }));
        }

        [Test]
        public void ResultsThatArriveAfterDisposeAreIgnored()
        {
            _backend.Hold = true;
            _signIn.Tick(0d);
            _signIn.Dispose();

            _backend.Release();

            Assert.That(_signIn.Status.Stage, Is.EqualTo(AccountStage.SigningIn));
            Assert.That(_backend.Calls, Is.EqualTo(new[] { "prepare", "restore" }), "the device id is not tried for a disposed sign-in");
        }

        [Test]
        public void TheBootstrapPublishesTheControlOnlyOnceTheServiceIsReady()
        {
            var signIn = new AccountSignIn(_backend);
            AccountBootstrap.Publish(signIn);
            Assert.That(GameSignals.AccountControl.HasValue, Is.False);

            _backend.Hold = true;
            signIn.Tick(0d);

            Assert.That(GameSignals.AccountControl.Value, Is.SameAs(signIn));
            Assert.That(GameSignals.Account.Value, Is.EqualTo(new AccountStatus(AccountKind.None, AccountStage.SigningIn, AccountSignInFailure.None, true)));

            _backend.Hold = false;
            _backend.Release();

            Assert.That(GameSignals.Account.Value, Is.EqualTo(new AccountStatus(AccountKind.Device, AccountStage.Idle, AccountSignInFailure.None, true)));
            signIn.Dispose();
        }

        [Test]
        public void TheBootstrapPublishesNothingWhenTheServiceIsOff()
        {
            _backend.Available = false;
            var signIn = new AccountSignIn(_backend);
            AccountBootstrap.Publish(signIn);

            LogAssert.Expect(LogType.Log, AccountLog.Off("no EOS credentials"));
            signIn.Tick(0d);

            Assert.That(GameSignals.AccountControl.HasValue, Is.False);
            Assert.That(GameSignals.Account.HasValue, Is.False);
            signIn.Dispose();
        }
    }
}
