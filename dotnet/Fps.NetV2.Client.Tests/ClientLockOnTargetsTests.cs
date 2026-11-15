using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    public sealed class ClientLockOnTargetsTests
    {
        private const int _me = 1;

        private static readonly StanceGeometry _geometry = new StanceGeometry(1.8f, 0.9f, 1.6f, 1.2f, 1f, 0.4f);
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);
        private static readonly int[] _shownInRange = { 2, 3 };
        private static readonly int[] _inFrontOfTheWall = { 3 };

        private readonly Dictionary<int, RemotePose> _poses = new Dictionary<int, RemotePose>();
        private SnapshotMessage _snapshot;

        [SetUp]
        public void SetUp()
        {
            _poses.Clear();
            _snapshot = new SnapshotMessage();
        }

        [Test]
        public void CollectsTheShownBodyCentreOfEveryLivingOtherPlayerInRange()
        {
            AddPlayer(_me, 100, Vector3.Zero);
            AddPlayer(2, 100, new Vector3(0f, 0f, 20f));
            AddPlayer(3, 100, new Vector3(0f, 0f, 20f), PlayerStance.Crouching);
            AddPlayer(4, 0, new Vector3(0f, 0f, 10f));
            AddPlayer(5, 100, new Vector3(0f, 0f, 80f));
            AddRow(6, 100);

            var found = Collect(new NoWorld(), range: 50f);

            Assert.That(found.ConvertAll(c => c.Id), Is.EqualTo(_shownInRange), "not me, the dead, the out of range or an unshown player");
            Assert.That(found[0].Point, Is.EqualTo(new Vector3(0f, _geometry.Standing.CenterY, 20f)));
            Assert.That(found[1].Point, Is.EqualTo(new Vector3(0f, _geometry.Crouched.CenterY, 20f)));
        }

        [Test]
        public void APlayerBehindTheWorldIsNotCollected()
        {
            AddPlayer(2, 100, new Vector3(0f, 0f, 20f));
            AddPlayer(3, 100, new Vector3(0f, 0f, 5f));

            var found = Collect(new WallAt(10f), range: 50f);

            Assert.That(found.ConvertAll(c => c.Id), Is.EqualTo(_inFrontOfTheWall));
        }

        [Test]
        public void FollowGivesTheTargetsCurrentlyShownCentreUntilItIsGone()
        {
            AddPlayer(2, 100, new Vector3(0f, 0f, 20f));
            var targets = Targets(new NoWorld());
            var follow = targets.Follow(2);

            _poses[2] = new RemotePose(new Vector3(3f, 0f, 20f), 0f, PlayerStance.Standing, null);
            Assert.That(follow(), Is.EqualTo(new Vector3(3f, _geometry.Standing.CenterY, 20f)));

            _poses.Remove(2);
            Assert.That(follow(), Is.Null);
        }

        [Test]
        public void WithoutASnapshotNothingIsCollected()
        {
            _snapshot = null;

            Assert.That(Collect(new NoWorld(), 50f), Is.Empty);
        }

        private List<LockOnCandidate> Collect(IWorldCollision world, float range)
        {
            var found = new List<LockOnCandidate>();
            Targets(world).Collect(_eye, Vector3.UnitZ, range, found);
            return found;
        }

        private ClientLockOnTargets Targets(IWorldCollision world)
        {
            return new ClientLockOnTargets(_me, () => _snapshot, _poses.TryGetValue, world, _geometry);
        }

        private void AddPlayer(int id, int health, Vector3 position, PlayerStance stance = PlayerStance.Standing)
        {
            AddRow(id, health);
            _poses[id] = new RemotePose(position, 0f, stance, null);
        }

        private void AddRow(int id, int health)
        {
            ref var row = ref _snapshot.Players.Add();
            row.PlayerId = id;
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
