using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientEconomyTests
    {
        private ClientEconomy _economy;

        [SetUp]
        public void SetUp()
        {
            _economy = new ClientEconomy(16000);
        }

        [Test]
        public void TheMaxMustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientEconomy(0));
            Assert.That(_economy.MaxMoney, Is.EqualTo(16000));
            Assert.Throws<ArgumentNullException>(() => _economy.Apply(null));
        }

        [Test]
        public void TheOwnersMoneyIsShownWithTheMaxAndTheBuyTimeOfThePreparation()
        {
            Assert.That(_economy.Apply(Snapshot(800, RoundPhase.Preparing)), Is.True);

            Assert.That(_economy.Status, Is.EqualTo((WalletStatus?)new WalletStatus(800, 16000, true)));
        }

        [TestCase(RoundPhase.Waiting)]
        [TestCase(RoundPhase.Live)]
        [TestCase(RoundPhase.Ended)]
        public void OutsideThePreparationTheWalletCannotBuy(RoundPhase phase)
        {
            _economy.Apply(Snapshot(800, phase));

            Assert.That(_economy.Status, Is.EqualTo((WalletStatus?)new WalletStatus(800, 16000, false)));
        }

        [Test]
        public void AnEndedMatchCannotBuyEvenInThePreparation()
        {
            var snapshot = Snapshot(800, RoundPhase.Preparing);
            snapshot.Match.State = GameModeState.Ended;

            _economy.Apply(snapshot);

            Assert.That(_economy.Status.Value.CanBuy, Is.False);
        }

        [Test]
        public void ASnapshotWithoutTheRoundCannotBuy()
        {
            var snapshot = Snapshot(800, RoundPhase.Preparing);
            snapshot.Match.HasRound = false;

            _economy.Apply(snapshot);

            Assert.That(_economy.Status.Value.CanBuy, Is.False);
        }

        [Test]
        public void TheSameMoneyAndPhaseKeepTheStatusAndAChangeReplacesIt()
        {
            _economy.Apply(Snapshot(800, RoundPhase.Preparing));

            Assert.That(_economy.Apply(Snapshot(800, RoundPhase.Preparing)), Is.False);
            Assert.That(_economy.Apply(Snapshot(300, RoundPhase.Preparing)), Is.True, "a purchase lowers the money");
            Assert.That(_economy.Status.Value.Money, Is.EqualTo(300));
            Assert.That(_economy.Apply(Snapshot(300, RoundPhase.Live)), Is.True, "the round going live closes the buy time");
            Assert.That(_economy.Status.Value.CanBuy, Is.False);
        }

        [Test]
        public void NoWalletIsShownUntilAnyMoneyArrives()
        {
            Assert.That(_economy.Apply(Snapshot(0, RoundPhase.Preparing)), Is.False, "a server without an economy sends no money");
            Assert.That(_economy.Status, Is.Null);

            Assert.That(_economy.Apply(Snapshot(300, RoundPhase.Live)), Is.True);
            Assert.That(_economy.Apply(Snapshot(0, RoundPhase.Preparing)), Is.True, "spending everything keeps the wallet at zero");
            Assert.That(_economy.Status, Is.EqualTo((WalletStatus?)new WalletStatus(0, 16000, true)));
        }

        [Test]
        public void OtherObjectivesHaveNoWallet()
        {
            _economy.Apply(Snapshot(800, RoundPhase.Preparing));
            var other = Snapshot(800, RoundPhase.Preparing);
            other.Match.Objective = MatchObjective.Elimination;

            Assert.That(_economy.Apply(other), Is.True);

            Assert.That(_economy.Status, Is.Null);
            Assert.That(_economy.Apply(Snapshot(0, RoundPhase.Preparing)), Is.False, "the next match has to send money again");
        }

        [Test]
        public void ASnapshotWithoutTheOwnerKeepsTheLastWallet()
        {
            _economy.Apply(Snapshot(800, RoundPhase.Preparing));
            var spectating = Snapshot(0, RoundPhase.Live);
            spectating.HasOwner = false;

            Assert.That(_economy.Apply(spectating), Is.False);

            Assert.That(_economy.Status.Value.Money, Is.EqualTo(800));
        }

        [Test]
        public void MoneyAboveTheLocalMaxRaisesTheMaxInsteadOfFailing()
        {
            var economy = new ClientEconomy(1000);

            economy.Apply(Snapshot(4200, RoundPhase.Preparing));

            Assert.That(economy.Status, Is.EqualTo((WalletStatus?)new WalletStatus(4200, 4200, true)));
            Assert.That(economy.Status.Value.IsFull, Is.True);
        }

        [Test]
        public void ResetDropsTheWallet()
        {
            _economy.Apply(Snapshot(800, RoundPhase.Preparing));

            Assert.That(_economy.Reset(), Is.True);
            Assert.That(_economy.Status, Is.Null);
            Assert.That(_economy.Reset(), Is.False);
        }

        [Test]
        public void TheMoneyRoundTripsThroughTheSnapshotTable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(Snapshot(2700, RoundPhase.Preparing));
            var received = (SnapshotMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment());

            _economy.Apply(received);

            Assert.That(_economy.Status, Is.EqualTo((WalletStatus?)new WalletStatus(2700, 16000, true)));
        }

        [Test]
        public void ABuyRequestCarriesTheItemId()
        {
            var request = ClientEconomy.BuyRequest("6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83", out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(request.ItemId, Is.EqualTo("6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83"));
        }

        [Test]
        public void AnItemIdThatCannotBeSentMakesNoRequest()
        {
            Assert.That(ClientEconomy.BuyRequest(null, out var none), Is.Null);
            Assert.That(none, Is.EqualTo("no item id"));
            Assert.That(ClientEconomy.BuyRequest(string.Empty, out _), Is.Null);

            var longest = new string('a', MatchProtocol.MaxContentIdBytes);
            Assert.That(ClientEconomy.BuyRequest(longest, out _), Is.Not.Null);
            Assert.That(ClientEconomy.BuyRequest(longest + "a", out var tooLong), Is.Null);
            Assert.That(tooLong, Is.EqualTo("the item id is longer than 64 bytes"));
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
    }
}
