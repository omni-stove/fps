using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Runtime;
using CorePartyStatus = Shooter.Core.PartyStatus;

namespace Shooter.Net.Tests
{
    public sealed class NetV2PartyControlTests
    {
        private const string _version = "0.3.0";

        private readonly List<string> _logs = new List<string>();
        private readonly List<CorePartyStatus> _changes = new List<CorePartyStatus>();
        private FakeLobbyBackend _backend;
        private NetV2PartyControl _control;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Party.Clear();
            GameSignals.PartyControl.Clear();
            _logs.Clear();
            _changes.Clear();
            _backend = new FakeLobbyBackend();
            _control = new NetV2PartyControl(new PartySession(_backend, _version, _logs.Add));
            _control.StatusChanged += _changes.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _control.Dispose();
            GameSignals.Party.Clear();
            GameSignals.PartyControl.Clear();
        }

        [Test]
        public void ItIsPublishedAsThePartyControlWithNoPartyYet()
        {
            Assert.That(GameSignals.PartyControl.Value, Is.SameAs(_control));
            Assert.That(GameSignals.Party.Value, Is.EqualTo((CorePartyStatus?)new CorePartyStatus(PartyStage.None, null, null, false)));
            Assert.That(NetV2PartyControl.Current, Is.SameAs(_control));
            Assert.That(_control.PartyId, Is.Null);
            Assert.That(_control.MatchPlayers, Is.EqualTo(1));
        }

        [Test]
        public void CreatingLogsInAndShowsTheJoiningStage()
        {
            _control.Create();

            Assert.That(_backend.Logins, Is.EqualTo(1));
            Assert.That(_control.Status.Stage, Is.EqualTo(PartyStage.Joining));
            Assert.That(GameSignals.Party.Value.Value.Stage, Is.EqualTo(PartyStage.Joining));
            Assert.That(_changes, Has.Count.EqualTo(1));
        }

        [Test]
        public void AFailedLoginGoesBackToNoParty()
        {
            _control.Create();

            _backend.CompleteLogin(null, "network down");

            Assert.That(_control.Status.Stage, Is.EqualTo(PartyStage.None));
            Assert.That(GameSignals.Party.Value.Value.Stage, Is.EqualTo(PartyStage.None));
            Assert.That(_logs, Has.Some.StartsWith(PartyLog.Tag));
        }

        [Test]
        public void AMistypedInviteCodeStaysOutsideAParty()
        {
            _control.Join("not a code!");

            Assert.That(_backend.Logins, Is.EqualTo(0));
            Assert.That(_control.Status.Stage, Is.EqualTo(PartyStage.None));
        }

        [Test]
        public void TickDrivesTheLobbyService()
        {
            _control.Tick(1f);

            Assert.That(_backend.Ticks, Is.EqualTo(1));
        }

        [Test]
        public void DisposingWithdrawsTheSignalsAndClosesTheService()
        {
            _control.Dispose();

            Assert.That(GameSignals.PartyControl.HasValue, Is.False);
            Assert.That(GameSignals.Party.HasValue, Is.False);
            Assert.That(NetV2PartyControl.Current, Is.Null);
            Assert.That(_backend.Disposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => _control.Create());
            Assert.That(_control.PartyId, Is.Null);
            Assert.That(_control.MatchPlayers, Is.EqualTo(1));
        }

        [Test]
        public void DisposingLeavesAnotherPublishersStatusAlone()
        {
            var other = new CorePartyStatus(PartyStage.Searching, new[] { new PartyMember("someone", true) }, "party-9", false);
            GameSignals.Party.Publish(other);

            _control.Dispose();

            Assert.That(GameSignals.Party.Value, Is.EqualTo((CorePartyStatus?)other));
        }

        [Test]
        public void RefusesANullSession()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2PartyControl(null));
            Assert.Throws<ArgumentNullException>(() => new NetV2PartyControl(null, () => 1000));
        }

        [Test]
        public void RefusesANullSkillRatingSource()
        {
            var session = new PartySession(new FakeLobbyBackend(), _version, _logs.Add);

            Assert.Throws<ArgumentNullException>(() => new NetV2PartyControl(session, null));
            session.Dispose();
        }

        [Test]
        public void TickAndTheAverageTakeTheCurrentSkillRating()
        {
            _control.Dispose();
            int? rating = 1000;
            _backend = new FakeLobbyBackend();
            _control = new NetV2PartyControl(new PartySession(_backend, _version, _logs.Add), () => rating);

            _control.Tick(1f);
            Assert.That(_control.Session.SkillRating, Is.EqualTo(1000));

            rating = 1250;
            Assert.That(_control.SkillAverage, Is.EqualTo(1250), "without a party the average is the own rating, read when asked");
            Assert.That(_control.Session.SkillRating, Is.EqualTo(1250));

            rating = null;
            _control.Tick(2f);
            Assert.That(_control.Session.SkillRating, Is.Null);
            Assert.That(_control.SkillAverage, Is.Null);

            _control.Dispose();
            rating = 900;
            Assert.That(_control.SkillAverage, Is.EqualTo(900), "a disposed control still hands over the own rating");
        }
    }
}
