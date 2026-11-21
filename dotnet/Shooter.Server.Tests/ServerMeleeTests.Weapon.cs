using System;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 持っている近接武器の溜めた振りと投げ (ServerMelee.Weapon) を確かめる。
    // 溜めは近接のボタンを押している長さ、投げは照準のボタンを押した瞬間で決める
    public sealed partial class ServerMeleeTests
    {
        private const string _macheteId = "e1920316-c182-4151-9273-26f94c3adaf9";

        // ふつうの振り 20、溜め切った振り 60。0.2 秒より長く押すと溜まり始め、そこから 0.4 秒で溜め切る
        private static readonly MeleeSpec _macheteSwing = new MeleeSpec(
            damage: 20, reach: 2f, arcDegrees: 90f, swingSeconds: 0.4f, cooldownSeconds: 0.1f, impactSeconds: 0.1f, lungeDistance: 0f);

        private static readonly MeleeSpec _macheteHeavy = new MeleeSpec(
            damage: 60, reach: 2f, arcDegrees: 90f, swingSeconds: 0.6f, cooldownSeconds: 0.2f, impactSeconds: 0.2f, lungeDistance: 0f);

        // 上へ起こさずに投げ、2 秒で拾える物に変わる
        private static readonly MeleeThrowSpec _macheteThrow = new MeleeThrowSpec(
            damage: 70, speed: 20f, upwardDegrees: 0f, spinDegreesPerSecond: 0f, radius: 0.1f, maxFlightSeconds: 2f);

        [Test]
        public void ATapSwingsTheLightSwingOfTheHeldMeleeWeapon()
        {
            StartWithMachete();
            Place(2, -3.5f, 0f);
            Face(1);

            Press(1);
            Hold(1, InputButtons.None, 60);

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Melee] weapon-swing id=1 weapon=" + _macheteId + " charge=0.00"));
            Assert.That(log, Does.Not.Contain("[Melee] swing id=1"));
            Assert.That(log, Does.Contain("[Combat] dealt attacker=1 victim=2 weapon=" + _macheteId + " zone=Body dmg=20 outcome=Damaged"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 20));
        }

        [Test]
        public void HoldingChargesAndReleasingSwingsTheChargedSwing()
        {
            StartWithMachete();
            Place(2, -3.5f, 0f);
            Face(1);

            Hold(1, InputButtons.Melee, 48);

            Assert.That(_melee.IsCharging(1), Is.True);
            Assert.That(_melee.IsSwinging(1), Is.False, "the swing waits for the release");
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));

            Hold(1, InputButtons.None, 60);

            Assert.That(_melee.IsCharging(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Melee] weapon-swing id=1 weapon=" + _macheteId + " charge=1.00"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 60), _log.ToString());
        }

        // 0.4 秒押すと、溜まり始めてから 0.2 秒で溜めの半分。ダメージはふつうの 20 と溜め切った 60 の間の 40
        [Test]
        public void ReleasingHalfwaySwingsBetweenTheLightAndTheHeavySwing()
        {
            StartWithMachete();
            Place(2, -3.5f, 0f);
            Face(1);

            Hold(1, InputButtons.Melee, 24);
            Hold(1, InputButtons.None, 60);

            Assert.That(_log.ToString(), Does.Contain("[Melee] weapon-swing id=1 weapon=" + _macheteId + " charge=0.50"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 40), _log.ToString());
        }

        // 入力の tick を飛ばして送っても、届いた時刻の差より長くは溜まらない
        [Test]
        public void SkippedInputTicksDoNotChargeFasterThanTheyArrive()
        {
            StartWithMachete();
            Place(2, -3.5f, 0f);
            Face(1);

            Send(1, new InputFrame(++_inputTick, Vector2.Zero, _facingEast, InputButtons.Melee));
            Tick();
            _inputTick += 600;
            Send(1, new InputFrame(_inputTick, Vector2.Zero, _facingEast, InputButtons.None));
            Hold(1, InputButtons.None, 60);

            Assert.That(_log.ToString(), Does.Not.Contain("charge=1.00"));
            Assert.That(_rules.Session.GetHealth(2), Is.GreaterThan(NetMatchSettings.DefaultMaxHealth - 60), _log.ToString());
        }

        [Test]
        public void SwitchingAwayDropsTheCharge()
        {
            StartWithMachete();
            Place(2, -3.5f, 0f);
            Face(1);

            Hold(1, InputButtons.Melee, 30);
            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.RifleId });
            Hold(1, InputButtons.Melee, 2);
            Hold(1, InputButtons.None, 60);

            Assert.That(_log.ToString(), Does.Not.Contain("[Melee] weapon-swing"));
            Assert.That(_melee.IsCharging(1), Is.False);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        // 溜められない武器は、押した瞬間にその武器のふつうの振りで振る
        [Test]
        public void AWeaponThatCannotChargeSwingsOnThePress()
        {
            Start(MeleeRules());
            Join(1);
            Join(2);
            Step(0.2f);
            Equip(1, Machete(charge: false), TestRules.Rifle());
            Place(2, -3.5f, 0f);
            Face(1);

            Press(1);

            Assert.That(_melee.IsSwinging(1), Is.True);
            Assert.That(_melee.IsCharging(1), Is.False);
            Hold(1, InputButtons.None, 60);
            Assert.That(_log.ToString(), Does.Contain("[Melee] weapon-swing id=1 weapon=" + _macheteId + " charge=0.00"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 20));
        }

        [Test]
        public void TheChargedSwingIsRefusedWhenTheRulesTurnMeleeOff()
        {
            Start(MeleeRules().WithActions(MeleeRules().Actions.WithMelee(Melee.Off)));
            Join(1);
            Join(2);
            Step(0.2f);
            Equip(1, Machete(), TestRules.Rifle());
            Place(2, -3.5f, 0f);
            Face(1);

            Hold(1, InputButtons.Melee, 48);
            Hold(1, InputButtons.None, 60);

            Assert.That(_log.ToString(), Does.Contain("[Melee] refused id=1 reason=Off"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void TheAimButtonThrowsTheHeldMeleeWeaponThatFliesAndHits()
        {
            StartWithMachete();
            Place(2, 0f, 0f);
            Face(1);

            Hold(1, InputButtons.Aim, 1);

            Assert.That(_log.ToString(), Does.Contain("[Melee] thrown id=1 weapon=" + _macheteId));
            Assert.That(_log.ToString(), Does.Contain("[Net] drop weapon player=1 weapon=" + _macheteId + " left=1"));
            Assert.That(_combat.WeaponsOf(1).Specs.Select(s => s.Id), Is.EqualTo(new[] { TestRules.RifleId }));
            Assert.That(_combat.TryGetWeapon(1, out var held, out _), Is.True);
            Assert.That(held.Id, Is.EqualTo(TestRules.RifleId));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "5 m at 20 m/s takes a quarter second");

            Hold(1, InputButtons.None, 30);

            var log = _log.ToString();
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 70), log);
            Assert.That(log, Does.Contain("[Melee] throw-hit id=1 weapon=" + _macheteId + " target=2 dmg=70 kind=Melee"));
            Assert.That(log, Does.Contain("[Combat] dealt attacker=1 victim=2 weapon=" + _macheteId + " zone=Body dmg=70 outcome=Damaged"));
            Assert.That(log, Does.Contain("[Melee] throw-landed id=1 weapon=" + _macheteId));
            Assert.That(_melee.ThrownCount, Is.Zero);
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Damage = m.Damage }).Single().Damage, Is.EqualTo(70));
        }

        // 最初に触れた 1 人にだけ与え、その後ろの人には届かない
        [Test]
        public void TheThrowHitsOnlyTheFirstTarget()
        {
            Start(MeleeRules());
            Join(1);
            Join(2);
            Join(3);
            Join(4);
            Step(0.2f);
            Equip(1, Machete(), TestRules.Rifle());
            Place(3, -5f, 6f);
            Place(2, -2f, 0f);
            Place(4, 1f, 0f);
            Face(1);

            Hold(1, InputButtons.Aim, 1);
            Hold(1, InputButtons.None, 60);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 70), _log.ToString());
            Assert.That(_rules.Session.GetHealth(4), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(Count(_log.ToString(), "[Melee] throw-hit"), Is.EqualTo(1));
        }

        [Test]
        public void AWallStopsTheThrow()
        {
            Start(MeleeRules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);
            Equip(1, Machete(), TestRules.Rifle());
            Place(2, -3.5f, 0f);
            Face(1);

            Hold(1, InputButtons.Aim, 1);
            Hold(1, InputButtons.None, 30);

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Melee] throw-world-hit id=1 weapon=" + _macheteId + " material=" + ServerCombat.DefaultSurfaceMaterialId));
            Assert.That(log, Does.Contain("[Melee] throw-landed id=1 weapon=" + _macheteId));
            Assert.That(log, Does.Not.Contain("[Melee] throw-hit"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void AThrowThatMissesLandsAndIsForgotten()
        {
            StartWithMachete();
            Place(2, -5f, 8f);
            Face(1);

            Hold(1, InputButtons.Aim, 1);
            Assert.That(_melee.ThrownCount, Is.EqualTo(1));
            Hold(1, InputButtons.None, (int)(_macheteThrow.MaxFlightSeconds / _tickSeconds) + 2);

            Assert.That(_melee.ThrownCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Melee] throw-landed id=1 weapon=" + _macheteId));
            Assert.That(_log.ToString(), Does.Not.Contain("[Melee] throw-hit"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void TheThrowIsRefusedWhileSwinging()
        {
            Start(MeleeRules());
            Join(1);
            Join(2);
            Step(0.2f);
            Equip(1, Machete(charge: false), TestRules.Rifle());
            Face(1);

            Press(1);
            Hold(1, InputButtons.Aim, 1);

            Assert.That(_melee.ThrownCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Melee] refused id=1 reason=Cooldown"));
            Assert.That(_combat.WeaponsOf(1).Specs.Select(s => s.Id), Does.Contain(_macheteId));
        }

        // 振り終えて待っているだけなら投げられる (MeleeController.TryThrowWeapon と同じ)
        [Test]
        public void TheThrowIsAllowedDuringTheCooldownAfterTheSwing()
        {
            Start(MeleeRules());
            Join(1);
            Join(2);
            Step(0.2f);
            Equip(1, Machete(charge: false), TestRules.Rifle());
            Face(1);

            Press(1);
            Hold(1, InputButtons.None, (int)(_macheteSwing.SwingSeconds / _tickSeconds) + 1);
            Assert.That(_melee.CooldownRemaining(1), Is.GreaterThan(0f));

            Hold(1, InputButtons.Aim, 1);

            Assert.That(_melee.ThrownCount, Is.EqualTo(1), _log.ToString());
        }

        [Test]
        public void AGunIsNotThrownWithTheAimButton()
        {
            StartWithTwo();
            Face(1);

            Hold(1, InputButtons.Aim, 5);

            Assert.That(_melee.ThrownCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Not.Contain("[Melee] thrown"));
            Assert.That(_combat.WeaponsOf(1).Specs.Select(s => s.Id), Does.Contain(TestRules.RifleId));
        }

        [Test]
        public void AMeleeWeaponWithoutAThrowIsKept()
        {
            Start(MeleeRules());
            Join(1);
            Join(2);
            Step(0.2f);
            Equip(1, Machete(@throw: false), TestRules.Rifle());
            Face(1);

            Hold(1, InputButtons.Aim, 5);

            Assert.That(_melee.ThrownCount, Is.Zero);
            Assert.That(_combat.WeaponsOf(1).Specs.Select(s => s.Id), Does.Contain(_macheteId));
        }

        [Test]
        public void TheThrowIsRefusedWhenTheRulesTurnMeleeOff()
        {
            Start(MeleeRules().WithActions(MeleeRules().Actions.WithMelee(Melee.Off)));
            Join(1);
            Join(2);
            Step(0.2f);
            Equip(1, Machete(), TestRules.Rifle());
            Face(1);

            Hold(1, InputButtons.Aim, 1);

            Assert.That(_melee.ThrownCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Melee] refused id=1 reason=Off"));
            Assert.That(_combat.WeaponsOf(1).Specs.Select(s => s.Id), Does.Contain(_macheteId));
        }

        // 東 (yaw = 90 度) へ水平に、真上へ起こすと上を向く
        [Test]
        public void TheLaunchDirectionIsTheYawRaisedByTheUpwardAngle()
        {
            var flat = ServerMelee.LaunchDirection(_facingEast, 0f);
            Assert.That(flat.X, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(flat.Y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(flat.Z, Is.EqualTo(0f).Within(1e-5f));

            var raised = ServerMelee.LaunchDirection(0f, 30f);
            Assert.That(raised.Y, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(raised.Z, Is.EqualTo(MathF.Sqrt(3f) / 2f).Within(1e-5f));
            Assert.That(raised.Length(), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void DroppingAWeaponKeepsTheOthersAndTheHeldOne()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(7, log, () => 0d);
            weapons.Equip(new[] { TestRules.Rifle(), Machete(), TestRules.Handgun() }, 0f, GameRules.Default);

            Assert.That(weapons.Drop(_macheteId, out var dropped), Is.True);

            Assert.That(dropped.Id, Is.EqualTo(_macheteId));
            Assert.That(weapons.Specs.Select(s => s.Id), Is.EqualTo(new[] { TestRules.RifleId, TestRules.HandgunId }));
            Assert.That(weapons.CurrentSpec.Id, Is.EqualTo(TestRules.RifleId));
            Assert.That(log.ToString(), Does.Contain("[Net] drop weapon player=7 weapon=" + _macheteId + " left=2"));
        }

        [Test]
        public void DroppingTheHeldWeaponHoldsTheNextOne()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(7, log, () => 0d);
            weapons.Equip(new[] { Machete(), TestRules.Rifle(), TestRules.Handgun() }, 0f, GameRules.Default);

            Assert.That(weapons.Drop(_macheteId, out _), Is.True);

            Assert.That(weapons.CurrentSpec.Id, Is.EqualTo(TestRules.RifleId));
            Assert.That(weapons.Sync().IsSwitching, Is.False);
        }

        [Test]
        public void DroppingTheLastWeaponLeavesNothing()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(7, log, () => 0d);
            weapons.Equip(new[] { Machete() }, 0f, GameRules.Default);

            Assert.That(weapons.Drop(_macheteId, out _), Is.True);

            Assert.That(weapons.IsEquipped, Is.False);
            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.NoWeapon));
            Assert.That(log.ToString(), Does.Contain("[Net] drop weapon player=7 weapon=" + _macheteId + " left=0"));
        }

        [Test]
        public void DroppingAWeaponThatIsNotHeldDoesNothing()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(7, log, () => 0d);

            Assert.That(weapons.Drop(_macheteId, out _), Is.False, "nothing is equipped");

            weapons.Equip(new[] { TestRules.Rifle() }, 0f, GameRules.Default);

            Assert.That(weapons.Drop(_macheteId, out var dropped), Is.False);
            Assert.That(dropped, Is.Null);
            Assert.That(weapons.Drop(null, out _), Is.False);
            Assert.That(weapons.Specs.Select(s => s.Id), Is.EqualTo(new[] { TestRules.RifleId }));
        }

        private static WeaponSpec Machete(bool charge = true, bool @throw = true)
        {
            return new WeaponSpec(
                _macheteId,
                "weapon.machete",
                WeaponCategory.Melee,
                melee: new MeleeWeaponSpec(
                    _macheteSwing,
                    charge ? _macheteHeavy : null,
                    chargeDelaySeconds: 0.2f,
                    chargeSeconds: 0.4f,
                    @throw: @throw ? _macheteThrow : null));
        }

        private void StartWithMachete()
        {
            StartWithTwo();
            Equip(1, Machete(), TestRules.Rifle());
        }

        // ルールが持たせた武器の代わりに、近接武器を構えさせる。体が持っている武器を知るのは戦闘の tick なので、1 tick 進める
        private void Equip(int id, params WeaponSpec[] specs)
        {
            _combat.WeaponsOf(id).Equip(specs, 0f, MeleeRules());
            Tick();
        }
    }
}
