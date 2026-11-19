using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class InteractHintSlotTests
    {
        private static readonly AmmoCrateHint _room = new AmmoCrateHint(true, "Player/Interact");
        private static readonly AmmoCrateHint _full = new AmmoCrateHint(false, "Player/Interact");

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            KeySharing.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            KeySharing.Reset();
        }

        [Test]
        public void ShowPublishesOnlyWhenTheHintChanges()
        {
            var source = new object();
            var slot = new InteractHintSlot(source);
            var published = 0;
            GameSignals.InteractHint.Changed += _ => published++;

            slot.Show(_room);
            slot.Show(_room);
            Assert.That(published, Is.EqualTo(1), "showing the same hint every frame publishes it once");
            Assert.That(GameSignals.InteractHint.Value, Is.EqualTo(new InteractHint(source, _room)));

            slot.Show(_full);
            Assert.That(published, Is.EqualTo(2));
            Assert.That(GameSignals.InteractHint.Value?.Detail, Is.EqualTo(_full));
        }

        [Test]
        public void AShownHintClaimsInteractUntilEverySlotClears()
        {
            var first = new InteractHintSlot(new object());
            var second = new InteractHintSlot(new object());
            Assert.That(KeySharing.IsClaimed("Interact"), Is.False);

            first.Show(_room);
            second.Show(_full);
            Assert.That(KeySharing.IsClaimed("Interact"), Is.True);

            first.Clear();
            Assert.That(KeySharing.IsClaimed("Interact"), Is.True, "the other slot still offers an interaction");

            second.Clear();
            Assert.That(KeySharing.IsClaimed("Interact"), Is.False);
        }

        [Test]
        public void ClearRemovesOnlyItsOwnHint()
        {
            var first = new InteractHintSlot(new object());
            var second = new InteractHintSlot(new object());

            first.Show(_room);
            second.Show(_full);
            first.Clear();
            Assert.That(GameSignals.InteractHint.Value?.Detail, Is.EqualTo(_full), "the newer hint stays");

            second.Clear();
            Assert.That(GameSignals.InteractHint.HasValue, Is.False);
            Assert.DoesNotThrow(() => second.Clear());
        }

        [Test]
        public void AReplacedHintIsShownAgainOnceTheSignalIsEmpty()
        {
            var firstSource = new object();
            var first = new InteractHintSlot(firstSource);
            var second = new InteractHintSlot(new object());

            first.Show(_room);
            second.Show(_full);
            first.Show(_room);
            Assert.That(GameSignals.InteractHint.Value?.Detail, Is.EqualTo(_full), "an unchanged hint does not take the signal back");

            second.Clear();
            first.Show(_room);
            Assert.That(GameSignals.InteractHint.Value, Is.EqualTo(new InteractHint(firstSource, _room)));
        }

        [Test]
        public void NullsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new InteractHintSlot(null));
            Assert.Throws<ArgumentNullException>(() => new InteractHintSlot(new object()).Show(null));
        }
    }
}
