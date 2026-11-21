using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2HordePartTests
    {
        private const int Self = 1;

        private readonly List<IMessage> _sent = new List<IMessage>();
        private readonly List<string> _logs = new List<string>();
        private bool _pressed;
        private bool _canAct;
        private string _key;
        private NetV2HordePart _part;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _logs.Clear();
            _pressed = false;
            _canAct = true;
            _key = "F";
            GameSignals.ModeStatus.Clear();
            _part = new NetV2HordePart(() => Self, Send, () => _canAct, () => _pressed, () => _key, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            GameSignals.ModeStatus.Clear();
        }

        [Test]
        public void AHordeSnapshotIsPublishedAsTheModeStatusWithTheKeyOnTheOffer()
        {
            _part.Apply(Resting(HordePurchaseVerdict.Ok));

            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(_part.Status.Status));
            var horde = (HordeStatus)GameSignals.ModeStatus.Value.Detail;
            Assert.That(horde.WaveNumber, Is.EqualTo(1));
            Assert.That(horde.Currency, Is.EqualTo(50));
            Assert.That(horde.Offer.Value.StructureName, Is.EqualTo("Wall"));
            Assert.That(horde.Offer.Value.KeyLabel, Is.EqualTo("F"));
        }

        [Test]
        public void TheSameValuesKeepTheStatusThatWasPublished()
        {
            _part.Apply(Resting(HordePurchaseVerdict.Ok));
            var first = GameSignals.ModeStatus.Value;

            _part.Apply(Resting(HordePurchaseVerdict.Ok));

            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(first));
        }

        [Test]
        public void PressingInteractOnAPayableOfferSendsTheFortifyRequest()
        {
            _part.Apply(Resting(HordePurchaseVerdict.Ok));

            _part.Tick(0.016f);
            Assert.That(_sent, Is.Empty, "nothing is sent without pressing Interact");
            Assert.That(KeySharing.IsClaimed(InputActionPaths.Interact), Is.True, "the offer takes the Interact key");

            _pressed = true;
            _part.Tick(0.016f);

            Assert.That(_sent.Count, Is.EqualTo(1));
            Assert.That(_sent[0], Is.InstanceOf<HordeFortifyRequestMessage>());
            Assert.That(_logs, Does.Contain("[Net] horde fortify request action=Repair structure=Wall cost=8 sent=True"));
        }

        [Test]
        public void PressingInteractDuringAWaveSendsNothingAndLeavesTheKey()
        {
            _part.Apply(Resting(HordePurchaseVerdict.WaveInProgress));
            _pressed = true;

            _part.Tick(0.016f);

            Assert.That(_sent, Is.Empty);
            Assert.That(KeySharing.IsClaimed(InputActionPaths.Interact), Is.False);
            Assert.That(_part.Status.Horde.Offer.Value.Verdict, Is.EqualTo(HordePurchaseVerdict.WaveInProgress), "the offer is still shown");
        }

        [Test]
        public void PressingInteractWhileUnableToActSendsNothing()
        {
            _part.Apply(Resting(HordePurchaseVerdict.Ok));
            _pressed = true;
            _canAct = false;

            _part.Tick(0.016f);

            Assert.That(_sent, Is.Empty);
            Assert.That(KeySharing.IsClaimed(InputActionPaths.Interact), Is.False);
        }

        [Test]
        public void AReplayShowsTheOfferWithoutAKeyAndNeverSends()
        {
            _part.Dispose();
            _part = new NetV2HordePart(() => Self, null, () => true, () => true, () => "F", _logs.Add);

            _part.Apply(Resting(HordePurchaseVerdict.Ok));
            _part.Tick(0.016f);

            Assert.That(_part.Status.Horde.Offer.Value.KeyLabel, Is.Empty);
            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Is.Empty);
            Assert.That(KeySharing.IsClaimed(InputActionPaths.Interact), Is.False);
        }

        [Test]
        public void LeavingHordeClearsThePublishedStatus()
        {
            _part.Apply(Resting(HordePurchaseVerdict.Ok));
            var other = Resting(HordePurchaseVerdict.Ok);
            other.Match.Objective = MatchObjective.Payload;

            _part.Apply(other);

            Assert.That(_part.Status, Is.Null);
            Assert.That(GameSignals.ModeStatus.HasValue, Is.False);
        }

        [Test]
        public void AnotherPublishersStatusIsNotClearedOnDispose()
        {
            _part.Apply(Resting(HordePurchaseVerdict.Ok));
            var others = new ModeStatus(null, Array.Empty<SideScore>(), null, null, false, null, null);
            GameSignals.ModeStatus.Publish(others);

            _part.Dispose();

            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(others));
        }

        [Test]
        public void DisposeClearsItsStatusAndReleasesTheKey()
        {
            _part.Apply(Resting(HordePurchaseVerdict.Ok));
            _part.Tick(0.016f);

            _part.Dispose();

            Assert.That(GameSignals.ModeStatus.HasValue, Is.False);
            Assert.That(KeySharing.IsClaimed(InputActionPaths.Interact), Is.False);
        }

        [Test]
        public void TheRegistrationRefusesANullContext()
        {
            Assert.Throws<ArgumentNullException>(() => NetV2HordePart.Create(null));
        }

        [Test]
        public void RefusesMissingPieces()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2HordePart(null, Send, () => true));
            Assert.Throws<ArgumentNullException>(() => new NetV2HordePart(() => Self, Send, null));
        }

        private bool Send(IMessage message)
        {
            _sent.Add(message);
            return true;
        }

        private static SnapshotMessage Resting(HordePurchaseVerdict verdict)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            ref var match = ref snapshot.Match;
            match.State = GameModeState.Playing;
            match.Objective = MatchObjective.Horde;
            match.AlphaPoints = 50;
            match.BravoPoints = 0;
            match.RoundNumber = 1;
            match.RoundsToWin = 5;
            match.NextWaveSeconds = 4;
            match.ObjectiveRemainingSeconds = 10;
            ref var self = ref snapshot.Players.Add();
            self.PlayerId = Self;
            self.Team = Team.Alpha;
            self.Health = 100;
            self.WeaponId = "";
            snapshot.HasOwner = true;
            snapshot.Owner.HordeOffer = new HordeOfferSync(2, HordeFortificationAction.Repair, "Wall", 8, verdict);
            return snapshot;
        }
    }
}
