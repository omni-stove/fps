using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientNameplateContactsTests
    {
        private const int _me = 1;

        private static readonly StanceGeometry _geometry = new StanceGeometry(1.8f, 0.9f, 1.6f, 1.2f, 1f, 0.4f);
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

        private static readonly int[] _ids23 = { 2, 3 };
        private static readonly int[] _ids3 = { 3 };
        private static readonly int[] _ids4 = { 4 };
        private static readonly int[] _ids992 = { 99, 2 };

        private readonly Dictionary<int, RemotePose> _poses = new Dictionary<int, RemotePose>();
        private SnapshotMessage _snapshot;

        [SetUp]
        public void SetUp()
        {
            _poses.Clear();
            _snapshot = new SnapshotMessage();
        }

        [Test]
        public void CollectsEveryShownPlayerButMeWithTheirRelationAndTheTopOfTheirHead()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Alpha, 100, new Vector3(0f, 0f, 10f));
            AddPlayer(3, Team.Bravo, 100, new Vector3(4f, 0f, 20f), PlayerStance.Crouching);
            AddRow(4, Team.Bravo, 100);

            var found = Collect(new NoWorld());

            Assert.That(found.Select(c => c.Id), Is.EqualTo(_ids23), "not me and not a player who is not shown yet");
            Assert.That(found[0].Relation, Is.EqualTo(TeamRelation.Friendly));
            Assert.That(found[0].HeadPosition, Is.EqualTo(new Vector3(0f, _geometry.Standing.TopY, 10f)));
            Assert.That(found[1].Relation, Is.EqualTo(TeamRelation.Enemy));
            Assert.That(found[1].HeadPosition, Is.EqualTo(new Vector3(4f, _geometry.Crouched.TopY, 20f)));
            Assert.That(found.All(c => c.IsAlive), Is.True);
        }

        [Test]
        public void TheDeadAreCollectedAsNotAliveSoTheRuleCanHideThem()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 0, new Vector3(0f, 0f, 10f));

            var found = Collect(new NoWorld());

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].IsAlive, Is.False);
            Assert.That(found[0].IsInSight, Is.False, "the dead are not checked for sight");
        }

        [Test]
        public void AnEnemyBehindTheWorldIsNotInSightButAnAllyIsNotChecked()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 20f));
            AddPlayer(3, Team.Bravo, 100, new Vector3(0f, 0f, 5f));
            AddPlayer(4, Team.Alpha, 100, new Vector3(0f, 0f, 20f));

            var found = Collect(new WallAt(10f)).ToDictionary(c => c.Id);

            Assert.That(found[2].IsInSight, Is.False, "the wall is between");
            Assert.That(found[3].IsInSight, Is.True);
            Assert.That(found[4].IsInSight, Is.False, "allies are shown through walls by NameplateRule, so their sight is not checked");
        }

        [Test]
        public void AnEnemyWhoseViewIsBlockedIsNotInSight()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 20f));
            AddPlayer(3, Team.Bravo, 100, new Vector3(0f, 0f, 5f));
            var contacts = new ClientNameplateContacts(
                _me, () => _snapshot, _poses.TryGetValue, new NoWorld(), _geometry, (eye, point) => point.Z > 10f);

            var found = new List<NameplateContact>();
            contacts.CopyTo(_eye, found);

            Assert.That(found.Where(c => c.IsInSight).Select(c => c.Id), Is.EqualTo(_ids3));
        }

        [Test]
        public void SightIsCheckedToTheMiddleOfTheBodyLikeTheLockOn()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 20f));
            var checkedPoints = new List<Vector3>();
            var contacts = new ClientNameplateContacts(
                _me, () => _snapshot, _poses.TryGetValue, new NoWorld(), _geometry, (eye, point) =>
                {
                    checkedPoints.Add(point);
                    return false;
                });

            contacts.CopyTo(_eye, new List<NameplateContact>());

            Assert.That(checkedPoints, Is.EqualTo(new[] { new Vector3(0f, _geometry.Standing.CenterY, 20f) }));
        }

        [Test]
        public void WhileTheOwnRowIsMissingEveryoneIsAnEnemy()
        {
            AddPlayer(2, Team.Alpha, 100, new Vector3(0f, 0f, 10f));
            AddPlayer(3, Team.Bravo, 100, new Vector3(0f, 0f, 20f));

            var found = Collect(new NoWorld());

            Assert.That(found.Select(c => c.Relation), Is.EqualTo(new[] { TeamRelation.Enemy, TeamRelation.Enemy }));
        }

        [Test]
        public void TheOwnTeamIsReadFromTheLatestSnapshotEachTime()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 20f));
            var contacts = Contacts(new NoWorld());

            var before = new List<NameplateContact>();
            contacts.CopyTo(_eye, before);
            _snapshot = new SnapshotMessage();
            AddRow(_me, Team.Bravo, 100);
            AddRow(2, Team.Bravo, 100);
            var after = new List<NameplateContact>();
            contacts.CopyTo(_eye, after);

            Assert.That(before.Single().Relation, Is.EqualTo(TeamRelation.Enemy));
            Assert.That(after.Single().Relation, Is.EqualTo(TeamRelation.Friendly));
        }

        [Test]
        public void APoseThatIsNotFiniteIsLeftOut()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(float.NaN, 0f, 10f));
            AddPlayer(3, Team.Bravo, 100, new Vector3(0f, float.PositiveInfinity, 10f));
            AddPlayer(4, Team.Bravo, 100, new Vector3(0f, 0f, 10f));

            Assert.That(Collect(new NoWorld()).Select(c => c.Id), Is.EqualTo(_ids4));
        }

        [Test]
        public void FromAnEyeThatIsNotFiniteNoEnemyIsInSight()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 10f));

            var found = new List<NameplateContact>();
            Contacts(new NoWorld()).CopyTo(new Vector3(float.NaN, 0f, 0f), found);

            Assert.That(found.Single().IsInSight, Is.False);
        }

        [Test]
        public void TheListIsAddedToAndNotCleared()
        {
            AddPlayer(_me, Team.Alpha, 100, Vector3.Zero);
            AddPlayer(2, Team.Bravo, 100, new Vector3(0f, 0f, 10f));
            var found = new List<NameplateContact> { new NameplateContact(99, Vector3.Zero, TeamRelation.Neutral, true, false) };

            Contacts(new NoWorld()).CopyTo(_eye, found);

            Assert.That(found.Select(c => c.Id), Is.EqualTo(_ids992));
        }

        [Test]
        public void WithoutASnapshotNothingIsCollected()
        {
            _snapshot = null;

            Assert.That(Collect(new NoWorld()), Is.Empty);
        }

        [Test]
        public void MissingPartsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientNameplateContacts(_me, null, _poses.TryGetValue, new NoWorld(), _geometry));
            Assert.Throws<ArgumentNullException>(() => new ClientNameplateContacts(_me, () => _snapshot, null, new NoWorld(), _geometry));
            Assert.Throws<ArgumentNullException>(() => new ClientNameplateContacts(_me, () => _snapshot, _poses.TryGetValue, null, _geometry));
            Assert.Throws<ArgumentNullException>(() => new ClientNameplateContacts(_me, () => _snapshot, _poses.TryGetValue, new NoWorld(), null));
            Assert.Throws<ArgumentNullException>(() => Contacts(new NoWorld()).CopyTo(_eye, null));
        }

        private List<NameplateContact> Collect(IWorldCollision world)
        {
            var found = new List<NameplateContact>();
            Contacts(world).CopyTo(_eye, found);
            return found;
        }

        private ClientNameplateContacts Contacts(IWorldCollision world)
        {
            return new ClientNameplateContacts(_me, () => _snapshot, _poses.TryGetValue, world, _geometry);
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
