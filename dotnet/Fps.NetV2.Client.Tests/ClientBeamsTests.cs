using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    public sealed class ClientBeamsTests
    {
        private const int _me = 1;
        private const int _other = 2;
        private const float _tickSeconds = 0.05f;
        private const float _range = 60f;
        private const float _wallZ = 20f;
        private const string _laserId = "739a532a-1faa-498e-b9e1-20de68186e40";
        private const string _rifleId = "3a7f5e4d-6b8c-4d9e-9f0a-2b3c4d5e6f70";

        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

        private readonly List<BeamView> _drawn = new List<BeamView>();
        private readonly List<int> _hidden = new List<int>();
        private ClientBeams _beams;

        [SetUp]
        public void SetUp()
        {
            _drawn.Clear();
            _hidden.Clear();
            _beams = new ClientBeams(Find);
            _beams.Drawn += _drawn.Add;
            _beams.Hidden += _hidden.Add;
            _beams.Attach(new Wall(_wallZ), ~0, _me);
        }

        [Test]
        public void AnotherPlayersBeamIsDrawnFromTheServersLineUntilItStops()
        {
            var start = new Vector3(1f, 1.6f, 0f);
            var end = new Vector3(1f, 1.6f, 7f);

            _beams.Receive(State(_other, _laserId, start, end, active: true));
            _beams.Receive(State(_other, _laserId, start, end, active: false));

            var line = _drawn.Single();
            Assert.That(line.ShooterId, Is.EqualTo(_other));
            Assert.That(line.WeaponId, Is.EqualTo(_laserId));
            Assert.That(line.Start, Is.EqualTo(start));
            Assert.That(line.End, Is.EqualTo(end));
            Assert.That(line.TickSeconds, Is.EqualTo(_tickSeconds));
            Assert.That(_hidden, Is.EqualTo(new[] { _other }));
        }

        [Test]
        public void ABeamOfAnUnknownWeaponIsStillDrawn()
        {
            _beams.Receive(State(_other, "unknown", _eye, _eye + Vector3.UnitZ, active: true));

            Assert.That(_drawn.Single().TickSeconds, Is.EqualTo(BeamView.DefaultHoldSeconds));
        }

        [Test]
        public void AStopForABeamThatWasNeverShownHidesNothing()
        {
            _beams.Receive(State(_other, _laserId, _eye, _eye, active: false));

            Assert.That(_hidden, Is.Empty);
        }

        [Test]
        public void TheShooterDrawsItsOwnBeamAlongItsAimWithoutWaitingForTheServer()
        {
            _beams.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Laser());

            Assert.That(_beams.IsLocalFiring, Is.True);
            var first = _drawn.Single();
            Assert.That(first.ShooterId, Is.EqualTo(_me));
            Assert.That(first.Start, Is.EqualTo(_eye));
            Assert.That(first.End.Z, Is.EqualTo(_wallZ).Within(1e-4f), "the line stops at the wall");

            // 視線を振ると線も追う
            _beams.Update(_eye, -Vector3.UnitZ);
            var turned = _drawn.Last();
            Assert.That(Vector3.Distance(turned.End, _eye - (Vector3.UnitZ * _range)), Is.LessThan(1e-3f));

            // 本人宛ての照射の知らせは遅れた視線なので使わない
            _beams.Receive(State(_me, _laserId, _eye, _eye + Vector3.UnitX, active: true));
            Assert.That(_drawn.Count, Is.EqualTo(2));

            _beams.LocalTrigger(Request(TriggerEdge.Released, Vector3.UnitZ), Laser());
            _beams.Update(_eye, Vector3.UnitZ);

            Assert.That(_beams.IsLocalFiring, Is.False);
            Assert.That(_hidden, Is.EqualTo(new[] { _me }));
            Assert.That(_drawn.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheServerStoppingTheShootersBeamHidesItUntilTheNextPress()
        {
            _beams.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Laser());

            _beams.Receive(State(_me, _laserId, _eye, _eye, active: false));
            _beams.Update(_eye, Vector3.UnitZ);

            Assert.That(_hidden, Is.EqualTo(new[] { _me }));
            Assert.That(_drawn.Count, Is.EqualTo(1));

            _beams.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Laser());
            Assert.That(_drawn.Count, Is.EqualTo(2));
        }

        [Test]
        public void AWeaponThatIsNotABeamDrawsNoLine()
        {
            _beams.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Rifle());

            Assert.That(_beams.IsLocalFiring, Is.False);
            Assert.That(_drawn, Is.Empty);
        }

        [Test]
        public void DetachingHidesEveryLine()
        {
            _beams.Receive(State(_other, _laserId, _eye, _eye + Vector3.UnitZ, active: true));
            _beams.LocalTrigger(Request(TriggerEdge.Pressed, Vector3.UnitZ), Laser());

            _beams.Detach();

            Assert.That(_hidden, Is.EquivalentTo(new[] { _me, _other }));
            Assert.That(_beams.IsAttached, Is.False);
        }

        [Test]
        public void TheHooksSendTheShootersTriggerAndBeamStatesHere()
        {
            var hooks = new ClientWeaponHooks(Find);
            var drawn = new List<BeamView>();
            hooks.Beams.Drawn += drawn.Add;
            hooks.Beams.Attach(new Wall(_wallZ), ~0, _me);

            hooks.LocalFired(Request(TriggerEdge.Pressed, Vector3.UnitZ), Laser());
            Assert.That(hooks.Handle(State(_other, _laserId, _eye, _eye + Vector3.UnitZ, active: true)), Is.True);

            Assert.That(drawn.Select(d => d.ShooterId), Is.EqualTo(new[] { _me, _other }));
        }

        private static WeaponSpec Find(string weaponId)
        {
            return weaponId == _laserId ? Laser() : weaponId == _rifleId ? Rifle() : null;
        }

        private static WeaponSpec Laser()
        {
            return new WeaponSpec(
                _laserId,
                "weapon.laser",
                WeaponCategory.AssaultRifle,
                round: new RoundSpec(RoundDelivery.Beam, _range, beam: new BeamSpec(_tickSeconds)));
        }

        private static WeaponSpec Rifle()
        {
            return new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle);
        }

        private static FireRequestMessage Request(TriggerEdge edge, Vector3 direction)
        {
            return new FireRequestMessage { WeaponId = _laserId, Origin = _eye, Direction = direction, Trigger = edge };
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
