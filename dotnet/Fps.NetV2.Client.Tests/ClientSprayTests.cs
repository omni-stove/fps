using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    public sealed class ClientSprayTests
    {
        private const int _me = 1;
        private const int _other = 2;
        private const float _tickSeconds = 0.1f;
        private const float _coneDegrees = 15f;
        private const float _range = 8f;
        private const float _wallZ = 5f;
        private const string _flamerId = "a3d4f6e2-7b1c-4c8e-9f20-5e6d7c8b9a01";
        private const string _rifleId = "3a7f5e4d-6b8c-4d9e-9f0a-2b3c4d5e6f70";

        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

        private readonly List<SprayView> _drawn = new List<SprayView>();
        private readonly List<int> _hidden = new List<int>();
        private ClientSpray _spray;

        [SetUp]
        public void SetUp()
        {
            _drawn.Clear();
            _hidden.Clear();
            _spray = new ClientSpray(Find);
            _spray.Drawn += _drawn.Add;
            _spray.Hidden += _hidden.Add;
            _spray.Attach(new Wall(_wallZ), ~0, _me);
        }

        [Test]
        public void AnotherPlayersSprayIsDrawnFromTheServersFlameUntilItStops()
        {
            var start = new Vector3(1f, 1.6f, 0f);
            var end = new Vector3(1f, 1.6f, 7f);

            _spray.Receive(State(_other, _flamerId, start, end, active: true));
            _spray.Receive(State(_other, _flamerId, start, end, active: false));

            var flame = _drawn.Single();
            Assert.That(flame.ShooterId, Is.EqualTo(_other));
            Assert.That(flame.WeaponId, Is.EqualTo(_flamerId));
            Assert.That(flame.Start, Is.EqualTo(start));
            Assert.That(flame.End, Is.EqualTo(end));
            Assert.That(flame.TickSeconds, Is.EqualTo(_tickSeconds));
            Assert.That(flame.ConeDegrees, Is.EqualTo(_coneDegrees));
            Assert.That(_hidden, Is.EqualTo(new[] { _other }));
        }

        [Test]
        public void ASprayOfAnUnknownWeaponIsDrawnWithTheDefaults()
        {
            _spray.Receive(State(_other, "unknown", _eye, _eye + Vector3.UnitZ, active: true));

            var flame = _drawn.Single();
            Assert.That(flame.TickSeconds, Is.EqualTo(SprayView.DefaultHoldSeconds));
            Assert.That(flame.ConeDegrees, Is.EqualTo(SprayView.DefaultConeDegrees));
        }

        [Test]
        public void ASprayWithoutADirectionIsNotDrawnAndAStopForAnUnshownSprayHidesNothing()
        {
            _spray.Receive(State(_other, _flamerId, _eye, _eye, active: true));
            _spray.Receive(State(_other, _flamerId, _eye, _eye, active: false));

            Assert.That(_drawn, Is.Empty);
            Assert.That(_hidden, Is.Empty);
        }

        [Test]
        public void TheShooterDrawsItsOwnSprayAlongItsAimWithoutWaitingForTheServer()
        {
            _spray.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Flamer());

            Assert.That(_spray.IsLocalFiring, Is.True);
            var first = _drawn.Single();
            Assert.That(first.ShooterId, Is.EqualTo(_me));
            Assert.That(first.Start, Is.EqualTo(_eye));
            Assert.That(first.End.Z, Is.EqualTo(_wallZ).Within(1e-4f), "the flame stops at the wall");
            Assert.That(first.ConeDegrees, Is.EqualTo(_coneDegrees));

            // 視線を振ると火炎も追い、壁が無ければ射程まで出る
            _spray.Update(_eye, -Vector3.UnitZ);
            Assert.That(Vector3.Distance(_drawn.Last().End, _eye - (Vector3.UnitZ * _range)), Is.LessThan(1e-3f));

            // 本人宛ての噴射の知らせは遅れた視線なので使わない
            _spray.Receive(State(_me, _flamerId, _eye, _eye + Vector3.UnitX, active: true));
            Assert.That(_drawn.Count, Is.EqualTo(2));

            _spray.LocalTrigger(Request(TriggerEdge.Released, Vector3.UnitZ), Flamer());
            _spray.Update(_eye, Vector3.UnitZ);

            Assert.That(_spray.IsLocalFiring, Is.False);
            Assert.That(_hidden, Is.EqualTo(new[] { _me }));
            Assert.That(_drawn.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheServerStoppingTheShootersSprayHidesItUntilTheNextPress()
        {
            _spray.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Flamer());

            _spray.Receive(State(_me, _flamerId, _eye, _eye, active: false));
            _spray.Update(_eye, Vector3.UnitZ);

            Assert.That(_hidden, Is.EqualTo(new[] { _me }));
            Assert.That(_drawn.Count, Is.EqualTo(1));

            _spray.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Flamer());
            Assert.That(_drawn.Count, Is.EqualTo(2));
        }

        [Test]
        public void AWeaponThatIsNotASprayDrawsNoFlame()
        {
            _spray.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Rifle());

            Assert.That(_spray.IsLocalFiring, Is.False);
            Assert.That(_drawn, Is.Empty);
        }

        [Test]
        public void NothingIsDrawnBeforeAttaching()
        {
            var spray = new ClientSpray(Find);
            var drawn = new List<SprayView>();
            spray.Drawn += drawn.Add;

            spray.Receive(State(_other, _flamerId, _eye, _eye + Vector3.UnitZ, active: true));
            spray.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Flamer());

            Assert.That(drawn, Is.Empty);
        }

        [Test]
        public void DetachingHidesEveryFlame()
        {
            _spray.Receive(State(_other, _flamerId, _eye, _eye + Vector3.UnitZ, active: true));
            _spray.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Flamer());

            _spray.Detach();

            Assert.That(_hidden, Is.EquivalentTo(new[] { _me, _other }));
            Assert.That(_spray.IsAttached, Is.False);
        }

        [Test]
        public void TheHooksSendTheShootersTriggerAndSprayStatesHere()
        {
            var hooks = new ClientWeaponHooks(Find);
            var drawn = new List<SprayView>();
            hooks.Spray.Drawn += drawn.Add;
            hooks.Spray.Attach(new Wall(_wallZ), ~0, _me);

            hooks.LocalFired(Request(TriggerEdge.Pressed, Vector3.UnitZ), Flamer());
            Assert.That(hooks.Handle(State(_other, _flamerId, _eye, _eye + Vector3.UnitZ, active: true)), Is.True);

            Assert.That(drawn.Select(d => d.ShooterId), Is.EqualTo(new[] { _me, _other }));
        }

        private static WeaponSpec Find(string weaponId)
        {
            return weaponId == _flamerId ? Flamer() : weaponId == _rifleId ? Rifle() : null;
        }

        private static WeaponSpec Flamer()
        {
            return new WeaponSpec(
                _flamerId,
                "weapon.flamethrower",
                WeaponCategory.AssaultRifle,
                round: new RoundSpec(RoundDelivery.Spray, _range, spray: new SpraySpec(_tickSeconds, _coneDegrees)));
        }

        private static WeaponSpec Rifle()
        {
            return new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle);
        }

        private static FireRequestMessage Request(TriggerEdge edge, Vector3 direction)
        {
            return new FireRequestMessage { WeaponId = _flamerId, Origin = _eye, Direction = direction, Trigger = edge };
        }

        private static BeamStateMessage State(int shooterId, string weaponId, Vector3 start, Vector3 end, bool active)
        {
            return new BeamStateMessage { ShooterId = shooterId, WeaponId = weaponId, Start = start, End = end, Active = active };
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
