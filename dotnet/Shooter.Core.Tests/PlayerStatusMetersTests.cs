using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class PlayerStatusMetersTests
    {
        private const float _tick = 1f / 60f;

        private static readonly StaminaSpec _smallStamina = new StaminaSpec(
            max: 10f, sprintCostPerSecond: 20f, slideCost: 5f, dodgeCost: 8f, regenPerSecond: 10f, regenDelaySeconds: 0.5f, recoverAmount: 5f);

        private static readonly CharacterMotorState _standing = new CharacterMotorState(Vector3.Zero, Vector3.Zero, true, false);

        [Test]
        public void EverythingOffLeavesTheInputAloneAndSendsNoStamina()
        {
            var meters = Meters(ActionRules.Default);
            var input = Input(sprint: true, jump: true, dodge: true);

            Assert.That(meters.StaminaRule, Is.EqualTo(Stamina.Off));
            Assert.That(meters.Restrain(input).Sprint, Is.True);
            Assert.That(meters.StaminaStatus, Is.Null);
            Assert.That(meters.Damaged(null, DamageKind.Melee, 10), Is.Null);
            Assert.That(meters.AddNearMiss(0f), Is.False);
            Assert.That(meters.StaggerKind, Is.EqualTo(StaggerKind.None));
            Assert.That(meters.SuppressionLevel, Is.Zero);
        }

        [Test]
        public void SprintingDrainsStaminaUntilSprintIsRefused()
        {
            var meters = Meters(On());
            var sprint = Input(sprint: true);

            // 10 を毎秒 20 で減らすので 0.5 秒 (30 tick) で尽きる
            for (var i = 0; i < 29; i++)
            {
                var restrained = meters.Restrain(sprint);
                Assert.That(restrained.Sprint, Is.True, "tick " + i);
                meters.Stepped(restrained, _standing, _standing, _tick);
            }

            meters.Stepped(meters.Restrain(sprint), _standing, _standing, _tick * 2f);

            Assert.That(meters.StaminaMeter.IsDepleted, Is.True);
            Assert.That(meters.StaminaStatus.Value.Depleted, Is.True);
            Assert.That(meters.Restrain(sprint).Sprint, Is.False);
            Assert.That(meters.CanSprint, Is.False);
        }

        [Test]
        public void StandingStillWithSprintHeldDoesNotDrain()
        {
            var meters = Meters(On());
            var held = Input(sprint: true, move: Vector2.Zero);

            meters.Stepped(meters.Restrain(held), _standing, _standing, 1f);

            Assert.That(meters.StaminaMeter.Current, Is.EqualTo(10f));
        }

        [Test]
        public void CrouchingWithSprintHeldDoesNotDrain()
        {
            var meters = Meters(On());
            var crouched = new CharacterMotorState(Vector3.Zero, Vector3.Zero, true, true);

            meters.Stepped(Input(sprint: true), crouched, crouched, 1f);

            Assert.That(meters.StaminaMeter.Current, Is.EqualTo(10f));
        }

        [Test]
        public void StartingADodgeSpendsItsCostAndAnUnaffordableDodgeIsDropped()
        {
            var meters = Meters(On());
            var dodging = new CharacterMotorState(
                Vector3.Zero, Vector3.Zero, true, false, CharacterCoverState.Out, new DodgeSnapshot(true, new Vector2(0f, 1f), 0f, 0f), CharacterVaultState.None);

            meters.Stepped(Input(dodge: true), _standing, dodging, 0f);

            Assert.That(meters.StaminaMeter.Current, Is.EqualTo(2f));
            Assert.That(meters.Restrain(Input(dodge: true)).Dodge, Is.False, "2 is less than the dodge cost of 8");

            meters.Stepped(Input(), dodging, dodging, 0f);
            Assert.That(meters.StaminaMeter.Current, Is.EqualTo(2f), "a dodge that keeps rolling is paid once");
        }

        [Test]
        public void StaminaComesBackAfterTheDelayAndLetsSprintAgainAtTheRecoverAmount()
        {
            var meters = Meters(On());
            meters.Stepped(Input(sprint: true), _standing, _standing, 1f);
            Assert.That(meters.CanSprint, Is.False);

            meters.Stepped(Input(), _standing, _standing, 0.5f + 0.4f);
            Assert.That(meters.StaminaMeter.Current, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(meters.CanSprint, Is.False, "4 is below the recover amount of 5");

            meters.Stepped(Input(), _standing, _standing, 0.2f);
            Assert.That(meters.CanSprint, Is.True);
        }

        [Test]
        public void AMeleeHitStaggersThePlayerWhoThenWalksSlowlyWithoutSprintOrJump()
        {
            var meters = Meters(On());

            var verdict = meters.Damaged(HitZone.Body, DamageKind.Melee, 30);

            Assert.That(verdict.HasValue, Is.True);
            Assert.That(verdict.Value.Outcome, Is.EqualTo(StaggerOutcome.Staggered));
            Assert.That(meters.StaggerKind, Is.EqualTo(StaggerKind.Stumble));
            var restrained = meters.Restrain(Input(sprint: true, jump: true, dodge: true));
            Assert.That(restrained.Move.Y, Is.EqualTo(StaggerSpec.DefaultPlayerMoveMultiplier).Within(1e-6f));
            Assert.That(restrained.Sprint, Is.False);
            Assert.That(restrained.Jump, Is.False);
            Assert.That(restrained.Dodge, Is.False);
        }

        [Test]
        public void TheStaggerEndsAfterItsSeconds()
        {
            var meters = Meters(On());
            meters.Damaged(null, DamageKind.Explosion, 40);
            Assert.That(meters.StaggerKind, Is.EqualTo(StaggerKind.Blast));

            Assert.That(meters.Tick(StaggerSpec.DefaultPlayerBlastSeconds - 0.01f), Is.EqualTo(StaggerKind.None));
            Assert.That(meters.Tick(0.02f), Is.EqualTo(StaggerKind.Blast));

            Assert.That(meters.StaggerKind, Is.EqualTo(StaggerKind.None));
            Assert.That(meters.Restrain(Input(sprint: true)).Sprint, Is.True);
        }

        [Test]
        public void ThePlayersDefaultTuningDoesNotStaggerOnBullets()
        {
            var meters = Meters(On());

            var verdict = meters.Damaged(HitZone.Head, DamageKind.Bullet, 50);

            Assert.That(verdict.Value.Outcome, Is.EqualTo(StaggerOutcome.NoReaction));
            Assert.That(meters.StaggerKind, Is.EqualTo(StaggerKind.None));
        }

        [Test]
        public void TunedBulletsStaggerOnTheHead()
        {
            var flinching = new StaggerSpec(0.5f, 0f, 0.4f, 0.6f, 20f, 8f, 1f, 1f, 1f, 1f, 0.5f, 0.5f);
            var meters = new PlayerStatusMeters(On(), _smallStamina, flinching, SuppressionSpec.Default);

            meters.Damaged(HitZone.Head, DamageKind.Bullet, 25);

            Assert.That(meters.StaggerKind, Is.EqualTo(StaggerKind.Flinch));
        }

        [Test]
        public void ShotsPassingCloseBuildSuppressionAndFarOnesDoNot()
        {
            var meters = Meters(On());
            var center = new Vector3(0f, 1f, 0f);

            // 中心の横 0.75m (半径 1.5 の半分) を通る弾は 0.2 の半分の 0.1 をためる
            Assert.That(meters.AddNearMiss(new Vector3(0.75f, 1f, -10f), Vector3.UnitZ, 20f, center, out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(meters.SuppressionLevel, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(meters.AddNearMiss(new Vector3(2f, 1f, -10f), Vector3.UnitZ, 20f, center, out _), Is.False);
            Assert.That(meters.AddNearMiss(new Vector3(0f, 1f, 5f), Vector3.UnitZ, 20f, center, out _), Is.False, "a shot fired away from the player does not count");

            for (var i = 0; i < 3; i++)
            {
                meters.AddNearMiss(new Vector3(0f, 1f, -10f), Vector3.UnitZ, 20f, center, out _);
            }

            Assert.That(meters.IsSuppressed, Is.True);
        }

        [Test]
        public void ResetRefillsStaminaAndClearsStaggerAndSuppression()
        {
            var meters = Meters(On());
            meters.Stepped(Input(sprint: true), _standing, _standing, 1f);
            meters.Damaged(null, DamageKind.Melee, 10);
            meters.AddNearMiss(0f);

            meters.Reset();

            Assert.That(meters.StaminaMeter.IsFull, Is.True);
            Assert.That(meters.CanSprint, Is.True);
            Assert.That(meters.StaggerKind, Is.EqualTo(StaggerKind.None));
            Assert.That(meters.SuppressionLevel, Is.Zero);
        }

        [Test]
        public void ConfiguringAgainStartsOver()
        {
            var meters = Meters(On());
            meters.Stepped(Input(sprint: true), _standing, _standing, 1f);

            meters.Configure(ActionRules.Default, _smallStamina, StaggerSpec.PlayerDefault, SuppressionSpec.Default);

            Assert.That(meters.StaminaRule, Is.EqualTo(Stamina.Off));
            Assert.That(meters.StaminaMeter.IsFull, Is.True);
        }

        private static ActionRules On()
        {
            return ActionRules.Default.WithStamina(Stamina.On).WithStagger(Stagger.On).WithSuppression(Suppression.On);
        }

        private static PlayerStatusMeters Meters(ActionRules rules)
        {
            return new PlayerStatusMeters(rules, _smallStamina, StaggerSpec.PlayerDefault, SuppressionSpec.Default);
        }

        private static CharacterMotorInput Input(bool sprint = false, bool jump = false, bool dodge = false, Vector2? move = null)
        {
            return new CharacterMotorInput(move ?? new Vector2(0f, 1f), 0f, sprint, jump, false, false, false, false, dodge, false);
        }
    }
}
