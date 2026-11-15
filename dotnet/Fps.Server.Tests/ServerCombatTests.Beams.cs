using System;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // ビーム (RoundDelivery.Beam) は押した・離したの間をサーバーが数え、照射の間合いごとに当て直す
    public sealed partial class ServerCombatTests
    {
        private const float _beamTickSeconds = 0.05f;
        private const int _beamDamage = 2;

        // 押した瞬間に 1 回、その後は照射の間合いごと
        [TestCase(30, 11)]
        [TestCase(60, 21)]
        public void ABeamDamagesForAsLongAsItIsHeld(int heldTicks, int expectedHits)
        {
            Start(Rules(), rifle: BeamRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            PressAt(1, Body(2).Center);
            for (var i = 0; i < heldTicks; i++)
            {
                Tick();
            }

            Release(1);
            Step(0.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - (_beamDamage * expectedHits)), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Combat] net beam stop shooter=1 weapon=" + TestRules.RifleId + " ticks=" + expectedHits));
        }

        [Test]
        public void ABeamIsSentToEveryoneWhileItFiresAndEndsOnRelease()
        {
            Start(Rules(), rifle: BeamRifle());
            Join(1);
            Join(2);
            Step(0.2f);
            var target = Body(2).Center;

            PressAt(1, target);
            Step(0.1f);
            Release(1);

            foreach (var receiver in new[] { 1, 2 })
            {
                var states = _transport.Read<BeamStateMessage>(receiver, Copy);
                Assert.That(states.Count, Is.GreaterThan(2));
                var firing = states.Take(states.Count - 1).ToList();
                Assert.That(firing.All(s => s.Active && s.ShooterId == 1 && s.WeaponId == TestRules.RifleId), Is.True);
                Assert.That(Vector3.Distance(firing[0].Start, Body(1).EyePosition), Is.LessThan(1e-3f));
                Assert.That(Vector3.Distance(firing[0].End, target), Is.LessThan(1f), "the line ends where it hit");
                Assert.That(states[^1].Active, Is.False);
            }

            Assert.That(_transport.Read<ShotMessage>(2, m => m), Is.Empty, "a beam is shown by its own messages");
        }

        [Test]
        public void ABeamFollowsTheShootersTurn()
        {
            Start(Rules(), rifle: BeamRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            PressAt(1, Body(2).Center);
            Turn(1, Body(1).Yaw + MathF.PI);
            var health = _rules.Session.GetHealth(2);
            Step(0.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(health), _log.ToString());
            var last = _transport.Read<BeamStateMessage>(2, Copy).Last();
            Assert.That(last.Active, Is.True);
            Assert.That(Vector3.Dot(last.End - last.Start, Body(2).Center - Body(1).EyePosition), Is.LessThan(0f), "the beam turned away");
        }

        [Test]
        public void ABeamUsesARoundEachTickAndStopsWhenTheMagazineIsEmpty()
        {
            const int magazine = 5;
            Start(Rules(), rifle: BeamRifle(new AmmoSpec(magazineSize: magazine, reserveAmmo: 0)));
            Join(1);
            Join(2);
            Step(0.2f);

            PressAt(1, Body(2).Center);
            Step(1f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - (_beamDamage * magazine)), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("net beam stop shooter=1 weapon=" + TestRules.RifleId + " ticks=" + magazine));
            Assert.That(_log.ToString(), Does.Contain("reason=" + ShotRejection.OutOfAmmo));
            Assert.That(_transport.Read<BeamStateMessage>(1, Copy).Last().Active, Is.False, "the shooter learns that the server stopped its beam");
        }

        [Test]
        public void ABeamStopsWhenTheShooterDies()
        {
            Start(Rules(), rifle: BeamRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            PressAt(1, Body(2).Center);
            Tick();
            _rules.ApplyDamage(2, 1, 1000);
            var health = _rules.Session.GetHealth(2);
            Step(0.3f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(health));
            Assert.That(_transport.Read<BeamStateMessage>(2, Copy).Last().Active, Is.False);
        }

        [Test]
        public void PressingAgainRightAwayDoesNotHitFasterThanTheBeamTicks()
        {
            Start(Rules(), rifle: BeamRifle(fireInterval: _tickSeconds / 2f));
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < 6; i++)
            {
                PressAt(1, Body(2).Center);
                Tick();
                Release(1);
            }

            // 毎 tick 押し直しても、当たるのは照射の間合い (3 tick) ごと
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - (_beamDamage * 2)), _log.ToString());
        }

        [Test]
        public void AShotWithoutAPressHitsOnce()
        {
            Start(Rules(), rifle: BeamRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);
            Step(0.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - _beamDamage), _log.ToString());
        }

        [Test]
        public void ShotsWithoutAPressRightAfterABeamDoNotHitFasterThanTheBeamTicks()
        {
            Start(Rules(), rifle: BeamRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            PressAt(1, Body(2).Center);
            for (var i = 0; i < 3; i++)
            {
                Tick();
            }

            Release(1);
            for (var i = 0; i < 3; i++)
            {
                FireAt(1, Body(2).Center);
            }

            // 押した瞬間と 3 tick 目の 2 回だけ。離した直後の押さない射撃は、前の照射からの待ちが明けるまで当たらない
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - (_beamDamage * 2)), _log.ToString());
        }

        [Test]
        public void ABeamStopsWhenItsStartSwingsBehindAWall()
        {
            Start(Rules(), wall: true, rifle: BeamRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            // 壁 (x = 0) の手前まで +x へ歩く
            for (var i = 0; i < 600 && Body(1).Position.X < -1.5f; i++)
            {
                var input = new InputMessage();
                input.Add(new InputFrame(++_inputTick, new Vector2(0f, 1f), MathF.PI / 2f, InputButtons.None));
                _host.OnMessage(new ConnectionId(1), input);
                Tick();
            }

            Turn(1, MathF.PI / 2f);
            Assert.That(Body(1).Position.X, Is.GreaterThan(-2.5f), "the shooter stands close to the wall");

            // 横にずらした始点で押し、振り向いて始点を壁の向こうへ回す
            var eye = Body(1).EyePosition;
            Send(1, Trigger(TriggerEdge.Pressed, eye + new Vector3(0f, 0f, -2.5f), Vector3.UnitX));
            Turn(1, 0f);
            Step(0.2f);

            Assert.That(_log.ToString(), Does.Contain("net beam stop shooter=1 weapon=" + TestRules.RifleId), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("reason=" + ShotRejection.OriginBlocked));
            Assert.That(_transport.Read<BeamStateMessage>(2, Copy).Last().Active, Is.False);
        }

        private static WeaponSpec BeamRifle(AmmoSpec ammo = null, float? fireInterval = null)
        {
            var rifle = TestRules.Rifle();
            var round = new RoundSpec(RoundDelivery.Beam, rifle.Round.Range, beam: new BeamSpec(_beamTickSeconds));
            var impact = new ImpactSpec(_beamDamage, DamageFalloff.None, rifle.Impact.StructureRadius);
            var trigger = fireInterval.HasValue ? new TriggerSpec(rifle.Trigger.FireMode, fireInterval.Value) : rifle.Trigger;
            return new WeaponSpec(
                rifle.Id, rifle.NameKey, rifle.Category, trigger, ammo ?? rifle.Ammo, round, impact, rifle.Aim, rifle.AmmoTypes, rifle.ShotNoiseRadius);
        }

        private void PressAt(int shooter, Vector3 target)
        {
            var eye = Body(shooter).EyePosition;
            Send(shooter, Trigger(TriggerEdge.Pressed, eye, target - eye));
        }

        private void Release(int shooter)
        {
            Send(shooter, Trigger(TriggerEdge.Released, Body(shooter).EyePosition, Vector3.UnitZ));
        }

        private FireRequestMessage Trigger(TriggerEdge edge, Vector3 origin, Vector3 direction)
        {
            return new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = origin,
                Direction = direction,
                ClientTick = (int)_host.TickCount,
                InterpolationAlpha = 0f,
                ShotIndex = _shotIndex++,
                ShotGeneration = 0,
                Trigger = edge,
            };
        }

        private void Turn(int id, float yaw)
        {
            var input = new InputMessage();
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, yaw, InputButtons.None));
            _host.OnMessage(new ConnectionId((ulong)id), input);
            Tick();
        }

        private static BeamStateMessage Copy(BeamStateMessage m)
        {
            return new BeamStateMessage { ShooterId = m.ShooterId, WeaponId = m.WeaponId, Start = m.Start, End = m.End, Active = m.Active };
        }
    }
}
