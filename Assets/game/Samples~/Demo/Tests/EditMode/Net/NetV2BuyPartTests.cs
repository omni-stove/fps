using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2BuyPartTests
    {
        private const string _rifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";

        private readonly List<BuyRequestMessage> _sent = new List<BuyRequestMessage>();
        private readonly List<string> _logs = new List<string>();
        private NetV2BuyPart _part;
        private bool _connected;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Wallet.Clear();
            GameSignals.BuySender.Clear();
            _sent.Clear();
            _logs.Clear();
            _connected = true;
            _part = new NetV2BuyPart(Send, 16000, _logs.Add);
            _changes = 0;
            GameSignals.Wallet.Changed += CountChange;
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.Wallet.Changed -= CountChange;
            _part.Dispose();
            GameSignals.Wallet.Clear();
            GameSignals.BuySender.Clear();
        }

        [Test]
        public void NothingIsPublishedBeforeMoneyArrives()
        {
            _part.Apply(Snapshot(0, RoundPhase.Preparing));

            Assert.That(GameSignals.Wallet.HasValue, Is.False);
            Assert.That(GameSignals.BuySender.HasValue, Is.False, "a match without an economy offers nothing to buy");
        }

        [Test]
        public void TheOwnersMoneyPublishesTheWalletAndTheSender()
        {
            _part.Apply(Snapshot(800, RoundPhase.Preparing));

            var expected = new WalletStatus(800, 16000, true);
            Assert.That(GameSignals.Wallet.Value, Is.EqualTo((WalletStatus?)expected));
            Assert.That(GameSignals.BuySender.Value, Is.SameAs(_part.BuySender));
            Assert.That(_logs, Is.EqualTo(new[] { EconomyLog.NetWallet(expected) }));
        }

        [Test]
        public void TheSameWalletIsNotPublishedAgain()
        {
            _part.Apply(Snapshot(800, RoundPhase.Preparing));
            _part.Apply(Snapshot(800, RoundPhase.Preparing));
            _part.Apply(Snapshot(800, RoundPhase.Live));

            Assert.That(_changes, Is.EqualTo(2));
            Assert.That(GameSignals.Wallet.Value, Is.EqualTo((WalletStatus?)new WalletStatus(800, 16000, false)));
        }

        [Test]
        public void BuyingThroughTheSenderSendsTheItemId()
        {
            _part.Apply(Snapshot(800, RoundPhase.Preparing));

            GameSignals.BuySender.Value.Buy(_rifleId);

            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].ItemId, Is.EqualTo(_rifleId));
            Assert.That(_logs, Has.Member(EconomyLog.NetBuy(_rifleId, true)));
        }

        [Test]
        public void ABuyThatCouldNotBeSentIsLogged()
        {
            _part.Apply(Snapshot(800, RoundPhase.Preparing));
            _connected = false;

            _part.BuySender.Buy(_rifleId);

            Assert.That(_logs, Has.Member(EconomyLog.NetBuy(_rifleId, false)));
        }

        [Test]
        public void AnEmptyItemIdIsNotSent()
        {
            _part.Apply(Snapshot(800, RoundPhase.Preparing));

            _part.BuySender.Buy(string.Empty);

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Member(EconomyLog.NetBuyRefused(string.Empty, "no item id")));
        }

        [Test]
        public void BuyingWithoutAnEconomyIsNotSent()
        {
            _part.BuySender.Buy(_rifleId);

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Member(EconomyLog.NetBuyRefused(_rifleId, "no economy in this match")));
        }

        [Test]
        public void LeavingTheEconomyClearsTheWalletAndTheSender()
        {
            _part.Apply(Snapshot(800, RoundPhase.Preparing));
            var other = Snapshot(800, RoundPhase.Preparing);
            other.Match.Objective = MatchObjective.Elimination;

            _part.Apply(other);

            Assert.That(GameSignals.Wallet.HasValue, Is.False);
            Assert.That(GameSignals.BuySender.HasValue, Is.False);
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo(EconomyLog.NetWallet(null)));
        }

        [Test]
        public void DisposeClearsWhatThisPartPublishedAndStopsSending()
        {
            _part.Apply(Snapshot(800, RoundPhase.Preparing));
            var sender = GameSignals.BuySender.Value;

            _part.Dispose();

            Assert.That(GameSignals.Wallet.HasValue, Is.False);
            Assert.That(GameSignals.BuySender.HasValue, Is.False);
            sender.Buy(_rifleId);
            Assert.That(_sent, Is.Empty, "a sender kept after leaving does not send");
            Assert.That(_logs, Has.Member(EconomyLog.NetBuyRefused(_rifleId, "left the match")));
        }

        [Test]
        public void ASnapshotAfterDisposeIsNotShown()
        {
            _part.Dispose();

            _part.Apply(Snapshot(800, RoundPhase.Preparing));

            Assert.That(GameSignals.Wallet.HasValue, Is.False);
            Assert.That(GameSignals.BuySender.HasValue, Is.False);
        }

        [Test]
        public void DisposeLeavesWhatSomeoneElsePublished()
        {
            _part.Apply(Snapshot(800, RoundPhase.Preparing));
            var other = new NetV2BuyPart(Send, 16000, _logs.Add);
            var otherWallet = new WalletStatus(300, 16000, false);
            GameSignals.Wallet.Publish(otherWallet);
            GameSignals.BuySender.Publish(other.BuySender);

            _part.Dispose();

            Assert.That(GameSignals.Wallet.Value, Is.EqualTo((WalletStatus?)otherWallet));
            Assert.That(GameSignals.BuySender.Value, Is.SameAs(other.BuySender));
            other.Dispose();
        }

        [Test]
        public void NoMessageIsTaken()
        {
            Assert.That(_part.Handle(new BuyRequestMessage { ItemId = _rifleId }), Is.False);
            Assert.That(_part.Handle(new VoteKickStateMessage()), Is.False);
        }

        private static SnapshotMessage Snapshot(int money, RoundPhase phase)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.Objective = MatchObjective.SearchAndDestroy;
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.HasRound = true;
            snapshot.Match.Round = phase;
            snapshot.HasOwner = true;
            snapshot.Owner.Money = money;
            return snapshot;
        }

        private bool Send(IMessage message)
        {
            if (!_connected)
            {
                return false;
            }

            _sent.Add((BuyRequestMessage)message);
            return true;
        }

        private void CountChange(WalletStatus? status)
        {
            _changes++;
        }
    }
}
