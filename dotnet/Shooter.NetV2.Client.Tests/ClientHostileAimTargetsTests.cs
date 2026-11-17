using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientHostileAimTargetsTests
    {
        private const int _me = 1;

        private static readonly StanceGeometry _geometry = new StanceGeometry(1.8f, 0.9f, 1.6f, 1.2f, 1f, 0.4f);
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

        private readonly Dictionary<int, RemotePose> _poses = new Dictionary<int, RemotePose>();
        private SnapshotMessage _snapshot;

        [SetUp]
        public void SetUp()
        {
            _poses.Clear();
            _snapshot = new SnapshotMessage();
        }

        [Test]
        public void CollectsOnlyLivingEnemiesInRangeAndLeavesOutAllies()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 20f));
            AddPlayer(3, Team.Alpha, 100, new Vector3(0f, 0f, 10f));
            AddPlayer(4, Team.Bravo, 0, new Vector3(0f, 0f, 10f));
            AddPlayer(5, Team.Bravo, 100, new Vector3(0f, 0f, 12f), downed: true);
            AddPlayer(6, Team.Bravo, 100, new Vector3(0f, 0f, 80f));
            AddRow(7, Team.Bravo, 100, downed: false);
            AddPlayer(8, Team.Bravo, 100, new Vector3(4f, 0f, 20f), PlayerStance.Crouching);

            var found = Collect(new NoWorld(), range: 50f);

            Assert.That(
                found,
                Is.EqualTo(new[]
                {
                    new Vector3(0f, _geometry.Standing.CenterY, 20f),
                    new Vector3(4f, _geometry.Crouched.CenterY, 20f),
                }),
                "not me, an ally, the dead, the downed, the out of range or an unshown player");
        }

        [Test]
        public void AnAllyStandingNextToTheAimIsNotCollected()
        {
            AddPlayer(_me, Team.Bravo, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 5f));

            Assert.That(Collect(new NoWorld(), range: 50f), Is.Empty);
        }

        [Test]
        public void TheOwnTeamIsReadFromTheLatestSnapshotEachTime()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 20f));
            AddPlayer(3, Team.Alpha, 100, new Vector3(0f, 0f, 10f));
            var targets = Targets(new NoWorld());

            var before = new List<Vector3>();
            targets.Collect(_eye, 50f, before);

            _snapshot = new SnapshotMessage();
            AddRow(_me, Team.Bravo, 100, downed: false);
            AddRow(2, Team.Bravo, 100, downed: false);
            AddRow(3, Team.Alpha, 100, downed: false);
            var after = new List<Vector3>();
            targets.Collect(_eye, 50f, after);

            Assert.That(before, Is.EqualTo(new[] { new Vector3(0f, _geometry.Standing.CenterY, 20f) }));
            Assert.That(after, Is.EqualTo(new[] { new Vector3(0f, _geometry.Standing.CenterY, 10f) }));
        }

        [Test]
        public void AnEnemyBehindTheWorldIsNotCollected()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 20f));
            AddPlayer(3, Team.Bravo, 100, new Vector3(0f, 0f, 5f));

            var found = Collect(new WallAt(10f), range: 50f);

            Assert.That(found, Is.EqualTo(new[] { new Vector3(0f, _geometry.Standing.CenterY, 5f) }));
        }

        [Test]
        public void AnEnemyWhoseViewIsBlockedIsNotCollected()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 20f));
            AddPlayer(3, Team.Bravo, 100, new Vector3(0f, 0f, 5f));
            var targets = new ClientHostileAimTargets(
                _me, () => _snapshot, _poses.TryGetValue, new NoWorld(), _geometry, (eye, point) => point.Z > 10f);

            var found = new List<Vector3>();
            targets.Collect(_eye, 50f, found);

            Assert.That(found, Is.EqualTo(new[] { new Vector3(0f, _geometry.Standing.CenterY, 5f) }));
        }

        [Test]
        public void WhileTheOwnRowIsMissingNothingIsCollected()
        {
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 20f));

            Assert.That(Collect(new NoWorld(), 50f), Is.Empty);
        }

        [Test]
        public void WithoutASnapshotNothingIsCollected()
        {
            _snapshot = null;

            Assert.That(Collect(new NoWorld(), 50f), Is.Empty);
        }

        [Test]
        public void ANonPositiveOrInfiniteRangeIsRejected()
        {
            var targets = Targets(new NoWorld());
            var into = new List<Vector3>();

            Assert.Throws<ArgumentOutOfRangeException>(() => targets.Collect(_eye, 0f, into));
            Assert.Throws<ArgumentOutOfRangeException>(() => targets.Collect(_eye, float.PositiveInfinity, into));
            Assert.Throws<ArgumentOutOfRangeException>(() => targets.Collect(_eye, float.NaN, into));
            Assert.Throws<ArgumentNullException>(() => targets.Collect(_eye, 10f, null));
        }

        private List<Vector3> Collect(IWorldCollision world, float range)
        {
            var found = new List<Vector3>();
            Targets(world).Collect(_eye, range, found);
            return found;
        }

        private ClientHostileAimTargets Targets(IWorldCollision world)
        {
            return new ClientHostileAimTargets(_me, () => _snapshot, _poses.TryGetValue, world, _geometry);
        }

        private void AddPlayer(
            int id, Team team, int health, Vector3 position, PlayerStance stance = PlayerStance.Standing, bool downed = false)
        {
            AddRow(id, team, health, downed);
            _poses[id] = new RemotePose(position, 0f, stance, null);
        }

        private void AddRow(int id, Team team, int health, bool downed)
        {
            ref var row = ref _snapshot.Players.Add();
            row.PlayerId = id;
            row.Team = team;
            row.Health = health;
            row.Downed = new DownedSync { IsDowned = downed };
        }

        private sealed class NoWorld : IWorldCollision
        {
            public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
            {
                hit = default;
                return false;
            }

            public bool RaycastCollider(int colliderId, Vector3 origin, Vector3 direction, float maxDistance, out WorldHit hit)
            {
                hit = default;
                return false;
            }

            public int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, int[] results)
            {
                return 0;
            }

            public bool CapsuleCast(
                Vector3 point0, Vector3 point1, float radius, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
            {
                hit = default;
                return false;
            }
        }

        // z = Z の位置に立つ無限の壁
        private sealed class WallAt : IWorldCollision
        {
            private readonly float _z;

            public WallAt(float z)
            {
                _z = z;
            }

            public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
            {
                hit = default;
                if (direction.Z <= 0f)
                {
                    return false;
                }

                var distance = (_z - origin.Z) / direction.Z;
                if (distance < 0f || distance > maxDistance)
                {
                    return false;
                }

                hit = new WorldHit(origin + (direction * distance), -Vector3.UnitZ, distance, 1, 0);
                return true;
            }

            public bool RaycastCollider(int colliderId, Vector3 origin, Vector3 direction, float maxDistance, out WorldHit hit)
            {
                return Raycast(origin, direction, maxDistance, ~0, out hit);
            }

            public int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, int[] results)
            {
                return 0;
            }

            public bool CapsuleCast(
                Vector3 point0, Vector3 point1, float radius, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
            {
                hit = default;
                return false;
            }
        }
    }
}
