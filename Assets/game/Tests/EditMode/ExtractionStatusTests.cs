using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class ExtractionStatusTests
    {
        private const string _ammo = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e001";
        private const string _bandage = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e002";

        [Test]
        public void StatusFromValuesIsTheSameAsFromTheSession()
        {
            var stash = new Stash(10, new ItemCatalog(new[] { new ItemSpec(_ammo, maxStack: 30), new ItemSpec(_bandage, maxStack: 5) }, InventoryRule.Default));
            stash.Add(_ammo, 30);
            var spec = new ExtractionSpec(raidSeconds: 60f, extractHoldSeconds: 5f, openPoints: 1, openAfterSeconds: 10f);
            var session = new ExtractionSession(spec, stash, new[] { "north" }, new[] { new ItemCount(_ammo, 20) });
            session.Pick(_bandage, 2);
            session.Tick(12f, null);
            session.Tick(2f, "north");
            var offline = new ExtractionStatus(session, false, true, "E");

            var fromValues = new ExtractionStatus(
                session.Outcome,
                session.RemainingSeconds,
                session.ElapsedSeconds,
                session.PointsOpen,
                session.SecondsUntilOpen,
                session.OpenPointIds.Count,
                session.HoldProgress,
                spec.ExtractHoldSeconds - session.HoldSeconds,
                false,
                true,
                "E",
                session.Carried,
                session.Stored,
                session.Lost);

            Assert.That(fromValues.Outcome, Is.EqualTo(offline.Outcome));
            Assert.That(fromValues.RemainingSeconds, Is.EqualTo(offline.RemainingSeconds));
            Assert.That(fromValues.ElapsedSeconds, Is.EqualTo(offline.ElapsedSeconds));
            Assert.That(fromValues.PointsOpen, Is.EqualTo(offline.PointsOpen));
            Assert.That(fromValues.SecondsUntilOpen, Is.EqualTo(offline.SecondsUntilOpen));
            Assert.That(fromValues.OpenPointCount, Is.EqualTo(offline.OpenPointCount));
            Assert.That(fromValues.IsHolding, Is.EqualTo(offline.IsHolding));
            Assert.That(fromValues.IsHolding, Is.True);
            Assert.That(fromValues.HoldProgress, Is.EqualTo(offline.HoldProgress));
            Assert.That(fromValues.HoldRemainingSeconds, Is.EqualTo(offline.HoldRemainingSeconds));
            Assert.That(fromValues.StandingOnClosedPoint, Is.EqualTo(offline.StandingOnClosedPoint));
            Assert.That(fromValues.LootInReach, Is.EqualTo(offline.LootInReach));
            Assert.That(fromValues.InteractKeyLabel, Is.EqualTo(offline.InteractKeyLabel));
            Assert.That(fromValues.CarriedCount, Is.EqualTo(offline.CarriedCount));
            Assert.That(fromValues.CarriedCount, Is.EqualTo(22));
            Assert.That(fromValues.StoredCount, Is.EqualTo(offline.StoredCount));
            Assert.That(fromValues.LostCount, Is.EqualTo(offline.LostCount));
        }

        [Test]
        public void AnEndedRaidHoldsNothingAndPromptsNothing()
        {
            var status = new ExtractionStatus(
                ExtractionOutcome.Died, 0f, 60f, true, 0f, 1, 0.5f, 2f, true, true, null, null, null, new[] { new ItemCount(_ammo, 3) });

            Assert.That(status.IsOver, Is.True);
            Assert.That(status.IsHolding, Is.False);
            Assert.That(status.HoldProgress, Is.Zero);
            Assert.That(status.HoldRemainingSeconds, Is.Zero);
            Assert.That(status.StandingOnClosedPoint, Is.False);
            Assert.That(status.LootInReach, Is.False);
            Assert.That(status.Carried, Is.Empty);
            Assert.That(status.LostCount, Is.EqualTo(3));
        }

        [Test]
        public void ClosedPointPromptIsHiddenWhileHolding()
        {
            var status = new ExtractionStatus(
                ExtractionOutcome.Raiding, 30f, 30f, false, 5f, 0, 0.25f, 3f, true, false, null, null, null, null);

            Assert.That(status.IsHolding, Is.True);
            Assert.That(status.StandingOnClosedPoint, Is.False);
            Assert.That(status.SecondsUntilOpen, Is.EqualTo(5f));
        }
    }
}
