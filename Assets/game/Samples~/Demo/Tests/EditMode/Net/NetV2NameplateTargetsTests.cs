using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using Vector3 = System.Numerics.Vector3;

namespace Shooter.Net.Tests
{
    public sealed class NetV2NameplateTargetsTests
    {
        private const int _me = 1;

        private static readonly StanceGeometry _geometry = new StanceGeometry(1.8f, 0.9f, 1.6f, 1.2f, 1f, 0.4f);
        private static readonly UnityEngine.Vector3 _eye = new UnityEngine.Vector3(0f, 1.6f, 0f);

        private readonly Dictionary<int, RemotePose> _poses = new Dictionary<int, RemotePose>();
        private SnapshotMessage _snapshot;
        private IParticipants _participants;

        [SetUp]
        public void SetUp()
        {
            _poses.Clear();
            _snapshot = new SnapshotMessage();
            _participants = new ParticipantTable();
        }

        [Test]
        public void EveryOtherShownPlayerIsATargetWithTheirTeamRelationAndLife()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Alpha, 80, new Vector3(-2f, 0f, 10f));
            AddPlayer(3, Team.Bravo, 100, new Vector3(2f, 0f, 10f));
            AddPlayer(4, Team.Bravo, 0, new Vector3(0f, 0f, 12f));
            AddRow(5, Team.Bravo, 100);

            var found = Copy(new NoWorld());

            Assert.That(found.Select(t => t.Id), Is.EqualTo(new[] { 2, 3, 4 }), "not me and not a player whose body is not shown");
            var ally = found.Single(t => t.Id == 2);
            Assert.That(ally.Relation, Is.EqualTo(TeamRelation.Friendly));
            Assert.That(ally.IsAlive, Is.True);
            Assert.That(ally.IsInSight, Is.False, "sight is only checked for enemies, like the offline targets");
            var enemy = found.Single(t => t.Id == 3);
            Assert.That(enemy.Relation, Is.EqualTo(TeamRelation.Enemy));
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.IsInSight, Is.True);
            var dead = found.Single(t => t.Id == 4);
            Assert.That(dead.IsAlive, Is.False, "the dead are passed on so NameplateRule can drop them");
            Assert.That(dead.IsInSight, Is.False);
        }

        [Test]
        public void TheHeadIsTheTopOfTheShownBodyForItsStance()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Alpha, 100, new Vector3(1f, 0.5f, 10f));
            AddPlayer(3, Team.Alpha, 100, new Vector3(-1f, 0.5f, 10f), PlayerStance.Crouching);

            var found = Copy(new NoWorld());

            var standing = found.Single(t => t.Id == 2).HeadPosition;
            Assert.That(standing.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(standing.y, Is.EqualTo(0.5f + _geometry.Standing.TopY).Within(1e-5f));
            Assert.That(standing.z, Is.EqualTo(10f).Within(1e-5f));
            var crouched = found.Single(t => t.Id == 3).HeadPosition;
            Assert.That(crouched.y, Is.EqualTo(0.5f + _geometry.Crouched.TopY).Within(1e-5f));
            Assert.That(crouched.y, Is.LessThan(standing.y), "a crouched player's name sits lower");
        }

        [Test]
        public void AnEnemyBehindTheMapIsNotInSightButAnAllyStillIsATarget()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Alpha, 100, new Vector3(-1f, 0f, 20f));
            AddPlayer(3, Team.Bravo, 100, new Vector3(1f, 0f, 20f));
            AddPlayer(4, Team.Bravo, 100, new Vector3(0f, 0f, 5f));

            var found = Copy(new WallAt(10f));

            Assert.That(found.Single(t => t.Id == 3).IsInSight, Is.False, "the wall at z=10 hides the enemy at z=20");
            Assert.That(found.Single(t => t.Id == 4).IsInSight, Is.True, "the enemy in front of the wall is seen");
            Assert.That(found.Single(t => t.Id == 2).Relation, Is.EqualTo(TeamRelation.Friendly));
        }

        [Test]
        public void SmokeHidesAnEnemy()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(3, Team.Bravo, 100, new Vector3(0f, 0f, 10f));
            var asked = new List<(Vector3 Eye, Vector3 Point)>();
            var targets = new NetV2NameplateTargets(
                _me, () => _snapshot, _poses.TryGetValue, new NoWorld(), _geometry, () => _participants,
                (eye, point) =>
                {
                    asked.Add((eye, point));
                    return true;
                });
            var found = new List<NameplateTarget>();

            targets.CopyTo(_eye, found);

            Assert.That(found.Single().IsInSight, Is.False);
            Assert.That(asked, Is.EqualTo(new[] { (new Vector3(0f, 1.6f, 0f), new Vector3(0f, _geometry.Standing.CenterY, 10f)) }),
                "the sight line runs from the eye to the body's centre, like the lock-on");
        }

        [Test]
        public void ATeamSwitchInTheLatestSnapshotChangesTheRelation()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Alpha, 100, new Vector3(0f, 0f, 10f));
            var targets = Targets(new NoWorld());
            var found = new List<NameplateTarget>();
            targets.CopyTo(_eye, found);
            Assert.That(found.Single().Relation, Is.EqualTo(TeamRelation.Friendly));

            _snapshot.Players[0].Team = Team.Bravo;
            found.Clear();
            targets.CopyTo(_eye, found);

            Assert.That(found.Single().Relation, Is.EqualTo(TeamRelation.Enemy));
        }

        [Test]
        public void NothingIsGatheredUntilTheLocalPlayersTeamIsKnown()
        {
            AddPlayer(2, Team.Alpha, 100, new Vector3(0f, 0f, 10f));

            Assert.That(Copy(new NoWorld()), Is.Empty, "without my row an enemy could be named through a wall as an ally");

            _snapshot = null;

            Assert.That(Copy(new NoWorld()), Is.Empty);
        }

        [Test]
        public void ABrokenPoseFromTheServerIsSkipped()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Alpha, 100, new Vector3(float.NaN, 0f, 10f));
            AddPlayer(3, Team.Alpha, 100, new Vector3(0f, 0f, 10f));

            Assert.That(Copy(new NoWorld()).Select(t => t.Id), Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void TheParticipantsAreReadEachTime()
        {
            var targets = Targets(new NoWorld());
            var first = _participants;
            Assert.That(targets.Participants, Is.SameAs(first));

            _participants = new ParticipantTable();

            Assert.That(targets.Participants, Is.SameAs(_participants), "the roster is rebuilt with each map");
            Assert.That(targets.Participants, Is.Not.SameAs(first));
        }

        [Test]
        public void MissingArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2NameplateTargets(_me, null, _poses.TryGetValue, new NoWorld(), _geometry, () => _participants));
            Assert.Throws<ArgumentNullException>(() => new NetV2NameplateTargets(_me, () => _snapshot, null, new NoWorld(), _geometry, () => _participants));
            Assert.Throws<ArgumentNullException>(() => new NetV2NameplateTargets(_me, () => _snapshot, _poses.TryGetValue, null, _geometry, () => _participants));
            Assert.Throws<ArgumentNullException>(() => new NetV2NameplateTargets(_me, () => _snapshot, _poses.TryGetValue, new NoWorld(), null, () => _participants));
            Assert.Throws<ArgumentNullException>(() => new NetV2NameplateTargets(_me, () => _snapshot, _poses.TryGetValue, new NoWorld(), _geometry, null));
            Assert.Throws<ArgumentNullException>(() => Targets(new NoWorld()).CopyTo(_eye, null));
        }

        private List<NameplateTarget> Copy(IWorldCollision world)
        {
            var found = new List<NameplateTarget>();
            Targets(world).CopyTo(_eye, found);
            return found;
        }

        private NetV2NameplateTargets Targets(IWorldCollision world)
        {
            return new NetV2NameplateTargets(_me, () => _snapshot, _poses.TryGetValue, world, _geometry, () => _participants);
        }

        private void AddPlayer(int id, Team team, int health, Vector3 position, PlayerStance stance = PlayerStance.Standing)
        {
            AddRow(id, team, health);
            _poses[id] = new RemotePose(position, 0f, stance, null);
        }

        private void AddRow(int id, Team team, int health)
        {
            ref var row = ref _snapshot.Players.Add();
            row.PlayerId = id;
            row.Team = team;
            row.Health = health;
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
