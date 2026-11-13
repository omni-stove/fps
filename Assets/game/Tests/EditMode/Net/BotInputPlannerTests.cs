using System;
using Fps.Core;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Net.Tests
{
    /// <summary>
    /// <see cref="BotInputPlanner"/> が Bot の判断を、人と同じ <see cref="MotorInput"/> (yaw はラジアン) と
    /// ServerFire に渡す始点・向き (Unity の向きの決まりと同じ) に変えることを確かめる。
    /// </summary>
    public sealed class BotInputPlannerTests
    {
        private const float _tolerance = 1e-3f;

        private static readonly Vector3 _position = new Vector3(2f, 0f, -1f);
        private static readonly Vector3 _eye = new Vector3(2f, 1.6f, -1f);

        // 正面 (+Z) 10m、目の高さの相手
        private static readonly Vector3 _ahead = new Vector3(2f, 1.6f, 9f);

        private static BotInputPlanner Planner(float aimErrorPerMeter = 0f)
        {
            var spec = new BotInputSpec(
                BotInputSpec.DefaultMaxTurnDegreesPerSecond,
                aimErrorPerMeter,
                BotInputSpec.DefaultAimErrorRefreshSeconds,
                BotInputSpec.DefaultFireToleranceDegrees,
                EnemyBrain.ArrivalDistance,
                BotInputSpec.DefaultSprintDistance,
                BotInputSpec.DefaultSprintMaxAngleDegrees,
                false);
            return new BotInputPlanner(spec, new System.Random(5));
        }

        private static System.Numerics.Vector3 N(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);

        [Test]
        public void AimClosesOnATargetTenMetersAheadWithinTheTurnLimitAndThenFires()
        {
            var planner = new BotInputPlanner(BotInputSpec.Default, new System.Random(11));
            var decision = new EnemyDecision(null, N(_ahead), true);
            var maxStep = BotInputSpec.DefaultMaxTurnDegreesPerSecond * NetworkPlayerMotor.TickSeconds;

            // 右 (+X) を向いたところから
            var yaw = Mathf.PI / 2f;
            var pitch = 0f;
            var previousAngle = Vector3.Angle(Quaternion.Euler(pitch, yaw * Mathf.Rad2Deg, 0f) * Vector3.forward, _ahead - _eye);
            var fired = false;
            var ticks = 0;
            for (; ticks < NetworkPlayerMotor.TickRate && !fired; ticks++)
            {
                var input = planner.Plan(NetworkPlayerMotor.TickSeconds, decision, _position, _eye, yaw, pitch, null);

                var yawStep = Mathf.Abs(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, input.Motor.Yaw * Mathf.Rad2Deg));
                var pitchStep = Mathf.Abs(input.Pitch - pitch);
                Assert.That(Mathf.Sqrt((yawStep * yawStep) + (pitchStep * pitchStep)), Is.LessThanOrEqualTo(maxStep + _tolerance));

                var angle = Vector3.Angle(input.FireDirection, _ahead - _eye);
                if (angle > 3f)
                {
                    Assert.That(angle, Is.LessThan(previousAngle), "tick " + ticks);
                }

                if (ticks == 0)
                {
                    Assert.That(angle, Is.GreaterThan(80f), "must not snap onto the target in one tick");
                    Assert.That(input.Fire, Is.False, "must not fire while turning");
                }

                yaw = input.Motor.Yaw;
                pitch = input.Pitch;
                previousAngle = angle;
                fired = input.Fire;
            }

            Assert.That(fired, Is.True, "aim reached the target and fired within a second");
            Assert.That(ticks, Is.GreaterThan(10), "a 90 degree turn takes several ticks");
        }

        [Test]
        public void FireDirectionMatchesUnityRotationOfYawAndPitchAndStartsAtTheEye()
        {
            var planner = Planner();
            var target = new Vector3(5f, 3f, 4f);

            var input = planner.Plan(1f, new EnemyDecision(null, N(target), true), _position, _eye, 0f, 0f, null);

            var expected = Quaternion.Euler(input.Pitch, input.Motor.Yaw * Mathf.Rad2Deg, 0f) * Vector3.forward;
            Assert.That(Vector3.Distance(input.FireDirection, expected), Is.LessThan(_tolerance));
            Assert.That(Vector3.Angle(input.FireDirection, target - _eye), Is.LessThan(0.01f));
            Assert.That(input.FireOrigin, Is.EqualTo(_eye));
            Assert.That(input.Fire, Is.True);
        }

        [Test]
        public void MoveFollowsNavMeshCornersAndRotatesBackToTheWorldDirectionWithTheMotorYaw()
        {
            var planner = Planner();
            var corners = new[] { _position, new Vector3(2f, 0f, 4f), new Vector3(-6f, 0f, 4f) };

            var input = planner.Plan(
                NetworkPlayerMotor.TickSeconds, new EnemyDecision(N(corners[2]), null, false), _position, _eye, 0f, 0f, corners);

            var move = input.Motor.Move;
            var world = NetworkPlayerMotor.YawRotation(input.Motor.Yaw) * new Vector3(move.x, 0f, move.y);
            Assert.That(world.x, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(world.z, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(input.Motor.Jump, Is.False);
            Assert.That(input.Motor.Downed, Is.False);
            Assert.That(input.Motor.Sprint, Is.True, "13m left to walk, facing forward and not shooting");
        }

        [Test]
        public void YawIsTakenAndReturnedInRadians()
        {
            var planner = Planner();

            // +X を向いたまま止まっていれば向きは変わらない
            var input = planner.Plan(NetworkPlayerMotor.TickSeconds, EnemyDecision.None, _position, _eye, Mathf.PI / 2f, 0f, null);

            Assert.That(input.Motor.Yaw, Is.EqualTo(Mathf.PI / 2f).Within(1e-4f));
            Assert.That(input.Motor.Move, Is.EqualTo(Vector2.zero));
            Assert.That(input.Fire, Is.False);
        }

        [Test]
        public void RejectsNonFiniteCorners()
        {
            var planner = Planner();
            var corners = new[] { _position, new Vector3(float.NaN, 0f, 0f) };

            Assert.That(
                () => planner.Plan(
                    NetworkPlayerMotor.TickSeconds, new EnemyDecision(N(_ahead), null, false), _position, _eye, 0f, 0f, corners),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
