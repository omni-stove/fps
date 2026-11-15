using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    public sealed class ClientProjectilesTests
    {
        private const int _me = 1;
        private const int _other = 2;
        private const float _frame = 1f / 60f;
        private const float _wallZ = 20f;
        private const string _crossbowId = "0d4c2b1a-3e5f-4a6b-8c7d-9e0f1a2b3c4d";
        private const string _rocketId = "1e5d3c2b-4f6a-4b7c-9d8e-0f1a2b3c4d5e";
        private const string _rifleId = "3a7f5e4d-6b8c-4d9e-9f0a-2b3c4d5e6f70";
        private const string _missileId = "2f6e4d3c-5a7b-4c8d-8e9f-1a2b3c4d5e6f";

        private static readonly Vector3 _gravity = new Vector3(0f, -9.81f, 0f);
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

        private FakeViews _views;
        private ClientProjectiles _projectiles;

        [SetUp]
        public void SetUp()
        {
            _views = new FakeViews();
            _projectiles = new ClientProjectiles(Find);
            _projectiles.Attach(_views, new Wall(_wallZ), ~0, _gravity, _me);
        }

        [Test]
        public void AnotherPlayersArrowAppearsAtTheSpawnArcsAndStaysWhereTheServerSaysItStuck()
        {
            _projectiles.Spawned(Spawn(7, _other, _crossbowId, _eye, new Vector3(0f, 0f, 30f)));

            var arrow = _views.Single();
            Assert.That(arrow.WeaponId, Is.EqualTo(_crossbowId));
            Assert.That(arrow.Position, Is.EqualTo(_eye));

            Advance(0.3f);

            Assert.That(arrow.Position.Z, Is.EqualTo(9f).Within(0.05f));
            Assert.That(arrow.Position.Y, Is.LessThan(_eye.Y - 0.3f), "gravity bends the flight");
            Assert.That(arrow.Velocity.Y, Is.LessThan(0f));

            var hit = new Vector3(0f, 1f, 12f);
            _projectiles.Ended(new ProjectileEndedMessage { ProjectileId = 7, Reason = ProjectileEndReason.HitWorld, Position = hit });

            Assert.That(arrow.StuckAt, Is.EqualTo(hit));
            Assert.That(arrow.Removed, Is.False);
            Assert.That(_projectiles.Count, Is.Zero);
        }

        [Test]
        public void ARoundThatDoesNotStickIsRemovedWhenItEnds()
        {
            _projectiles.Spawned(Spawn(3, _other, _rocketId, _eye, new Vector3(0f, 0f, 20f)));

            _projectiles.Ended(new ProjectileEndedMessage { ProjectileId = 3, Reason = ProjectileEndReason.HitWorld, Position = Vector3.Zero });

            Assert.That(_views.Single().Removed, Is.True);
            Assert.That(_views.Single().StuckAt, Is.Null);
        }

        [Test]
        public void AnArrowThatHitAPlayerIsRemovedEvenIfItStuckInTheWallBehindHere()
        {
            _projectiles.Spawned(Spawn(4, _other, _crossbowId, new Vector3(0f, 1.6f, _wallZ - 1f), new Vector3(0f, 0f, 40f)));
            Advance(0.1f);
            Assert.That(_views.Single().StuckAt, Is.Not.Null, "the client stops it on the wall it knows");

            _projectiles.Ended(new ProjectileEndedMessage { ProjectileId = 4, Reason = ProjectileEndReason.HitCharacter });

            Assert.That(_views.Single().Removed, Is.True);
        }

        [Test]
        public void TheShooterSeesTheArrowAtOnceAndTheServersArrowTakesItOver()
        {
            _projectiles.LocalFired(Fire(_crossbowId, Vector3.UnitZ), Find(_crossbowId));

            var arrow = _views.Single();
            Assert.That(arrow.Position, Is.EqualTo(_eye));
            Assert.That(arrow.Velocity, Is.EqualTo(new Vector3(0f, 0f, 30f)));

            Advance(0.1f);
            var ahead = arrow.Position;

            // サーバーの弾は往復の分だけ遅れて届く。
            _projectiles.Spawned(Spawn(9, _me, _crossbowId, _eye, new Vector3(0f, 0f, 30f)));
            _projectiles.Apply(Snapshot(Row(9, _me, _crossbowId, _eye + new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, 30f))));

            Assert.That(_views.Shown, Has.Count.EqualTo(1), "no second arrow for the server's");
            Assert.That(arrow.Position, Is.EqualTo(ahead), "the late snapshot does not pull the prediction back");

            Advance(0.5f);
            Assert.That(arrow.Removed, Is.False, "a claimed prediction is not dropped as rejected");

            var hit = new Vector3(0f, 0.5f, 15f);
            _projectiles.Ended(new ProjectileEndedMessage { ProjectileId = 9, Reason = ProjectileEndReason.HitWorld, Position = hit });

            Assert.That(arrow.StuckAt, Is.EqualTo(hit));
        }

        [Test]
        public void APredictionTheServerNeverClaimsIsRemoved()
        {
            _projectiles.LocalFired(Fire(_crossbowId, new Vector3(0f, 1f, 0f)), Find(_crossbowId));

            Advance(ClientProjectiles.DefaultPendingSeconds - 0.1f);
            Assert.That(_views.Single().Removed, Is.False);

            Advance(0.2f);
            Assert.That(_views.Single().Removed, Is.True);
            Assert.That(_projectiles.Count, Is.Zero);
        }

        [Test]
        public void OnlyProjectileShotsArePredicted()
        {
            _projectiles.LocalFired(Fire(_crossbowId, Vector3.UnitZ, TriggerEdge.Pressed), Find(_crossbowId));
            _projectiles.LocalFired(Fire(_rifleId, Vector3.UnitZ), new WeaponSpec(_rifleId, "", WeaponCategory.AssaultRifle));

            Assert.That(_views.Shown, Is.Empty);
        }

        [Test]
        public void EveryPlannedRoundIsPredictedAndClaimedInOrder()
        {
            var spec = Weapon(_crossbowId, WorldHitResponse.Stick, pellets: 3, spreadDegrees: 5f);
            var planned = PelletPattern.Count(spec.Round);
            _projectiles.LocalFired(Fire(_crossbowId, Vector3.UnitZ), spec);
            _projectiles.LocalFired(Fire(_crossbowId, Vector3.UnitZ), spec);

            Assert.That(_views.Shown, Has.Count.EqualTo(2 * planned));
            for (var id = 0; id < 2 * planned; id++)
            {
                _projectiles.Spawned(Spawn(id, _me, _crossbowId, _eye, new Vector3(0f, 0f, 30f)));
            }

            Assert.That(_views.Shown, Has.Count.EqualTo(2 * planned));
            _projectiles.Ended(new ProjectileEndedMessage { ProjectileId = 0, Reason = ProjectileEndReason.Expired });

            Assert.That(_views.Shown[0].Removed, Is.True);
            Assert.That(_views.Shown[1].Removed, Is.False);
        }

        [Test]
        public void AnotherPlayersArrowIsEasedTowardsTheSnapshotInsteadOfJumping()
        {
            _projectiles.Spawned(Spawn(5, _other, _rocketId, _eye, new Vector3(0f, 0f, 10f)));
            Advance(0.1f);
            var before = _views.Single().Position;

            var server = before + new Vector3(1f, 0f, 0f);
            _projectiles.Apply(Snapshot(Row(5, _other, _rocketId, server, new Vector3(0f, 0f, 10f))));

            Assert.That(_views.Single().Position, Is.EqualTo(before), "the shown position does not jump");

            Advance(0.5f);
            var expected = server + new Vector3(0f, 0f, 5f);
            Assert.That(Vector3.Distance(_views.Single().Position, expected), Is.LessThan(0.05f));
        }

        [Test]
        public void AFarSnapshotIsJumpedTo()
        {
            _projectiles.Spawned(Spawn(5, _other, _rocketId, _eye, new Vector3(0f, 0f, 10f)));
            var far = _eye + new Vector3(ClientProjectiles.DefaultSnapDistance + 1f, 0f, 0f);

            _projectiles.Apply(Snapshot(Row(5, _other, _rocketId, far, new Vector3(0f, 0f, 10f))));

            Assert.That(_views.Single().Position, Is.EqualTo(far));
        }

        [Test]
        public void TheShootersHomingMissileFollowsTheServer()
        {
            _projectiles.LocalFired(Fire(_missileId, Vector3.UnitZ), Find(_missileId));
            _projectiles.Spawned(Spawn(6, _me, _missileId, _eye, new Vector3(0f, 0f, 20f)));

            var turned = _eye + new Vector3(ClientProjectiles.DefaultSnapDistance + 1f, 0f, 0f);
            _projectiles.Apply(Snapshot(Row(6, _me, _missileId, turned, new Vector3(20f, 0f, 0f))));

            Assert.That(_views.Single().Position, Is.EqualTo(turned));
        }

        [Test]
        public void AnArrowWhoseSpawnWasMissedAppearsFromTheSnapshotAndLeavesWithIt()
        {
            _projectiles.Apply(Snapshot(Row(8, _other, _crossbowId, _eye, new Vector3(0f, 0f, 30f))));

            var arrow = _views.Single();
            Assert.That(arrow.Position, Is.EqualTo(_eye));

            _projectiles.Apply(Snapshot());
            Advance(ClientProjectiles.DefaultPendingSeconds);

            Assert.That(arrow.Removed, Is.True);
            Assert.That(_projectiles.Count, Is.Zero);
        }

        [Test]
        public void AnArrowAlreadySpawnedIsNotRemovedBeforeASnapshotHasListedIt()
        {
            _projectiles.Spawned(Spawn(8, _other, _crossbowId, _eye, new Vector3(0f, 0f, 30f)));

            _projectiles.Apply(Snapshot());

            Assert.That(_views.Single().Removed, Is.False);
        }

        [Test]
        public void AStaleSnapshotDoesNotBringAnEndedArrowBack()
        {
            var row = Row(8, _other, _crossbowId, _eye, new Vector3(0f, 0f, 30f));
            _projectiles.Ended(new ProjectileEndedMessage { ProjectileId = 8, Reason = ProjectileEndReason.Removed });

            _projectiles.Apply(Snapshot(row));
            _projectiles.Spawned(Spawn(8, _other, _crossbowId, _eye, new Vector3(0f, 0f, 30f)));

            Assert.That(_views.Shown, Is.Empty);

            // スナップショットから消えたら Id を忘れ、サーバーが使い直した Id の弾は出す。
            _projectiles.Apply(Snapshot());
            _projectiles.Apply(Snapshot(row));

            Assert.That(_views.Shown, Has.Count.EqualTo(1));
        }

        [Test]
        public void AnArrowTheServerSaysNothingAboutIsDroppedAfterItLands()
        {
            _projectiles.Spawned(Spawn(4, _other, _rocketId, new Vector3(0f, 1.6f, _wallZ - 1f), new Vector3(0f, 0f, 40f)));
            Advance(0.1f);
            Assert.That(_views.Single().Removed, Is.False, "a stopped round waits for the server");

            Advance(ClientProjectiles.DefaultPendingSeconds);

            Assert.That(_views.Single().Removed, Is.True);
            Assert.That(_projectiles.Count, Is.Zero);
        }

        [Test]
        public void AnUnknownWeaponStillShowsAStraightRound()
        {
            _projectiles.Spawned(Spawn(2, _other, "unknown", _eye, new Vector3(0f, 0f, 10f)));

            Advance(0.5f);

            Assert.That(_views.Single().Position.Y, Is.EqualTo(_eye.Y).Within(1e-4f));
            Assert.That(_views.Single().Position.Z, Is.EqualTo(5f).Within(0.05f));
        }

        [Test]
        public void NothingIsShownOutsideAMatchAndLeavingRemovesEverything()
        {
            var idle = new ClientProjectiles(Find);
            idle.Spawned(Spawn(1, _other, _crossbowId, _eye, Vector3.UnitZ));
            idle.Apply(Snapshot(Row(2, _other, _crossbowId, _eye, Vector3.UnitZ)));
            idle.LocalFired(Fire(_crossbowId, Vector3.UnitZ), Find(_crossbowId));
            idle.Ended(new ProjectileEndedMessage { ProjectileId = 1 });
            idle.Tick(_frame);
            Assert.That(idle.Count, Is.Zero);

            _projectiles.Spawned(Spawn(1, _other, _crossbowId, _eye, Vector3.UnitZ));
            _projectiles.LocalFired(Fire(_crossbowId, Vector3.UnitZ), Find(_crossbowId));
            _projectiles.Detach();

            Assert.That(_views.Shown, Has.Count.EqualTo(2));
            Assert.That(_views.Shown, Has.All.Matches<FakeView>(view => view.Removed));
            Assert.That(_projectiles.IsAttached, Is.False);
        }

        [Test]
        public void TheHooksShowTheServersArrows()
        {
            var hooks = new ClientWeaponHooks(Find);
            hooks.Projectiles.Attach(_views, new Wall(_wallZ), ~0, _gravity, _me);

            hooks.Handle(Spawn(1, _other, _crossbowId, _eye, Vector3.UnitZ));
            hooks.LocalFired(Fire(_crossbowId, Vector3.UnitZ), Find(_crossbowId));

            Assert.That(_views.Shown, Has.Count.EqualTo(2));
        }

        private void Advance(float seconds)
        {
            for (var left = seconds; left > 1e-6f; left -= _frame)
            {
                _projectiles.Tick(Math.Min(_frame, left));
            }
        }

        private static WeaponSpec Find(string weaponId)
        {
            switch (weaponId)
            {
                case _crossbowId:
                    return Weapon(_crossbowId, WorldHitResponse.Stick);
                case _rocketId:
                    return Weapon(_rocketId, WorldHitResponse.Stop, gravity: 0f);
                case _missileId:
                    return Weapon(_missileId, WorldHitResponse.Stop, gravity: 0f, homing: new HomingSpec(0.5f, 10f, 90f));
                default:
                    return null;
            }
        }

        private static WeaponSpec Weapon(
            string id, WorldHitResponse onWorldHit, float gravity = 1f, int pellets = 1, float spreadDegrees = 0f, HomingSpec homing = null)
        {
            var speed = id == _crossbowId ? 30f : 20f;
            var round = new RoundSpec(
                RoundDelivery.Projectile,
                pelletCount: pellets,
                pelletSpreadDegrees: spreadDegrees,
                projectile: new ProjectileSpec(speed, gravity, 5f, onWorldHit),
                homing: homing);
            return new WeaponSpec(id, "", WeaponCategory.AssaultRifle, round: round);
        }

        private static FireRequestMessage Fire(string weaponId, Vector3 direction, TriggerEdge edge = TriggerEdge.None)
        {
            return new FireRequestMessage { WeaponId = weaponId, Origin = _eye, Direction = direction, Trigger = edge };
        }

        private static ProjectileSpawnedMessage Spawn(int id, int shooter, string weaponId, Vector3 position, Vector3 velocity)
        {
            return new ProjectileSpawnedMessage
            {
                ProjectileId = id,
                HasShooter = true,
                ShooterId = shooter,
                WeaponId = weaponId,
                Position = position,
                Velocity = velocity,
            };
        }

        private static ProjectileSnapshot Row(int id, int shooter, string weaponId, Vector3 position, Vector3 velocity)
        {
            return new ProjectileSnapshot
            {
                ProjectileId = id,
                Present = ProjectileFields.All,
                HasShooter = true,
                ShooterId = shooter,
                WeaponId = weaponId,
                Position = position,
                Velocity = velocity,
            };
        }

        private static SnapshotMessage Snapshot(params ProjectileSnapshot[] rows)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            foreach (var row in rows)
            {
                snapshot.Projectiles.Add() = row;
            }

            return snapshot;
        }

        private sealed class FakeViews : IClientProjectileViews
        {
            public List<FakeView> Shown { get; } = new List<FakeView>();

            public FakeView Single()
            {
                Assert.That(Shown, Has.Count.EqualTo(1));
                return Shown[0];
            }

            public IClientProjectileView Show(string weaponId, Vector3 position, Vector3 velocity)
            {
                var view = new FakeView(weaponId, position, velocity);
                Shown.Add(view);
                return view;
            }
        }

        private sealed class FakeView : IClientProjectileView
        {
            public FakeView(string weaponId, Vector3 position, Vector3 velocity)
            {
                WeaponId = weaponId;
                Position = position;
                Velocity = velocity;
            }

            public string WeaponId { get; }

            public Vector3 Position { get; private set; }

            public Vector3 Velocity { get; private set; }

            public Vector3? StuckAt { get; private set; }

            public bool Removed { get; private set; }

            public void Move(Vector3 position, Vector3 velocity)
            {
                Assert.That(Removed, Is.False, "a removed view is not moved");
                Position = position;
                Velocity = velocity;
                StuckAt = null;
            }

            public void Stick(Vector3 position)
            {
                Position = position;
                StuckAt = position;
            }

            public void Remove()
            {
                Removed = true;
            }
        }

        // z = 一定の平らな壁だけの世界。
        private sealed class Wall : IWorldCollision
        {
            private readonly float _z;

            public Wall(float z)
            {
                _z = z;
            }

            public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
            {
                hit = default;
                var unit = Vector3.Normalize(direction);
                if (unit.Z <= 0f || origin.Z >= _z)
                {
                    return false;
                }

                var distance = (_z - origin.Z) / unit.Z;
                if (distance > maxDistance)
                {
                    return false;
                }

                hit = new WorldHit(origin + (unit * distance), -Vector3.UnitZ, distance, 1, 0);
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
