using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 盾の武器を最初の武器に持たせ、撃つ側 (1) は拳銃に持ち替えて盾を持つ側 (2) を撃つ
    public sealed partial class ServerCombatTests
    {
        private const int _handgunDamage = 34;

        // Bravo (x=5) から Alpha (x=-5) の方を向く向き (ラジアン)
        private const float _bravoFacesAlpha = -MathF.PI / 2f;
        private const float _bravoFacesAway = MathF.PI / 2f;

        private static WeaponSpec ShieldWeapon(RiotShieldSpec shield = null) => new WeaponSpec(
            TestRules.RifleId,
            "weapon.riotShield",
            WeaponCategory.AssaultRifle,
            ammo: new AmmoSpec(1, 0, 1f),
            impact: new ImpactSpec(25),
            weight: 6f,
            shield: shield ?? RiotShieldSpec.Default);

        [Test]
        public void TheShieldStopsAShotFromTheFrontAndTheBackIsHit()
        {
            StartWithShield();

            Face(2, _bravoFacesAlpha);
            FireHandgunAt(Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain(
                "[Combat] riot-shield blocked id=2 attacker=1 weapon=" + TestRules.HandgunId + " dmg=" + _handgunDamage));
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => m), Is.Empty, "a blocked shot is not a hit");
            Assert.That(_transport.Read<DamageTakenMessage>(2, m => m), Is.Empty);
            var shot = _transport.Read<ShotMessage>(2, m => new ShotMessage { HasImpact = m.HasImpact, ImpactIsCharacter = m.ImpactIsCharacter }).Single();
            Assert.That(shot.HasImpact, Is.True);
            Assert.That(shot.ImpactIsCharacter, Is.False, "the shot sparks on the shield");

            Face(2, _bravoFacesAway);
            Step(0.3f);
            FireHandgunAt(Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - _handgunDamage), _log.ToString());
        }

        [Test]
        public void SprintingLowersTheShieldAndTheShotHits()
        {
            StartWithShield();

            Face(2, _bravoFacesAlpha, InputButtons.Sprint);
            Assert.That(Body(2).RaisedShield, Is.Null);
            FireHandgunAt(Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - _handgunDamage), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Combat] riot-shield lowered id=2 cause=" + RiotShieldLog.SprintCause));

            Face(2, _bravoFacesAlpha);
            Assert.That(Body(2).RaisedShield, Is.SameAs(RiotShieldSpec.Default));
            Assert.That(_log.ToString(), Does.Contain("[Combat] riot-shield raised id=2"));
        }

        [Test]
        public void TheShieldIsOutWhileHeldAndAwayAfterSwitching()
        {
            StartWithShield();

            Assert.That(_log.ToString(), Does.Contain("[Combat] riot-shield out id=2 weapon=" + TestRules.RifleId));
            Assert.That(Body(2).RaisedShield, Is.SameAs(RiotShieldSpec.Default));
            Assert.That(Body(1).RaisedShield, Is.Null, "player 1 switched to the handgun");
            Assert.That(_log.ToString(), Does.Not.Contain("[Combat] riot-shield out id=1"), "player 1 started switching before the first tick");

            _host.OnMessage(new ConnectionId(2), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });
            Tick();

            Assert.That(Body(2).RaisedShield, Is.Null, "the shield is put away as the switch starts");
            Assert.That(_log.ToString(), Does.Contain("[Combat] riot-shield away id=2 weapon=" + TestRules.RifleId));
        }

        [Test]
        public void TheShieldWeaponNeitherFiresNorReloads()
        {
            StartWithShield();
            var eye = Body(2).EyePosition;

            Fire(2, eye, Body(1).Center - eye, (int)_host.TickCount, TestRules.RifleId);
            _host.OnMessage(new ConnectionId(2), new WeaponActionRequestMessage { Action = WeaponAction.Reload });

            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected shot attacker=2 reason=ShieldWeapon"));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] reload start client=2"));
            Assert.That(_transport.Read<ShotMessage>(1, m => m), Is.Empty);
        }

        [Test]
        public void ARaisedShieldSlowsTheHolderAndSprintingDropsIt()
        {
            var shield = new RiotShieldSpec(moveSpeedMultiplier: 0.5f);
            StartWithShield(shield);

            Face(2, _bravoFacesAlpha);
            var raised = OwnerCarrySpeed(2);
            Face(2, _bravoFacesAlpha, InputButtons.Sprint);
            var lowered = OwnerCarrySpeed(2);

            Assert.That(raised, Is.EqualTo(lowered * 0.5f).Within(1e-5f));
        }

        [Test]
        public void AShieldWeaponInTheRulesFileIsReadBackWithItsShield()
        {
            var shield = new RiotShieldSpec(90f, 0.7f, 1.4f, 0.6f);
            var text = NetMatchFile.Write(TestRules.File(Rules(), rifle: ShieldWeapon(shield)));

            var read = NetMatchFile.Parse(text, "shield.json").LoadoutWeapons[0];

            Assert.That(read.IsShield, Is.True);
            Assert.That(read.Shield.BlockArcDegrees, Is.EqualTo(90f));
            Assert.That(read.Shield.Width, Is.EqualTo(0.7f));
            Assert.That(read.Shield.Height, Is.EqualTo(1.4f));
            Assert.That(read.Shield.MoveSpeedMultiplier, Is.EqualTo(0.6f));
        }

        [Test]
        public void ADeadHoldersShieldIsPutAway()
        {
            StartWithShield();

            _rules.ApplyDamage(1, 2, 1000);
            Tick();

            Assert.That(Body(2).RaisedShield, Is.Null);
        }

        private void StartWithShield(RiotShieldSpec shield = null)
        {
            Start(Rules(), rifle: ShieldWeapon(shield));
            Join(1);
            Join(2);
            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });
            Step(0.6f);
        }

        private void FireHandgunAt(Vector3 target)
        {
            var eye = Body(1).EyePosition;
            Fire(1, eye, target - eye, (int)_host.TickCount, TestRules.HandgunId);
        }

        // 動かずに向きだけを変え、戦闘の tick で盾の向きを当たり判定に記録させる
        private void Face(int id, float yaw, InputButtons buttons = InputButtons.None)
        {
            var input = new InputMessage();
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, yaw, buttons));
            _host.OnMessage(new ConnectionId((ulong)id), input);
            Tick();
        }

        private float OwnerCarrySpeed(int receiverId)
        {
            var snapshot = new SnapshotMessage();
            foreach (var player in _match.Players)
            {
                snapshot.Players.Add().PlayerId = player.PlayerId;
            }

            Assert.That(_match.TryGetPlayer(receiverId, out var receiver), Is.True);
            _movement.Contribute(snapshot, receiver);
            return snapshot.Owner.CarrySpeedMultiplier;
        }
    }
}
