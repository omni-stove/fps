using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ObjectiveZoneTests
    {
        [Test]
        public void RejectsUnknownObjective()
        {
            var mission = TwoStep();

            Assert.Throws<ArgumentNullException>(() => new ObjectiveZone(null, "a", ObjectiveZoneMode.Reach));
            Assert.Throws<ArgumentNullException>(() => new ObjectiveZone(mission, null, ObjectiveZoneMode.Reach));
            Assert.Throws<ArgumentException>(() => new ObjectiveZone(mission, "missing", ObjectiveZoneMode.Reach));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectiveZone(mission, "a", (ObjectiveZoneMode)99));
        }

        [Test]
        public void ReachProgressesOnEnter()
        {
            var mission = TwoStep();
            using var zone = new ObjectiveZone(mission, "a", ObjectiveZoneMode.Reach);

            zone.Enter();

            Assert.That(mission.GetState("a"), Is.EqualTo(ObjectiveState.Completed));
        }

        [Test]
        public void ReachWhileLockedProgressesWhenTheObjectiveBecomesActive()
        {
            var mission = TwoStep();
            using var zone = new ObjectiveZone(mission, "b", ObjectiveZoneMode.Reach);

            zone.Enter();
            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Locked));

            mission.Progress("a");

            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Completed));
            Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
        }

        [Test]
        public void ReachAfterLeavingDoesNotProgressOnActivation()
        {
            var mission = TwoStep();
            using var zone = new ObjectiveZone(mission, "b", ObjectiveZoneMode.Reach);

            zone.Enter();
            zone.Exit();
            mission.Progress("a");

            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Active));
        }

        [Test]
        public void ExitDoesNotGoBelowZero()
        {
            using var zone = new ObjectiveZone(TwoStep(), "a", ObjectiveZoneMode.Interact);

            zone.Exit();
            zone.Enter();
            zone.Enter();
            zone.Exit();

            Assert.That(zone.Occupants, Is.EqualTo(1));
            Assert.That(zone.IsOccupied, Is.True);
        }

        [Test]
        public void InteractNeedsOccupantAndActiveObjective()
        {
            var mission = TwoStep();
            using var zone = new ObjectiveZone(mission, "b", ObjectiveZoneMode.Interact);

            Assert.That(zone.Interact(), Is.False, "nobody is inside");
            zone.Enter();
            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Locked), "entering alone does not progress");
            Assert.That(zone.Interact(), Is.False, "the objective is still locked");

            mission.Progress("a");
            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Active), "activation alone does not progress");

            Assert.That(zone.Interact(), Is.True);
            Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
        }

        [Test]
        public void ReachIgnoresInteract()
        {
            var mission = TwoStep();
            using var zone = new ObjectiveZone(mission, "b", ObjectiveZoneMode.Reach);
            zone.Enter();

            Assert.That(zone.Interact(), Is.False);
        }

        [Test]
        public void IgnoresInputAfterDispose()
        {
            var mission = TwoStep();
            var zone = new ObjectiveZone(mission, "b", ObjectiveZoneMode.Reach);
            zone.Enter();

            zone.Dispose();
            zone.Dispose();
            mission.Progress("a");
            zone.Enter();

            Assert.That(zone.Occupants, Is.Zero);
            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Active));
        }

        [Test]
        public void DoesNothingAfterTheMissionFinished()
        {
            var mission = TwoStep();
            using var zone = new ObjectiveZone(mission, "a", ObjectiveZoneMode.Reach);
            mission.Fail("a");

            zone.Enter();

            Assert.That(mission.GetState("a"), Is.EqualTo(ObjectiveState.Failed));
        }

        [Test]
        public void HasObjective()
        {
            var mission = TwoStep();

            Assert.That(mission.HasObjective("a"), Is.True);
            Assert.That(mission.HasObjective("missing"), Is.False);
            Assert.That(mission.HasObjective(null), Is.False);
        }

        private static readonly string[] _prerequisiteA = { "a" };

        private static Mission TwoStep()
        {
            return new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", prerequisites: _prerequisiteA),
            });
        }
    }
}
