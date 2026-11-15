using System;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // フラッシュが、見えている人に距離と向きで長さを決めた目くらましをかけ、全員に知らせることを確かめる
    public sealed partial class ServerCombatTests
    {
        private static readonly BlindSpec _blind = new BlindSpec(15f, 4f, 0.25f);

        private static readonly GrenadeSpec _flash = new GrenadeSpec(
            "bcc273d5-24a8-42f4-bb3c-b054d001434d",
            "grenade.flash",
            new ImpactSpec(damage: 0, blind: new BlindSpec(40f, 4f, 0.25f)),
            fuseSeconds: 0.5f);

        [Test]
        public void LookingAtTheFlashBlindsLongerThanTurningAwayFromIt()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            var toward = BlindSecondsOfBravo(MathF.PI * -0.5f);
            Step(_blind.DurationSeconds + 0.1f);
            var away = BlindSecondsOfBravo(MathF.PI * 0.5f);

            // 目から 3 m なので、正面なら 4 × (1 - 3/15) 秒、真後ろならその 0.25 倍
            Assert.That(toward, Is.EqualTo(3.2f).Within(0.05f), _log.ToString());
            Assert.That(away, Is.EqualTo(0.8f).Within(0.05f), _log.ToString());
        }

        [Test]
        public void AFlashFartherThanItsRadiusDoesNotBlind()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            _combat.Flash.Detonate(Body(2).EyePosition + new Vector3(0f, 0f, _blind.Radius + 1f), _blind, 1, ServerCombat.GrenadeWeaponId);

            Assert.That(_transport.Read<StatusEffectMessage>(2, Copy).Where(m => m.TargetId == 2), Is.Empty);
        }

        [Test]
        public void AWallBetweenTheFlashAndTheEyesKeepsThemSeeing()
        {
            Start(Rules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            _combat.Flash.Detonate(new Vector3(-2f, Body(1).EyePosition.Y, 0f), _blind, 1, ServerCombat.GrenadeWeaponId);

            var blinded = _transport.Read<StatusEffectMessage>(2, Copy).Select(m => m.TargetId).ToList();
            Assert.That(blinded, Does.Contain(1), "the thrower on the near side is blinded");
            Assert.That(blinded, Does.Not.Contain(2), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Flash] blocked victim=2"));
        }

        [Test]
        public void AThrownFlashBlindsEveryoneWhoSeesItWithoutExploding()
        {
            Start(Rules(), grenades: new GrenadeCatalog(new[] { _flash }));
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX, GrenadeTypeId = _flash.Id });
            Step(_flash.FuseSeconds + 0.1f);

            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("[Flash] net flashed thrower=1"));
            Assert.That(_log.ToString(), Does.Not.Contain("[Grenade] net exploded"), "a flash does not explode");
            Assert.That(_transport.Read<ExplosionMessage>(2, m => new ExplosionMessage()), Is.Empty);
            for (var receiver = 1; receiver <= 2; receiver++)
            {
                var status = _transport.Read<StatusEffectMessage>(receiver, Copy).Single(m => m.TargetId == 2);
                Assert.That(status.KindId, Is.EqualTo(StatusEffectKinds.Blind));
                Assert.That(status.RemainingSeconds, Is.GreaterThan(0f));
            }

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "being blinded does not hurt");
        }

        [Test]
        public void TheBlindnessEndsAndEveryoneHearsIt()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            var seconds = BlindSecondsOfBravo(MathF.PI * -0.5f);
            Step(seconds + 0.1f);

            var status = _transport.Read<StatusEffectMessage>(1, Copy).Where(m => m.TargetId == 2).ToList();
            Assert.That(status.Select(m => m.RemainingSeconds).Last(), Is.Zero);
            Assert.That(_combat.StatusEffects.RemainingSeconds(2, StatusEffectKinds.Blind), Is.Zero);
        }

        // 2 を yaw (ラジアン) に向かせ、2 の目の 3 m 西 (-X) でフラッシュを起こして、2 にかかった秒数を返す
        private float BlindSecondsOfBravo(float yaw)
        {
            Face(2, yaw);
            var before = _transport.Read<StatusEffectMessage>(2, Copy).Count;
            _combat.Flash.Detonate(Body(2).EyePosition - new Vector3(3f, 0f, 0f), _blind, 1, ServerCombat.GrenadeWeaponId);
            var status = _transport.Read<StatusEffectMessage>(2, Copy).Skip(before).Single(m => m.TargetId == 2);
            Assert.That(status.KindId, Is.EqualTo(StatusEffectKinds.Blind));
            return status.RemainingSeconds;
        }

        private void Face(int id, float yaw)
        {
            var input = new InputMessage();
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, yaw, InputButtons.None));
            _host.OnMessage(new ConnectionId((ulong)id), input);
            Tick();
            Assert.That(Body(id).Yaw, Is.EqualTo(yaw).Within(1e-4f));
        }

        private static StatusEffectMessage Copy(StatusEffectMessage m)
        {
            return new StatusEffectMessage { TargetId = m.TargetId, KindId = m.KindId, RemainingSeconds = m.RemainingSeconds };
        }
    }
}
