using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BattleRoyaleBotPlannerTests
    {
        private const float _tolerance = 1e-4f;
        private const float _searchRadius = 10f;

        private static readonly BotPickupOption[] _noPickups = Array.Empty<BotPickupOption>();
        private static readonly Vector3[] _noDowned = Array.Empty<Vector3>();

        // 今の円は原点の半径 20、次の円は (5, 0) の半径 10 で、待っている。
        private static readonly ZoneState _zone = new ZoneState(
            0, ZoneStage.Waiting, Vector2.Zero, 20f, new Vector2(5f, 0f), 10f, 1f, 10f, 10f);

        [Test]
        public void OutsideTheCurrentCircleTheBotHeadsIntoTheNextOneEvenInAFight()
        {
            var position = new Vector3(30f, 1f, 0f);
            var weapon = new[] { new BotPickupOption(new Vector3(1f, 0f, 0f), true) };

            var errand = BattleRoyaleBotPlanner.Plan(position, _zone, false, weapon, _noDowned, _searchRadius);

            Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.EnterZone));
            Assert.That(errand.OverridesCombat, Is.True);
            Assert.That(errand.Destination.X, Is.EqualTo(10f).Within(_tolerance), "half the next radius from its centre, on the bot's side");
            Assert.That(errand.Destination.Y, Is.EqualTo(1f));
            Assert.That(errand.Destination.Z, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void ADownedSquadmateInReachComesBeforePickups()
        {
            var position = new Vector3(4f, 0f, 0f);
            var downed = new[] { new Vector3(4f, 0f, 30f), new Vector3(6f, 0f, 3f) };
            var pickups = new[] { new BotPickupOption(new Vector3(5f, 0f, 0f), true) };

            var errand = BattleRoyaleBotPlanner.Plan(position, _zone, false, pickups, downed, _searchRadius);

            Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.Revive));
            Assert.That(errand.TargetIndex, Is.EqualTo(1), "the first one is outside the zone");
            Assert.That(errand.OverridesCombat, Is.False);
        }

        [Test]
        public void AnUnarmedBotGoesForTheNearestWeaponInsideTheZoneHoweverFar()
        {
            var position = new Vector3(5f, 0f, 0f);
            var pickups = new[]
            {
                new BotPickupOption(new Vector3(6f, 0f, 0f), false),
                new BotPickupOption(new Vector3(-18f, 0f, 0f), true),
                new BotPickupOption(new Vector3(5f, 0f, 30f), true),
            };

            var errand = BattleRoyaleBotPlanner.Plan(position, _zone, false, pickups, _noDowned, _searchRadius);

            Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.Collect));
            Assert.That(errand.TargetIndex, Is.EqualTo(1));
            Assert.That(errand.OverridesCombat, Is.True);
        }

        [Test]
        public void InsideTheCurrentButOutsideTheNextCircleTheBotMovesInWithoutLeavingAFight()
        {
            var position = new Vector3(-15f, 0f, 0f);

            var errand = BattleRoyaleBotPlanner.Plan(position, _zone, true, _noPickups, _noDowned, _searchRadius);

            Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.EnterZone));
            Assert.That(errand.OverridesCombat, Is.False);
            Assert.That(errand.Destination.X, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void AnArmedBotInsideTheNextCirclePicksUpWhatIsNearby()
        {
            var position = new Vector3(5f, 0f, 0f);
            var pickups = new[]
            {
                new BotPickupOption(new Vector3(5f, 0f, 12f), false),
                new BotPickupOption(new Vector3(8f, 0f, 0f), false),
            };

            var errand = BattleRoyaleBotPlanner.Plan(position, _zone, true, pickups, _noDowned, _searchRadius);

            Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.Collect));
            Assert.That(errand.TargetIndex, Is.EqualTo(1), "the first one is out of the search radius");
            Assert.That(errand.OverridesCombat, Is.False);
        }

        [Test]
        public void NothingToDoLeavesTheBotToItsOwnJudgement()
        {
            var errand = BattleRoyaleBotPlanner.Plan(new Vector3(5f, 0f, 0f), _zone, true, _noPickups, _noDowned, _searchRadius);

            Assert.That(errand.HasErrand, Is.False);
            Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.None));
        }

        [Test]
        public void WithoutAZoneEverywhereCountsAsInside()
        {
            var pickups = new[] { new BotPickupOption(new Vector3(100f, 0f, 0f), true) };

            var errand = BattleRoyaleBotPlanner.Plan(new Vector3(90f, 0f, 0f), null, false, pickups, _noDowned, _searchRadius);

            Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.Collect));
        }

        [Test]
        public void InvalidArgumentsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => BattleRoyaleBotPlanner.Plan(Vector3.Zero, null, true, null, _noDowned, 1f));
            Assert.Throws<ArgumentNullException>(() => BattleRoyaleBotPlanner.Plan(Vector3.Zero, null, true, _noPickups, null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleBotPlanner.Plan(Vector3.Zero, null, true, _noPickups, _noDowned, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BattleRoyaleBotPlanner.Plan(Vector3.Zero, null, true, _noPickups, _noDowned, float.PositiveInfinity));
        }
    }
}
