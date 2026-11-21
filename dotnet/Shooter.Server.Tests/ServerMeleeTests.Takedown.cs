using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 背後から気づいていない相手を倒す技 (ServerMelee.Takedown) を、オフラインの ContextMeleeAction と同じ条件で決めることを確かめる。
    // 1 は (-5, 0) で東を向き、2 は 1 の 1.5 m 東 (体の表面まで 1 m) に立つ。2 が東を向けば 1 はその真後ろにいる
    public sealed partial class ServerMeleeTests
    {
        // 西を向く yaw。2 が 1 の方を向く
        private const float _facingWest = -MathF.PI / 2f;

        // 位置合わせ 0.15 秒 + 技 1.5 秒 (MeleeSpec の既定値) を越えるまで
        private static readonly int _takedownTicks = (int)MathF.Ceiling((MeleeSpec.DefaultAlignSeconds + MeleeSpec.DefaultTakedownSeconds) / _tickSeconds) + 2;

        [Test]
        public void TheMeleeButtonBehindAnUnawareTargetStartsATakedownThatKillsAtTheEnd()
        {
            StartWithTwoUnaware();
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);

            Press(1);

            Assert.That(_melee.IsPerformingTakedown(1), Is.True, _log.ToString());
            Assert.That(_melee.TryGetTakedownTarget(1, out var target), Is.True);
            Assert.That(target, Is.EqualTo(2));
            Assert.That(_melee.IsTakedownTarget(2), Is.True);
            Assert.That(_melee.IsSwinging(1), Is.False, "the takedown replaces the ordinary swing");
            Assert.That(_log.ToString(), Does.Contain("[ContextMelee] started id=1 target=2 move=takedown"));
            Assert.That(_log.ToString(), Does.Not.Contain("[Melee] swing id=1"));

            Hold(1, InputButtons.None, _takedownTicks - 10);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "no damage before the move resolves");

            Hold(1, InputButtons.None, 10);

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            Assert.That(_melee.IsPerformingTakedown(1), Is.False);
            Assert.That(_melee.IsTakedownTarget(2), Is.False);
            Assert.That(_log.ToString(), Does.Contain(
                "[ContextMelee] resolved id=1 target=2 move=takedown outcome=Kill dmg=" + NetMatchSettings.DefaultMaxHealth + " killed=true"));
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].KillerId, Is.EqualTo(1));
            Assert.That(kills[0].VictimId, Is.EqualTo(2));
            Assert.That(kills[0].WeaponId, Is.EqualTo(TestRules.RifleId), "counted like the ordinary melee, with the held weapon");
        }

        [Test]
        public void TheTakedownButtonAlsoStartsTheTakedown()
        {
            StartWithTwoUnaware();
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);

            Hold(1, InputButtons.Takedown, 1);
            Hold(1, InputButtons.None, _takedownTicks);

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[ContextMelee] started id=1 target=2 move=takedown"));
        }

        [Test]
        public void ATargetFacingTheAttackerGetsTheOrdinarySwing()
        {
            StartWithTwoUnaware();
            PlaceFacing(2, -3.5f, 0f, _facingWest);
            Face(1);

            Swing(1);

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[ContextMelee] none id=1 target=2 conditions="));
            Assert.That(log, Does.Contain("TargetUnaware"));
            Assert.That(log, Does.Not.Contain("TargetBehind"));
            Assert.That(log, Does.Not.Contain("[ContextMelee] started"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), log);
        }

        // 気づいているかを答える口が無ければ、オフラインのプレイヤーと同じく常に気づいているとみなす
        [Test]
        public void WithoutAnAwarenessSourceTheTargetIsAwareAndTakesTheOrdinarySwing()
        {
            StartWithTwo();
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);

            Swing(1);

            var log = _log.ToString();
            Assert.That(log, Does.Contain("TargetBehind"));
            Assert.That(log, Does.Not.Contain("TargetUnaware"));
            Assert.That(log, Does.Not.Contain("[ContextMelee] started"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), log);
        }

        [Test]
        public void AnAwareTargetIsNotTakenDown()
        {
            StartWithTwo();
            _melee.IsTargetUnaware = _ => false;
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);

            Swing(1);

            Assert.That(_log.ToString(), Does.Not.Contain("[ContextMelee] started"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30));
        }

        // テイクダウンだけのボタンは、技が選ばれなくてもふつうの近接に替えない
        [Test]
        public void TheTakedownButtonWithoutAMoveDoesNotSwing()
        {
            StartWithTwoUnaware();
            PlaceFacing(2, -3.5f, 0f, _facingWest);
            Face(1);

            Hold(1, InputButtons.Takedown, 1);
            Hold(1, InputButtons.None, 60);

            Assert.That(_melee.IsSwinging(1), Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Melee] swing id=1"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        // 背後の扇は MeleeSpec.BehindArcDegrees (melee.json) で決める。真後ろから 60 度ずれた所は、90 度の扇の外で 150 度の扇の内
        [TestCase(90f, false)]
        [TestCase(150f, true)]
        public void TheBehindArcComesFromTheMeleeSpec(float behindArcDegrees, bool expectTakedown)
        {
            var spec = new MeleeSpec(
                damage: 30, reach: 2f, arcDegrees: 90f, swingSeconds: 0.5f, cooldownSeconds: 0.4f, impactSeconds: 0.2f, lungeDistance: 0f,
                behindArcDegrees: behindArcDegrees);
            Start(MeleeRules(), spec);
            Join(1);
            Join(2);
            Step(0.2f);
            _melee.IsTargetUnaware = id => id == 2;

            // 2 は 1 から東へ 1.5 m。2 が東から北へ 60 度回った向きなら、1 は 2 の真後ろから 60 度ずれる
            PlaceFacing(2, -3.5f, 0f, _facingEast - (MathF.PI / 3f));
            Face(1);

            Press(1);

            Assert.That(_melee.IsPerformingTakedown(1), Is.EqualTo(expectTakedown), _log.ToString());
        }

        [Test]
        public void ATeammateIsNotTakenDown()
        {
            Start(MeleeRules());
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            _melee.IsTargetUnaware = _ => true;
            Assert.That(_rules.Session.GetTeam(3), Is.EqualTo(_rules.Session.GetTeam(1)));
            PlaceFacing(3, -3.5f, 0f, _facingEast);
            Place(2, 5f, 0f);
            Face(1);

            Press(1);

            Assert.That(_melee.IsPerformingTakedown(1), Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[ContextMelee] started"));
        }

        [Test]
        public void TheAttackerCannotSwingDuringTheTakedown()
        {
            StartWithTwoUnaware();
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);
            Press(1);
            Hold(1, InputButtons.None, 5);

            Press(1);

            Assert.That(_log.ToString(), Does.Contain("[Melee] refused id=1 reason=Busy"));
            Assert.That(_melee.IsSwinging(1), Is.False);
            Assert.That(_melee.IsPerformingTakedown(1), Is.True);
        }

        [Test]
        public void BeingHurtInterruptsAnInterruptibleTakedown()
        {
            StartWithTwoUnaware();
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);
            Press(1);
            Hold(1, InputButtons.None, 10);

            _combat.DealDamage(null, 1, 10, TestRules.RifleId, HitZone.Body, null);
            Hold(1, InputButtons.None, _takedownTicks);

            Assert.That(_log.ToString(), Does.Contain("[ContextMelee] interrupted id=1 target=2 move=takedown reason=Shot"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_melee.IsPerformingTakedown(1), Is.False);
        }

        [Test]
        public void ANonInterruptibleTakedownGoesOnThroughDamage()
        {
            var spec = new MeleeSpec(
                damage: 30, reach: 2f, arcDegrees: 90f, swingSeconds: 0.5f, cooldownSeconds: 0.4f, impactSeconds: 0.2f, lungeDistance: 0f,
                takedownInterruptible: false);
            Start(MeleeRules(), spec);
            Join(1);
            Join(2);
            Step(0.2f);
            _melee.IsTargetUnaware = id => id == 2;
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);
            Press(1);
            Hold(1, InputButtons.None, 10);

            _combat.DealDamage(null, 1, 10, TestRules.RifleId, HitZone.Body, null);
            Hold(1, InputButtons.None, _takedownTicks);

            Assert.That(_log.ToString(), Does.Not.Contain("[ContextMelee] interrupted"));
            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
        }

        [Test]
        public void ATargetThatDiesFromSomethingElseEndsTheTakedown()
        {
            StartWithTwoUnaware();
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);
            Press(1);
            Hold(1, InputButtons.None, 10);

            _combat.DealDamage(null, 2, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Hold(1, InputButtons.None, 2);

            Assert.That(_log.ToString(), Does.Contain("[ContextMelee] interrupted id=1 target=2 move=takedown reason=TargetLost"));
            Assert.That(_melee.IsPerformingTakedown(1), Is.False);
        }

        // サーバーは相手を止めないので、とどめの瞬間に届かなくなっていれば倒さない
        [Test]
        public void ATargetThatMovedOutOfReachIsNotKilled()
        {
            StartWithTwoUnaware();
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);
            Press(1);
            Hold(1, InputButtons.None, 10);

            PlaceFacing(2, 0f, 0f, _facingEast);
            Hold(1, InputButtons.None, _takedownTicks);

            Assert.That(_log.ToString(), Does.Contain("[ContextMelee] interrupted id=1 target=2 move=takedown reason=TargetLost"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void TheAttackerLeavingCancelsTheTakedown()
        {
            StartWithTwoUnaware();
            PlaceFacing(2, -3.5f, 0f, _facingEast);
            Face(1);
            Press(1);

            _match.Leave(new ConnectionId(1));
            Hold(2, InputButtons.None, _takedownTicks);

            Assert.That(_melee.IsPerformingTakedown(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[ContextMelee] interrupted id=1 target=2 move=takedown reason=Cancelled"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void TheStartLogShowsTheTakedownTuning()
        {
            StartWithTwo();

            Assert.That(_log.ToString(), Does.Contain(" takedown=" + (MeleeSpec.DefaultAlignSeconds + MeleeSpec.DefaultTakedownSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " behindArc=" + MeleeSpec.DefaultBehindArcDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Test]
        public void UnawareBotsNeedsTheBots()
        {
            Assert.Throws<ArgumentNullException>(() => ServerMelee.UnawareBots(null));
        }

        private void StartWithTwoUnaware()
        {
            StartWithTwo();
            _melee.IsTargetUnaware = id => id == 2;
        }

        private void PlaceFacing(int id, float x, float z, float yaw)
        {
            _context.Bodies.Teleport(id, new Vector3(x, _alphaSpawn.Y, z), yaw, _host.TickCount);
            Tick();
        }

        // 振る者の向きは移動が tick で入力を読んでから体に載るので、押す前に東を向かせておく
        private void Face(int id)
        {
            Hold(id, InputButtons.None, 1);
        }
    }
}
