using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerVisibilityTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);
        private static readonly Vector3 _openEye = new Vector3(0f, 1.6f, 20f);

        [Test]
        public void ATargetInTheOpenIsSeenAndOneBehindTheWallIsNot()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 0f));

            Assert.That(See(visibility, Team.Alpha, 7, Open(), 0d), Is.True);
            Assert.That(See(visibility, Team.Alpha, 8, Hidden(), 0d), Is.False);
        }

        [Test]
        public void TheWallStillHidesATargetWellBehindItWithTheMarginDistance()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 1.5f, marginSeconds: 0f));

            Assert.That(See(visibility, Team.Alpha, 8, Hidden(), 0d), Is.False);
        }

        [Test]
        public void TheMarginDistanceSeesATargetAboutToStepOutFromTheEdge()
        {
            var nearEdge = new Vector3(10f, 1f, -0.5f);

            Assert.That(
                See(new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 0f)), Team.Alpha, 7, nearEdge, 0d),
                Is.False,
                "the straight line to the body crosses the wall");
            Assert.That(
                See(new ServerVisibility(Wall(), Conceal(marginDistance: 1.5f, marginSeconds: 0f)), Team.Alpha, 7, nearEdge, 0d),
                Is.True,
                "a point 1.5m to the side clears the edge of the wall");
        }

        [Test]
        public void ATargetBeyondTheMaxDistanceIsNotSeenEvenInTheOpen()
        {
            var visibility = new ServerVisibility(Empty(), Conceal(maxDistance: 50f, marginDistance: 0f, marginSeconds: 0f));

            Assert.That(See(visibility, Team.Alpha, 7, new Vector3(49f, 1.6f, 0f), 0d), Is.True);
            Assert.That(See(visibility, Team.Alpha, 7, new Vector3(51f, 1.6f, 0f), 0d), Is.False);
        }

        [Test]
        public void TheMarginDistanceAlsoWidensTheMaxDistance()
        {
            var visibility = new ServerVisibility(Empty(), Conceal(maxDistance: 50f, marginDistance: 2f, marginSeconds: 0f));

            Assert.That(See(visibility, Team.Alpha, 7, new Vector3(51.5f, 1.6f, 0f), 0d), Is.True);
            Assert.That(See(visibility, Team.Alpha, 7, new Vector3(52.5f, 1.6f, 0f), 0d), Is.False);
        }

        [Test]
        public void ATargetLastSeenWithinTheMarginSecondsStaysSeen()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 0.5f));

            Assert.That(See(visibility, Team.Alpha, 7, Open(), 1d), Is.True);
            Assert.That(See(visibility, Team.Alpha, 7, Hidden(), 1.3d), Is.True, "hidden 0.3s after it was last seen");
            Assert.That(See(visibility, Team.Alpha, 7, Hidden(), 1.5d), Is.True, "exactly at the margin");
            Assert.That(See(visibility, Team.Alpha, 7, Hidden(), 1.6d), Is.False, "hidden 0.6s after it was last seen");
        }

        [Test]
        public void SeeingAgainRestartsTheMarginSeconds()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 0.5f));

            See(visibility, Team.Alpha, 7, Open(), 0d);
            See(visibility, Team.Alpha, 7, Open(), 2d);

            Assert.That(See(visibility, Team.Alpha, 7, Hidden(), 2.4d), Is.True);
        }

        [Test]
        public void ATargetNeverSeenIsNotKeptByTheMarginSeconds()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 10f));

            Assert.That(See(visibility, Team.Alpha, 7, Hidden(), 0d), Is.False);
        }

        [Test]
        public void AnyEyeOfTheTeamIsEnough()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 0f));
            var behindWall = new Vector3(10f, 1.6f, -5f);

            Assert.That(
                visibility.CanSee(Team.Alpha, new[] { _eye }, 7, behindWall - new Vector3(0f, 0.6f, 0f), behindWall, 0d),
                Is.False);
            Assert.That(
                visibility.CanSee(Team.Alpha, new[] { _eye, new Vector3(9f, 1.6f, 0f) }, 7, behindWall - new Vector3(0f, 0.6f, 0f), behindWall, 0d),
                Is.True,
                "a teammate on the far side of the wall sees the target");
            Assert.That(visibility.CanSee(Team.Alpha, Array.Empty<Vector3>(), 9, Open(), Open(), 0d), Is.False, "a team with nobody alive sees nothing");
        }

        [Test]
        public void EachTeamRemembersOnItsOwn()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 1f));

            See(visibility, Team.Alpha, 7, Open(), 0d);

            Assert.That(See(visibility, Team.Bravo, 7, Hidden(), 0.2d), Is.False);
            Assert.That(See(visibility, Team.Alpha, 7, Hidden(), 0.2d), Is.True);
        }

        [Test]
        public void InFreeForAllEachPlayerIsATeamOfOne()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 1f));
            var target = Hidden();
            var targetEye = target + new Vector3(0f, 0.6f, 0f);

            Assert.That(visibility.CanSeeAlone(1, new Vector3(9f, 1.6f, 0f), 7, target, targetEye, 0d), Is.True);
            Assert.That(visibility.CanSeeAlone(2, _eye, 7, target, targetEye, 0d), Is.False, "another player does not share what player 1 saw");
            Assert.That(visibility.CanSeeAlone(1, _eye, 7, target, targetEye, 0.5d), Is.True, "player 1 keeps it for the margin seconds");
        }

        [Test]
        public void APlayerAloneAndATeamWithTheSameNumberDoNotShareMemory()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 1f));

            Assert.That(visibility.CanSeeAlone((int)Team.Alpha, _openEye, 7, Open(), Open() + new Vector3(0f, 0.6f, 0f), 0d), Is.True);

            Assert.That(See(visibility, Team.Alpha, 7, Hidden(), 0.2d), Is.False);
        }

        [Test]
        public void ForgettingAPlayerDropsWhatWasSeenOfThemAndByThem()
        {
            var visibility = new ServerVisibility(Wall(), Conceal(marginDistance: 0f, marginSeconds: 1f));
            var target = Hidden();
            var targetEye = target + new Vector3(0f, 0.6f, 0f);
            See(visibility, Team.Alpha, 7, Open(), 0d);
            visibility.CanSeeAlone(3, new Vector3(9f, 1.6f, 0f), 8, target, targetEye, 0d);

            visibility.Forget(7);
            visibility.Forget(3);

            Assert.That(See(visibility, Team.Alpha, 7, Hidden(), 0.2d), Is.False);
            Assert.That(visibility.CanSeeAlone(3, _eye, 8, target, targetEye, 0.2d), Is.False);
        }

        [Test]
        public void ANewWorldDropsWhatWasSeenOnTheOldMap()
        {
            var visibility = new ServerVisibility(Empty(), Conceal(marginDistance: 0f, marginSeconds: 1f));
            See(visibility, Team.Alpha, 7, Hidden(), 0d);

            visibility.UseWorld(Wall());

            Assert.That(See(visibility, Team.Alpha, 7, Hidden(), 0.2d), Is.False);
        }

        [Test]
        public void TheWorldAndTheSettingsAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new ServerVisibility(null, AntiCheatTuning.ConcealSettings.Default));
            Assert.Throws<ArgumentNullException>(() => new ServerVisibility(Empty(), null));
            Assert.Throws<ArgumentNullException>(() => new ServerVisibility(Empty(), AntiCheatTuning.ConcealSettings.Default).UseWorld(null));
            Assert.Throws<ArgumentNullException>(
                () => new ServerVisibility(Empty(), AntiCheatTuning.ConcealSettings.Default).CanSee(Team.Alpha, null, 7, Open(), Open(), 0d));
        }

        private static bool See(ServerVisibility visibility, Team team, int targetId, Vector3 targetCenter, double now)
        {
            return visibility.CanSee(team, new[] { _eye }, targetId, targetCenter, targetCenter + new Vector3(0f, 0.6f, 0f), now);
        }

        private static Vector3 Open()
        {
            return new Vector3(10f, 1f, 5f);
        }

        private static Vector3 Hidden()
        {
            return new Vector3(10f, 1f, -5f);
        }

        private static AntiCheatTuning.ConcealSettings Conceal(float maxDistance = 150f, float marginDistance = 1.5f, float marginSeconds = 0.5f)
        {
            return new AntiCheatTuning.ConcealSettings(true, maxDistance, marginDistance, marginSeconds);
        }

        private static BvhWorldCollision Wall()
        {
            return new BvhWorldCollision(new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(5f, 2.5f, -5f), new Vector3(0.1f, 2.5f, 5f), Quaternion.Identity),
            }));
        }

        private static BvhWorldCollision Empty()
        {
            return new BvhWorldCollision(new WorldMap(Array.Empty<WorldCollider>()));
        }
    }
}
