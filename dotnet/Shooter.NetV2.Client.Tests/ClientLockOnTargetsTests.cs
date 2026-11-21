using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientLockOnTargetsTests
    {
        private const int _me = 1;

        private static readonly StanceGeometry _geometry = new StanceGeometry(1.8f, 0.9f, 1.6f, 1.2f, 1f, 0.4f);
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);
        private static readonly int[] _shownInRange = { 2, 3 };
        private static readonly int[] _inFrontOfTheWall = { 3 };
        private static readonly int[] _onlyThePlayer = { 2 };

        private readonly Dictionary<int, RemotePose> _poses = new Dictionary<int, RemotePose>();
        private readonly Dictionary<int, Vector3> _centers = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, float> _cones = new Dictionary<int, float>();
        private SnapshotMessage _snapshot;
        private FakeVehicles _vehicles;

        [SetUp]
        public void SetUp()
        {
            _poses.Clear();
            _centers.Clear();
            _cones.Clear();
            _snapshot = new SnapshotMessage();
            _vehicles = new FakeVehicles(_centers, _cones);
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
        public void AConcealedPlayerIsNotCollectedEvenWithAShownPose()
        {
            AddPlayer(2, 100, new Vector3(0f, 0f, 20f));
            AddPlayer(3, 100, new Vector3(0f, 0f, 5f));
            _snapshot.Players[0].Concealed = true;

            var found = Collect(new NoWorld(), range: 50f);

            Assert.That(found.ConvertAll(c => c.Id), Is.EqualTo(_inFrontOfTheWall));
        }

        [Test]
        public void WithoutASnapshotNothingIsCollected()
        {
            _snapshot = null;

            Assert.That(Collect(new NoWorld(), 50f), Is.Empty);
        }

        [Test]
        public void CollectsEveryShownVehicleExceptTheWreckedAndTheOneIAmIn()
        {
            AddPlayer(_me, 100, Vector3.Zero);
            _snapshot.Players[0].Vehicle = new VehicleSeatSync(2, 1);
            AddVehicle(0, VehicleDamageStage.Burning, new Vector3(0f, 3f, 20f));
            AddVehicle(1, VehicleDamageStage.Destroyed, new Vector3(0f, 3f, 25f));
            AddVehicle(2, VehicleDamageStage.Intact, new Vector3(0f, 1f, 1f));
            AddVehicle(3, VehicleDamageStage.Intact, null);
            AddVehicle(4, VehicleDamageStage.Intact, new Vector3(0f, 3f, 90f));

            var found = Collect(new NoWorld(), range: 50f, _vehicles);

            Assert.That(found.ConvertAll(c => c.Id), Is.EqualTo(new[] { ClientLockTargetIds.OfVehicle(0) }));
            Assert.That(found[0].Point, Is.EqualTo(new Vector3(0f, 3f, 20f)));
            Assert.That(found[0].Decoy, Is.Not.Null);
        }

        [Test]
        public void WithoutTheVehicleSourceOnlyPlayersAreCollectedAndNobodyHasADecoy()
        {
            AddPlayer(2, 100, new Vector3(0f, 0f, 20f));
            AddVehicle(0, VehicleDamageStage.Intact, new Vector3(0f, 3f, 20f));

            var found = Collect(new NoWorld(), range: 50f, null);

            Assert.That(found.ConvertAll(c => c.Id), Is.EqualTo(_onlyThePlayer));
            Assert.That(found[0].Decoy, Is.Null);
            Assert.That(Targets(new NoWorld(), null).Decoy(ClientLockTargetIds.OfVehicle(0)), Is.Null);
        }

        [Test]
        public void AVehicleBehindTheWorldIsNotCollected()
        {
            AddVehicle(0, VehicleDamageStage.Intact, new Vector3(0f, 3f, 20f));
            AddVehicle(1, VehicleDamageStage.Intact, new Vector3(0f, 3f, 5f));

            var found = Collect(new WallAt(10f), range: 50f, _vehicles);

            Assert.That(found.ConvertAll(c => c.Id), Is.EqualTo(new[] { ClientLockTargetIds.OfVehicle(1) }));
        }

        [Test]
        public void AFlareNearTheVehicleKeepsTheTrackerFromLockingIt()
        {
            // 目から見て車体 (0, 3, 20) の 30 度の内に入る位置のフレア
            AddVehicle(0, VehicleDamageStage.Intact, new Vector3(0f, 3f, 20f), Flare(7, new Vector3(2f, 2f, 20f)));
            var found = Collect(new NoWorld(), range: 50f, _vehicles);
            var tracker = new LockOnTracker();

            tracker.Update(new HomingSpec(0f, 8f, 45f), 0.1f, true, _eye, Vector3.UnitZ, found);

            Assert.That(found[0].Decoy.TryLure(_eye, found[0].Point, out var flare, out var point), Is.True);
            Assert.That(flare, Is.EqualTo(7));
            Assert.That(point, Is.EqualTo(new Vector3(2f, 2f, 20f)));
            Assert.That(tracker.TargetId, Is.Null, "a lured vehicle is skipped like one outside the cone");
            Assert.That(found[0].Decoy.TryLocate(7, out var located), Is.True);
            Assert.That(located, Is.EqualTo(new Vector3(2f, 2f, 20f)));
            Assert.That(found[0].Decoy.TryLocate(8, out _), Is.False);
        }

        [Test]
        public void AFlareFarFromTheLineOfSightOrOnAVehicleWithoutFlaresLuresNothing()
        {
            AddVehicle(0, VehicleDamageStage.Intact, new Vector3(0f, 3f, 20f), Flare(1, new Vector3(0f, 3f, -20f)));
            AddVehicle(1, VehicleDamageStage.Intact, new Vector3(5f, 3f, 20f), Flare(2, new Vector3(5f, 3f, 20f)));
            _cones.Remove(1);
            var targets = Targets(new NoWorld(), _vehicles);

            Assert.That(targets.Decoy(ClientLockTargetIds.OfVehicle(0)).TryLure(_eye, new Vector3(0f, 3f, 20f), out _, out _), Is.False);
            Assert.That(
                targets.Decoy(ClientLockTargetIds.OfVehicle(1)).TryLure(_eye, new Vector3(5f, 3f, 20f), out _, out _),
                Is.False,
                "the scene vehicle carries no flares, so the client does not know how far they lure");
        }

        [Test]
        public void ALockOnARiderIsLuredByTheFlaresOfTheVehicleTheyRideIn()
        {
            AddPlayer(2, 100, new Vector3(0f, 0f, 20f));
            AddVehicle(0, VehicleDamageStage.Intact, new Vector3(0f, 3f, 20f), Flare(4, new Vector3(1f, 1f, 20f)));
            var targets = Targets(new NoWorld(), _vehicles);
            var decoy = targets.Decoy(2);
            var body = new Vector3(0f, _geometry.Standing.CenterY, 20f);

            Assert.That(decoy.TryLure(_eye, body, out _, out _), Is.False, "on foot the vehicle's flares do not cover them");

            _snapshot.Players[0].Vehicle = new VehicleSeatSync(0, 0);

            Assert.That(decoy.TryLure(_eye, body, out var flare, out _), Is.True);
            Assert.That(flare, Is.EqualTo(4));
            Assert.That(targets.Decoy(2), Is.SameAs(decoy), "the same target keeps one decoy");
        }

        [Test]
        public void FollowingAVehicleGivesItsShownCentreUntilItIsWrecked()
        {
            AddVehicle(0, VehicleDamageStage.Intact, new Vector3(0f, 3f, 20f));
            var follow = Targets(new NoWorld(), _vehicles).Follow(ClientLockTargetIds.OfVehicle(0));

            Assert.That(follow(), Is.EqualTo(new Vector3(0f, 3f, 20f)));

            _centers[0] = new Vector3(4f, 3f, 20f);
            Assert.That(follow(), Is.EqualTo(new Vector3(4f, 3f, 20f)));

            _snapshot.Vehicles[0].Stage = VehicleDamageStage.Destroyed;
            Assert.That(follow(), Is.Null);
        }

        [Test]
        public void TheClosestFlareToTheLineOfSightIsTheDecoyAndTheLaterOneWinsATie()
        {
            var flares = VehicleFlareSync.From(new List<CountermeasureFlare>
            {
                new CountermeasureFlare(1, new Vector3(3f, 0f, 10f), Vector3.Zero, 0f),
                new CountermeasureFlare(2, new Vector3(1f, 0f, 10f), Vector3.Zero, 0f),
                new CountermeasureFlare(3, new Vector3(-1f, 0f, 10f), Vector3.Zero, 0f),
            });

            Assert.That(ClientLockOnTargets.TryLure(flares, 30f, Vector3.Zero, new Vector3(0f, 0f, 10f), out var id, out var point), Is.True);
            Assert.That(id, Is.EqualTo(3));
            Assert.That(point, Is.EqualTo(new Vector3(-1f, 0f, 10f)));
            Assert.That(ClientLockOnTargets.TryLure(flares, 1f, Vector3.Zero, new Vector3(0f, 0f, 10f), out id, out _), Is.False);
            Assert.That(id, Is.EqualTo(-1));
            Assert.That(ClientLockOnTargets.TryLure(VehicleFlareSync.None, 30f, Vector3.Zero, Vector3.UnitZ, out _, out _), Is.False);
        }

        private List<LockOnCandidate> Collect(IWorldCollision world, float range)
        {
            return Collect(world, range, null);
        }

        private List<LockOnCandidate> Collect(IWorldCollision world, float range, IClientLockOnVehicles vehicles)
        {
            var found = new List<LockOnCandidate>();
            Targets(world, vehicles).Collect(_eye, Vector3.UnitZ, range, found);
            return found;
        }

        private ClientLockOnTargets Targets(IWorldCollision world, IClientLockOnVehicles vehicles = null)
        {
            return new ClientLockOnTargets(_me, () => _snapshot, _poses.TryGetValue, world, _geometry, vehicles);
        }

        // center が null の乗り物は、スナップショットにはあるが画面に出していない
        private void AddVehicle(int index, VehicleDamageStage stage, Vector3? center, VehicleFlareSync flares = null)
        {
            ref var row = ref _snapshot.Vehicles.Add();
            row.Index = index;
            row.Rotation = Quaternion.Identity;
            row.Stage = stage;
            row.Flares = flares ?? VehicleFlareSync.None;
            if (center.HasValue)
            {
                _centers[index] = center.Value;
            }

            _cones[index] = CountermeasureSpec.DefaultDecoyConeDegrees;
        }

        private static VehicleFlareSync Flare(int id, Vector3 position)
        {
            return VehicleFlareSync.From(new List<CountermeasureFlare> { new CountermeasureFlare(id, position, Vector3.Zero, 0f) });
        }

        private sealed class FakeVehicles : IClientLockOnVehicles
        {
            private readonly Dictionary<int, Vector3> _centers;
            private readonly Dictionary<int, float> _cones;

            public FakeVehicles(Dictionary<int, Vector3> centers, Dictionary<int, float> cones)
            {
                _centers = centers;
                _cones = cones;
            }

            public bool TryGetShownCenter(int vehicle, out Vector3 center)
            {
                return _centers.TryGetValue(vehicle, out center);
            }

            public float? DecoyConeDegrees(int vehicle)
            {
                return _cones.TryGetValue(vehicle, out var cone) ? cone : (float?)null;
            }
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
