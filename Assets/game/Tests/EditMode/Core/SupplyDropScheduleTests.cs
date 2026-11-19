using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SupplyDropScheduleTests
    {
        private const float _tolerance = 1e-4f;

        [Test]
        public void TheDefaultsDropMoreAsTheZoneShrinks()
        {
            var spec = SupplyDropSpec.Default;

            Assert.That(spec.Stages.Count, Is.EqualTo(3));
            Assert.That(spec.Stages[0].Count, Is.EqualTo(1));
            Assert.That(spec.Stages[1].Count, Is.EqualTo(2));
            Assert.That(spec.Stages[2].IntervalSeconds, Is.EqualTo(8f));
            Assert.That(spec.TableId, Is.EqualTo(string.Empty));
            Assert.That(spec.UsesTable, Is.False);
            Assert.That(spec.DropHeight, Is.EqualTo(40f));
            Assert.That(spec.FallSpeed, Is.EqualTo(6f));
            Assert.That(spec.PlaceFraction, Is.EqualTo(0.8f));
            Assert.That(spec.Reach, Is.EqualTo(2.5f));
            Assert.That(spec.BotSearchRadius, Is.EqualTo(80f));
        }

        [Test]
        public void PhasesPastTheListRepeatTheLastStage()
        {
            var spec = new SupplyDropSpec(new[] { new SupplyDropStage(1, 0f, 1f), new SupplyDropStage(3, 2f, 4f) });

            Assert.That(spec.StageFor(0).Count, Is.EqualTo(1));
            Assert.That(spec.StageFor(1).Count, Is.EqualTo(3));
            Assert.That(spec.StageFor(7), Is.SameAs(spec.Stages[1]));
            Assert.That(new SupplyDropSpec(Array.Empty<SupplyDropStage>()).StageFor(0), Is.Null, "an empty list drops nothing");
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.StageFor(-1));
        }

        [Test]
        public void TheFirstCrateWaitsForTheDelayAndTheRestFollowAtTheInterval()
        {
            var schedule = new SupplyDropSchedule(new SupplyDropSpec(new[] { new SupplyDropStage(3, 5f, 10f) }));

            Assert.That(schedule.BeginStage(0), Is.EqualTo(3));
            Assert.That(schedule.IsDropping, Is.True);
            Assert.That(schedule.SecondsUntilNext, Is.EqualTo(5f));

            Assert.That(schedule.Tick(4.9f), Is.EqualTo(0));
            Assert.That(schedule.Tick(0.1f), Is.EqualTo(1));
            Assert.That(schedule.Remaining, Is.EqualTo(2));
            Assert.That(schedule.SecondsUntilNext, Is.EqualTo(10f).Within(_tolerance));

            Assert.That(schedule.Tick(9f), Is.EqualTo(0));
            Assert.That(schedule.Tick(1f), Is.EqualTo(1));
            Assert.That(schedule.Tick(100f), Is.EqualTo(1), "only as many as the stage has");
            Assert.That(schedule.DroppedThisStage, Is.EqualTo(3));
            Assert.That(schedule.IsDropping, Is.False);
            Assert.That(schedule.Tick(100f), Is.EqualTo(0));
        }

        [Test]
        public void ALongFrameDropsEveryCrateThatCameDueInIt()
        {
            var schedule = new SupplyDropSchedule(new SupplyDropSpec(new[] { new SupplyDropStage(4, 0f, 2f) }));
            schedule.BeginStage(0);

            Assert.That(schedule.Tick(0f), Is.EqualTo(1), "no delay drops the first at once");
            Assert.That(schedule.Tick(4.5f), Is.EqualTo(2));
            Assert.That(schedule.Remaining, Is.EqualTo(1));
            Assert.That(schedule.SecondsUntilNext, Is.EqualTo(1.5f).Within(_tolerance));
        }

        [Test]
        public void ANewStageDropsWhatIsLeftOfThePreviousOne()
        {
            var schedule = new SupplyDropSchedule(new SupplyDropSpec(new[] { new SupplyDropStage(5, 1f, 1f), new SupplyDropStage(2, 3f, 1f) }));
            schedule.BeginStage(0);
            schedule.Tick(1f);

            Assert.That(schedule.BeginStage(1), Is.EqualTo(2));
            Assert.That(schedule.StageIndex, Is.EqualTo(1));
            Assert.That(schedule.DroppedThisStage, Is.EqualTo(0));
            Assert.That(schedule.SecondsUntilNext, Is.EqualTo(3f));

            schedule.Stop();
            Assert.That(schedule.StageIndex, Is.EqualTo(SupplyDropSchedule.NoStage));
            Assert.That(schedule.IsDropping, Is.False);
            Assert.That(schedule.Tick(10f), Is.EqualTo(0));
        }

        [Test]
        public void ACountOfZeroSkipsTheStage()
        {
            var schedule = new SupplyDropSchedule(new SupplyDropSpec(new[] { new SupplyDropStage(0, 0f, 1f) }));

            Assert.That(schedule.BeginStage(0), Is.EqualTo(0));
            Assert.That(schedule.Tick(5f), Is.EqualTo(0));
        }

        [Test]
        public void TheScheduleRejectsInvalidInput()
        {
            var schedule = new SupplyDropSchedule(SupplyDropSpec.Default);

            Assert.Throws<ArgumentNullException>(() => new SupplyDropSchedule(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Tick(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.BeginStage(-1));
        }

        [Test]
        public void PickedPointsStayInsideTheFractionOfTheCircle()
        {
            var random = new Random(3);
            var center = new Vector2(10f, -4f);
            var far = 0f;
            for (var i = 0; i < 500; i++)
            {
                var point = SupplyDropSchedule.PickPoint(center, 20f, 0.5f, random);
                var distance = Vector2.Distance(point, center);
                Assert.That(distance, Is.LessThanOrEqualTo(10f + _tolerance));
                far = Math.Max(far, distance);
            }

            Assert.That(far, Is.GreaterThan(8f), "points reach out toward the edge, not only the middle");
            Assert.That(SupplyDropSchedule.PickPoint(center, 20f, 0f, random), Is.EqualTo(center));
        }

        [Test]
        public void TheSameSeedPicksTheSamePoints()
        {
            var a = SupplyDropSchedule.PickPoint(Vector2.Zero, 30f, 0.8f, new Random(42));
            var b = SupplyDropSchedule.PickPoint(Vector2.Zero, 30f, 0.8f, new Random(42));

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void PickPointRejectsInvalidInput()
        {
            var random = new Random(1);

            Assert.Throws<ArgumentNullException>(() => SupplyDropSchedule.PickPoint(Vector2.Zero, 1f, 1f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => SupplyDropSchedule.PickPoint(Vector2.Zero, -1f, 1f, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => SupplyDropSchedule.PickPoint(Vector2.Zero, float.PositiveInfinity, 1f, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => SupplyDropSchedule.PickPoint(Vector2.Zero, 1f, 1.5f, random));
        }

        [Test]
        public void TheSpecRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyDropStage(-1, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyDropStage(1, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyDropStage(1, 0f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyDropSpec(dropHeight: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyDropSpec(fallSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyDropSpec(placeFraction: 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyDropSpec(reach: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyDropSpec(botSearchRadius: float.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => new SupplyDropSpec(new SupplyDropStage[] { null }));
            Assert.That(new SupplyDropSpec(tableId: "  crates  ").TableId, Is.EqualTo("crates"));
        }

        [Test]
        public void TheJsonReadsEveryItemAndLeavesOutTheRestAsDefaults()
        {
            const string text = "{ \"stages\": [ { \"count\": 2, \"delaySeconds\": 1.5, \"intervalSeconds\": 4 }, { \"count\": 0, \"intervalSeconds\": 1 } ],"
                + " \"tableId\": \"supply\", \"dropHeight\": 30, \"fallSpeed\": 10, \"placeFraction\": 0.5, \"reach\": 3, \"botSearchRadius\": 50 }";

            var spec = SupplyDropJson.Parse(text, "test");

            Assert.That(spec.Stages.Count, Is.EqualTo(2));
            Assert.That(spec.Stages[0].Count, Is.EqualTo(2));
            Assert.That(spec.Stages[0].DelaySeconds, Is.EqualTo(1.5f));
            Assert.That(spec.Stages[0].IntervalSeconds, Is.EqualTo(4f));
            Assert.That(spec.Stages[1].Count, Is.EqualTo(0));
            Assert.That(spec.Stages[1].DelaySeconds, Is.EqualTo(0f));
            Assert.That(spec.TableId, Is.EqualTo("supply"));
            Assert.That(spec.DropHeight, Is.EqualTo(30f));
            Assert.That(spec.FallSpeed, Is.EqualTo(10f));
            Assert.That(spec.PlaceFraction, Is.EqualTo(0.5f));
            Assert.That(spec.Reach, Is.EqualTo(3f));
            Assert.That(spec.BotSearchRadius, Is.EqualTo(50f));

            var empty = SupplyDropJson.Parse("{}", "test");
            Assert.That(empty.Stages, Is.SameAs(SupplyDropSpec.DefaultStages));
            Assert.That(empty.DropHeight, Is.EqualTo(SupplyDropSpec.DefaultDropHeight));
        }

        [Test]
        public void TheJsonNamesTheItemItCannotRead()
        {
            var missing = Assert.Throws<TuningJsonException>(() => SupplyDropJson.Parse("{ \"stages\": [ { \"count\": 1 } ] }", "test"));
            Assert.That(missing.Item, Is.EqualTo("stages[0].intervalSeconds"));

            var negative = Assert.Throws<TuningJsonException>(() => SupplyDropJson.Parse("{ \"fallSpeed\": -2 }", "test"));
            Assert.That(negative.Item, Is.EqualTo("fallSpeed"));

            var unknown = Assert.Throws<TuningJsonException>(() => SupplyDropJson.Parse("{ \"speed\": 2 }", "test"));
            Assert.That(unknown.Message, Does.Contain("speed"));
        }

        [Test]
        public void TheLogLinesAreSpaceSeparatedKeyValues()
        {
            Assert.That(SupplyDropLog.Stage(0, 2), Is.EqualTo("[SupplyDrop] stage phase=1 crates=2"));
            Assert.That(
                SupplyDropLog.Dropped(3, new Vector3(1f, 42.5f, -2f), 2.5f),
                Is.EqualTo("[SupplyDrop] dropped crate=3 at=(1.00,42.50,-2.00) ground=2.50"));
            Assert.That(SupplyDropLog.Landed(3, new Vector3(1f, 2.5f, -2f)), Is.EqualTo("[SupplyDrop] landed crate=3 at=(1.00,2.50,-2.00)"));
            Assert.That(
                SupplyDropLog.Opened(3, 7, new[] { new ItemCount("a", 2) }, 1),
                Is.EqualTo("[SupplyDrop] opened crate=3 by=7 items=ax2 extras=1"));
            Assert.That(SupplyDropLog.Opened(3, 7, Array.Empty<ItemCount>(), 0), Is.EqualTo("[SupplyDrop] opened crate=3 by=7 items=- extras=0"));
            Assert.That(SupplyDropLog.UnknownTable("my table"), Does.StartWith("[SupplyDrop] tableId=my_table is not"));
            Assert.That(BattleRoyaleLog.CrateOpened(2, 3, 4), Is.EqualTo("[Mode] crate-opened player=2 crate=3 pickups=4"));
            Assert.Throws<ArgumentNullException>(() => SupplyDropLog.Opened(1, 1, null, 0));
        }
    }
}
