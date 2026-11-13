using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class BotDriverTests
    {
        private const float _tick = 1f / TickClock.DefaultTickRate;
        private const float _tolerance = 1e-3f;
        private const float _maxTurn = 240f;

        private static readonly Vector3 _position = Vector3.Zero;
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

        // 正面 (+Z) 10m、目の高さの相手。
        private static readonly Vector3 _ahead = new Vector3(0f, 1.6f, 10f);

        private static BotInputSpec Spec(
            float aimErrorPerMeter = 0f, float sprintDistance = BotInputSpec.DefaultSprintDistance, bool crouchWhenHolding = false)
        {
            return new BotInputSpec(
                _maxTurn,
                aimErrorPerMeter,
                BotInputSpec.DefaultAimErrorRefreshSeconds,
                BotInputSpec.DefaultFireToleranceDegrees,
                EnemyBrain.ArrivalDistance,
                sprintDistance,
                BotInputSpec.DefaultSprintMaxAngleDegrees,
                crouchWhenHolding);
        }

        private static BotDriver Driver(BotInputSpec spec, int seed = 1) => new BotDriver(spec, new Random(seed));

        private static BotInputPlan Plan(BotDriver driver, EnemyDecision decision, float yaw, float pitch = 0f, IReadOnlyList<Vector3> path = null)
        {
            return driver.Plan(_tick, decision, _position, _eye, yaw, pitch, path);
        }

        // 入力 (X 右・Y 前) を yaw (度) で回したワールドの水平の向き (X, Z)。PlayerMotor と同じ回し方。
        private static Vector2 ToWorld(Vector2 move, float yaw)
        {
            var radians = yaw * Math.PI / 180.0;
            var sin = (float)Math.Sin(radians);
            var cos = (float)Math.Cos(radians);
            return new Vector2((move.X * cos) + (move.Y * sin), (-move.X * sin) + (move.Y * cos));
        }

        private static float TurnDegrees(float fromYaw, float fromPitch, float toYaw, float toPitch)
        {
            var yaw = LookAngles.ToSigned(toYaw - fromYaw);
            var pitch = toPitch - fromPitch;
            return (float)Math.Sqrt((yaw * yaw) + (pitch * pitch));
        }

        // 目から見た相手と、射撃の線との距離 (m)。
        private static float MissDistance(BotInputPlan plan, Vector3 target)
        {
            return Vector3.Cross(plan.FireDirection, target - plan.FireOrigin).Length();
        }

        [Test]
        public void AimClosesOnATargetTenMetersAheadOverSeveralTicksWithoutExceedingTheTurnLimit()
        {
            var driver = Driver(BotInputSpec.Default, seed: 7);
            var decision = new EnemyDecision(null, _ahead, true);
            var maxStep = BotInputSpec.DefaultMaxTurnDegreesPerSecond * _tick;

            // 右 (+X) を向いたところから正面の相手へ振り向く
            var yaw = 90f;
            var pitch = 0f;
            var previousAngle = BotDriver.AngleDegrees(DeathCamFraming.Forward(yaw, pitch), _ahead - _eye);
            var errorCone = (float)(Math.Atan(BotInputSpec.DefaultAimErrorPerMeter) * 180.0 / Math.PI);
            var ticks = 0;
            while (previousAngle > errorCone + 0.5f)
            {
                var plan = Plan(driver, decision, yaw, pitch);
                Assert.That(TurnDegrees(yaw, pitch, plan.Yaw, plan.Pitch), Is.LessThanOrEqualTo(maxStep + _tolerance));

                var angle = BotDriver.AngleDegrees(plan.FireDirection, _ahead - _eye);
                Assert.That(angle, Is.LessThan(previousAngle), "tick " + ticks);
                if (ticks == 0)
                {
                    Assert.That(angle, Is.GreaterThan(80f), "must not snap onto the target in one tick");
                }

                yaw = plan.Yaw;
                pitch = plan.Pitch;
                previousAngle = angle;
                ticks++;
                Assert.That(ticks, Is.LessThan(60), "aim did not reach the target within a second");
            }

            // 90° を 1 tick 4° で回すので 20 tick 余り
            Assert.That(ticks, Is.GreaterThanOrEqualTo((int)(80f / maxStep)));
        }

        [Test]
        public void DoesNotFireWhileTurningAndFiresOnceAligned()
        {
            var driver = Driver(Spec());
            var decision = new EnemyDecision(null, _ahead, true);

            var yaw = 90f;
            var pitch = 0f;
            var plan = Plan(driver, decision, yaw, pitch);
            Assert.That(plan.Fire, Is.False);

            var fired = false;
            for (var i = 0; i < 60 && !fired; i++)
            {
                plan = Plan(driver, decision, yaw, pitch);
                yaw = plan.Yaw;
                pitch = plan.Pitch;
                fired = plan.Fire;
            }

            Assert.That(fired, Is.True);
            Assert.That(
                BotDriver.AngleDegrees(plan.FireDirection, _ahead - _eye),
                Is.LessThanOrEqualTo(BotInputSpec.DefaultFireToleranceDegrees + _tolerance));
            Assert.That(plan.FireOrigin, Is.EqualTo(_eye));
        }

        [Test]
        public void DoesNotFireWithoutTheDecisionEvenWhenAligned()
        {
            var driver = Driver(Spec());

            var plan = Plan(driver, new EnemyDecision(null, _ahead, false), 0f);

            Assert.That(plan.Fire, Is.False);
            Assert.That(BotDriver.AngleDegrees(plan.FireDirection, _ahead - _eye), Is.LessThan(_tolerance));
        }

        [Test]
        public void WithoutAimErrorTheAimSettlesExactlyOnTheTarget()
        {
            var driver = Driver(Spec());
            var target = new Vector3(3f, 4f, 10f);
            var decision = new EnemyDecision(null, target, true);

            var yaw = 0f;
            var pitch = 0f;
            BotInputPlan plan = default;
            for (var i = 0; i < 60; i++)
            {
                plan = Plan(driver, decision, yaw, pitch);
                yaw = plan.Yaw;
                pitch = plan.Pitch;
            }

            Assert.That(MissDistance(plan, target), Is.LessThan(1e-3f));
            Assert.That(plan.Pitch, Is.LessThan(0f), "a target above the eye is aimed at by looking up");
        }

        [Test]
        public void AimErrorInMetersGrowsWithDistanceAndStaysInsideTheRadius()
        {
            const float errorPerMeter = 0.05f;
            var near = new Vector3(0f, 1.6f, 10f);
            var far = new Vector3(0f, 1.6f, 40f);

            var anyMiss = false;
            for (var seed = 0; seed < 20; seed++)
            {
                // 同じ乱数なら誤差は単位円の同じ点なので、外れる距離は相手までの距離に比例する
                var nearMiss = MissDistance(Settle(Driver(Spec(errorPerMeter), seed), near), near);
                var farMiss = MissDistance(Settle(Driver(Spec(errorPerMeter), seed), far), far);

                Assert.That(nearMiss, Is.LessThanOrEqualTo((errorPerMeter * 10f) + _tolerance));
                Assert.That(farMiss, Is.LessThanOrEqualTo((errorPerMeter * 40f) + _tolerance));
                Assert.That(farMiss, Is.EqualTo(nearMiss * 4f).Within(0.01f));
                anyMiss |= nearMiss > 0.05f;
            }

            Assert.That(anyMiss, Is.True, "aim error must actually move the aim off the target");
        }

        // 誤差を引き直さない間 (0.5 秒) に向き終える
        private static BotInputPlan Settle(BotDriver driver, Vector3 target)
        {
            var decision = new EnemyDecision(null, target, true);
            var yaw = 0f;
            var pitch = 0f;
            BotInputPlan plan = default;
            for (var i = 0; i < 20; i++)
            {
                plan = Plan(driver, decision, yaw, pitch);
                yaw = plan.Yaw;
                pitch = plan.Pitch;
            }

            return plan;
        }

        [Test]
        public void AimErrorIsKeptUntilTheRefreshIntervalAndThenRedrawn()
        {
            var driver = Driver(Spec(0.05f), seed: 3);
            var decision = new EnemyDecision(null, _ahead, false);
            var spec = driver.Spec;

            // 旋回を待たないよう、毎回同じ向きから 1 tick だけ回した結果の向きで比べる
            var first = driver.Plan(_tick, decision, _position, _eye, 0f, 0f, null);
            var kept = driver.Plan(spec.AimErrorRefreshSeconds * 0.5f, decision, _position, _eye, 0f, 0f, null);
            Assert.That(kept.Yaw, Is.EqualTo(first.Yaw).Within(1e-4f));
            Assert.That(kept.Pitch, Is.EqualTo(first.Pitch).Within(1e-4f));

            var redrawn = driver.Plan(spec.AimErrorRefreshSeconds, decision, _position, _eye, 0f, 0f, null);
            var resetDriver = Driver(Spec(0.05f), seed: 3);
            resetDriver.Plan(_tick, decision, _position, _eye, 0f, 0f, null);
            resetDriver.ResetAim();
            var afterReset = resetDriver.Plan(_tick, decision, _position, _eye, 0f, 0f, null);

            Assert.That(redrawn.Yaw, Is.Not.EqualTo(first.Yaw).Within(1e-4f));
            Assert.That(afterReset.Yaw, Is.EqualTo(redrawn.Yaw).Within(1e-4f), "ResetAim draws the next error like a refresh");
        }

        [Test]
        public void ZeroDeltaDoesNotTurn()
        {
            var driver = Driver(Spec());

            var plan = driver.Plan(0f, new EnemyDecision(null, _ahead, true), _position, _eye, 90f, 10f, null);

            Assert.That(plan.Yaw, Is.EqualTo(90f).Within(_tolerance));
            Assert.That(plan.Pitch, Is.EqualTo(10f).Within(_tolerance));
            Assert.That(plan.Fire, Is.False);
        }

        [Test]
        public void TurnsTheShortWayAcrossZero()
        {
            var driver = Driver(Spec());
            var left = new Vector3(-10f, 1.6f, 10f);

            var plan = Plan(driver, new EnemyDecision(null, left, false), 10f);

            Assert.That(plan.Yaw, Is.EqualTo(10f - (_maxTurn * _tick)).Within(_tolerance));
        }

        [Test]
        public void MovesTowardTheFirstCornerNotYetReachedAlongThePath()
        {
            var driver = Driver(Spec());
            var path = new[] { new Vector3(0.1f, 0f, 0f), new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 20f) };
            var decision = new EnemyDecision(path[2], null, false);

            var plan = Plan(driver, decision, 0f, path: path);

            Assert.That(plan.Move.Length(), Is.EqualTo(1f).Within(_tolerance));
            var world = ToWorld(plan.Move, plan.Yaw);
            Assert.That(world.X, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(world.Y, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void MoveIsRelativeToTheNewYawWhileLookingElsewhere()
        {
            var driver = Driver(Spec());
            var decision = new EnemyDecision(new Vector3(0f, 0f, 10f), new Vector3(10f, 1.6f, 0f), false);

            var plan = Plan(driver, decision, 90f);

            // +X を向いたまま +Z へ進むので左へ歩く
            Assert.That(plan.Yaw, Is.EqualTo(90f).Within(_tolerance));
            Assert.That(plan.Move.X, Is.EqualTo(-1f).Within(_tolerance));
            Assert.That(plan.Move.Y, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(plan.Sprint, Is.False, "strafing does not sprint");
        }

        [Test]
        public void WithoutAPathGoesStraightToTheDestination()
        {
            var driver = Driver(Spec());
            var decision = new EnemyDecision(new Vector3(-3f, 5f, 0f), null, false);

            var plan = Plan(driver, decision, 270f, path: Array.Empty<Vector3>());

            var world = ToWorld(plan.Move, plan.Yaw);
            Assert.That(world.X, Is.EqualTo(-1f).Within(_tolerance));
            Assert.That(world.Y, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void WithoutALookTargetTurnsTowardTheWayItMovesAndLevelsThePitch()
        {
            var driver = Driver(Spec());
            var decision = new EnemyDecision(new Vector3(10f, 0f, 0f), null, false);

            var plan = Plan(driver, decision, 80f, pitch: 2f);

            Assert.That(plan.Yaw, Is.GreaterThan(80f));
            Assert.That(plan.Pitch, Is.LessThan(2f));
            Assert.That(TurnDegrees(80f, 2f, plan.Yaw, plan.Pitch), Is.LessThanOrEqualTo((_maxTurn * _tick) + _tolerance));
        }

        [Test]
        public void StopsWhenTheDestinationIsWithinArrivalDistance()
        {
            var driver = Driver(Spec());
            var decision = new EnemyDecision(new Vector3(0.3f, 2f, 0.2f), null, false);

            var plan = Plan(driver, decision, 45f);

            Assert.That(plan.Move, Is.EqualTo(Vector2.Zero));
            Assert.That(plan.Sprint, Is.False);
            Assert.That(plan.Yaw, Is.EqualTo(45f).Within(_tolerance));
        }

        [Test]
        public void StaysWithoutAMoveTarget()
        {
            var driver = Driver(Spec());

            var plan = Plan(driver, EnemyDecision.None, 30f, pitch: -5f);

            Assert.That(plan.Move, Is.EqualTo(Vector2.Zero));
            Assert.That(plan.Yaw, Is.EqualTo(30f).Within(_tolerance));
            Assert.That(plan.Pitch, Is.EqualTo(-5f).Within(_tolerance));
            Assert.That(plan.Fire, Is.False);
            Assert.That(plan.Crouch, Is.False);
        }

        [Test]
        public void SprintsForwardOnlyWhenTheRemainingPathIsLongAndNotShooting()
        {
            var path = new[] { Vector3.Zero, new Vector3(0f, 0f, 3f), new Vector3(4f, 0f, 3f), new Vector3(4f, 0f, 6f) };

            // 3 + 4 + 3 = 10m
            var far = Plan(Driver(Spec(sprintDistance: 9f)), new EnemyDecision(path[3], null, false), 0f, path: path);
            var near = Plan(Driver(Spec(sprintDistance: 11f)), new EnemyDecision(path[3], null, false), 0f, path: path);
            var shooting = Plan(Driver(Spec(sprintDistance: 9f)), new EnemyDecision(path[3], path[3], true), 0f, path: path);

            Assert.That(far.Sprint, Is.True);
            Assert.That(near.Sprint, Is.False);
            Assert.That(shooting.Sprint, Is.False);
        }

        [Test]
        public void CrouchesOnlyWhenHoldingAndAimingIfEnabled()
        {
            var holding = new EnemyDecision(null, _ahead, true);
            var moving = new EnemyDecision(new Vector3(0f, 0f, 5f), _ahead, true);

            Assert.That(Plan(Driver(Spec(crouchWhenHolding: true)), holding, 0f).Crouch, Is.True);
            Assert.That(Plan(Driver(Spec(crouchWhenHolding: true)), moving, 0f).Crouch, Is.False);
            Assert.That(Plan(Driver(Spec(crouchWhenHolding: false)), holding, 0f).Crouch, Is.False);
        }

        [Test]
        public void ToLocalIsTheInverseOfTheMotorRotation()
        {
            var world = Vector2.Normalize(new Vector2(0.3f, -0.8f));

            var local = BotDriver.ToLocal(world, 123f);
            var back = ToWorld(local, 123f);

            Assert.That(back.X, Is.EqualTo(world.X).Within(1e-5f));
            Assert.That(back.Y, Is.EqualTo(world.Y).Within(1e-5f));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var driver = Driver(Spec());
            var ok = new EnemyDecision(null, _ahead, false);

            Assert.That(() => driver.Plan(-1f, ok, _position, _eye, 0f, 0f, null), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => driver.Plan(float.NaN, ok, _position, _eye, 0f, 0f, null), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => driver.Plan(_tick, ok, new Vector3(float.NaN, 0f, 0f), _eye, 0f, 0f, null), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => driver.Plan(_tick, ok, _position, new Vector3(0f, float.PositiveInfinity, 0f), 0f, 0f, null),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => driver.Plan(_tick, ok, _position, _eye, float.NaN, 0f, null), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => driver.Plan(_tick, ok, _position, _eye, 0f, float.NaN, null), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => driver.Plan(_tick, new EnemyDecision(new Vector3(float.NaN, 0f, 0f), null, false), _position, _eye, 0f, 0f, null),
                Throws.ArgumentException);
            Assert.That(
                () => driver.Plan(_tick, ok, _position, _eye, 0f, 0f, new[] { Vector3.Zero, new Vector3(0f, float.NaN, 0f) }),
                Throws.ArgumentException);
            Assert.That(() => new BotDriver(null, new Random(1)), Throws.ArgumentNullException);
            Assert.That(() => new BotDriver(Spec(), null), Throws.ArgumentNullException);
        }

        [Test]
        public void SpecRejectsOutOfRangeValues()
        {
            Assert.That(() => new BotInputSpec(0f, 0f, 1f, 3f, 0.5f, 8f, 30f, false), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new BotInputSpec(float.PositiveInfinity, 0f, 1f, 3f, 0.5f, 8f, 30f, false), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new BotInputSpec(240f, -0.1f, 1f, 3f, 0.5f, 8f, 30f, false), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new BotInputSpec(240f, 0f, 0f, 3f, 0.5f, 8f, 30f, false), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new BotInputSpec(240f, 0f, 1f, 0f, 0.5f, 8f, 30f, false), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new BotInputSpec(240f, 0f, 1f, 3f, 0f, 8f, 30f, false), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new BotInputSpec(240f, 0f, 1f, 3f, 0.5f, float.NaN, 30f, false), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new BotInputSpec(240f, 0f, 1f, 3f, 0.5f, 8f, 91f, false), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new BotInputSpec(240f, 0f, 1f, 3f, 0.5f, float.PositiveInfinity, 30f, false), Throws.Nothing, "infinity disables sprint");
        }
    }
}
