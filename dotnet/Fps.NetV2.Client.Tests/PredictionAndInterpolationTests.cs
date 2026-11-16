using System;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    public sealed class PredictionAndInterpolationTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly Vector3 _spawn = new Vector3(0f, 1.25f, 0f);

        [Test]
        public void PredictionKeepsUnprocessedInputsAndSendsTheNewestSixteen()
        {
            var prediction = Prediction();
            prediction.Start(-1, Standing(_spawn));
            for (var tick = 0; tick < 20; tick++)
            {
                Step(prediction, tick, new Vector2(0f, 1f));
            }

            var message = new InputMessage();
            prediction.FillInput(message);
            Assert.That(message.Count, Is.EqualTo(InputMessage.MaxFrames));
            Assert.That(message[0].Tick, Is.EqualTo(4));
            Assert.That(message[message.Count - 1].Tick, Is.EqualTo(19));

            prediction.Reconcile(17, prediction.Current);
            prediction.FillInput(message);
            Assert.That(message.Count, Is.EqualTo(2));
            Assert.That(message[0].Tick, Is.EqualTo(18));
        }

        [Test]
        public void AMatchingAnswerKeepsThePredictionAndADifferentOneReplaysTheLaterInputs()
        {
            var server = Prediction();
            server.Start(-1, Standing(_spawn));
            var client = Prediction();
            client.Start(-1, Standing(_spawn));
            CharacterMotorState atFive = default;
            for (var tick = 0; tick < 10; tick++)
            {
                Step(client, tick, new Vector2(0f, 1f));
                var state = Step(server, tick, new Vector2(0f, 1f));
                if (tick == 5)
                {
                    atFive = state;
                }
            }

            Assert.That(client.Reconcile(5, atFive).Outcome, Is.EqualTo(ReconcileOutcome.Matched));

            var shoved = new CharacterMotorState(atFive.Position + new Vector3(1f, 0f, 0f), atFive.Velocity, atFive.IsGrounded, false);
            var before = client.Current.Position;
            var result = client.Reconcile(6, shoved);

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Corrected));
            Assert.That(result.ResimulatedTicks, Is.EqualTo(3));
            Assert.That(client.Current.Position.X, Is.EqualTo(before.X + 1f).Within(0.01f));
            Assert.That(client.Simulation.Body.Position, Is.EqualTo(client.Current.Position), "the body is put back at the newest state");
        }

        [Test]
        public void PredictingOutOfOrderIsRefused()
        {
            var prediction = Prediction();
            Assert.Throws<InvalidOperationException>(() => Step(prediction, 0, Vector2.Zero));
            prediction.Start(-1, Standing(_spawn));
            Assert.Throws<ArgumentException>(() => Step(prediction, 1, Vector2.Zero));
        }

        [Test]
        public void InputReadsTheRulesAndDownedTheWayTheServerDoes()
        {
            var frame = new InputFrame(3, new Vector2(0f, 1f), 1f, SnapshotMotion.Buttons(true, false, false, true, true, true));
            var rules = new MatchRulesSync { Cover = Cover.Off, Dodge = Dodge.On, Vault = Vault.On };

            var input = SnapshotMotion.ToInput(frame, true, rules);

            Assert.That(input.Sprint, Is.True);
            Assert.That(input.Cover, Is.False);
            Assert.That(input.Dodge, Is.True);
            Assert.That(input.Aim, Is.True);
            Assert.That(input.Downed, Is.True);
            Assert.That(input.VaultAllowed, Is.True);
        }

        [Test]
        public void InputCarriesTheMultiplierTheServerSentAndOneWithoutIt()
        {
            var frame = new InputFrame(3, new Vector2(0f, 1f), 0f, InputButtons.None);
            var rules = new MatchRulesSync { Cover = Cover.On, Dodge = Dodge.On };

            Assert.That(SnapshotMotion.ToInput(frame, false, rules, 0.75f).CarrySpeedMultiplier, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(SnapshotMotion.ToInput(frame, false, rules).CarrySpeedMultiplier, Is.EqualTo(1f));
            Assert.That(SnapshotMotion.ToInput(frame, true, rules, 0.75f).Restrained().CarrySpeedMultiplier, Is.EqualTo(0.75f).Within(1e-6f));
        }

        [Test]
        public void ARowBecomesTheSameMotorState()
        {
            var row = new PlayerSnapshot
            {
                Position = new Vector3(1f, 2f, 3f),
                Velocity = new Vector3(0f, -1f, 0f),
                IsGrounded = false,
                IsCrouched = true,
                Vault = new VaultSync(true, Vector3.One, new Vector3(2f, 2f, 2f), new Vector3(3f, 1f, 3f), 0.1f),
            };

            var state = SnapshotMotion.ToState(row);

            Assert.That(state.Position, Is.EqualTo(row.Position));
            Assert.That(state.IsCrouched, Is.True);
            Assert.That(state.IsVaulting, Is.True);
            Assert.That(state.Vault.Land, Is.EqualTo(new Vector3(3f, 1f, 3f)));
            Assert.That(state.Vault.Elapsed, Is.EqualTo(0.1f));
        }

        [TestCase(0f, 0f)]
        [TestCase(4f, 4f - 2f * MathF.PI)]
        [TestCase(-4f, -4f + 2f * MathF.PI)]
        public void YawIsWrappedIntoOneTurn(float yaw, float expected)
        {
            Assert.That(SnapshotMotion.WrapYaw(yaw), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void YawIsInterpolatedTheShortWayRound()
        {
            var halfway = SnapshotMotion.LerpYaw(3f, -3f, 0.5f);

            Assert.That(MathF.Abs(halfway), Is.EqualTo(MathF.PI).Within(1e-4f));
        }

        [Test]
        public void ATrackInterpolatesBetweenTicksAndHoldsAtItsEnds()
        {
            var track = new RemoteTrack();
            track.Record(10, new RemotePose(Vector3.Zero, 0f, PlayerStance.Standing, ""));
            track.Record(12, new RemotePose(new Vector3(2f, 0f, 0f), 1f, PlayerStance.Crouching, "rifle"));
            track.Record(11, new RemotePose(new Vector3(9f, 9f, 9f), 0f, PlayerStance.Standing, ""));

            Assert.That(track.TrySample(11f, out var middle), Is.True);
            Assert.That(middle.Position.X, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(middle.Yaw, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(middle.IsCrouched, Is.False);
            Assert.That(track.TrySample(5f, out var early), Is.True);
            Assert.That(early.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(track.TrySample(20f, out var late), Is.True);
            Assert.That(late.Position.X, Is.EqualTo(2f));

            track.Clear();
            Assert.That(track.TrySample(11f, out _), Is.False);
        }

        [Test]
        public void TheClockStartsTwoTicksBehindFollowsWithoutPassingTheNewestAndJumpsAfterALongGap()
        {
            var clock = new InterpolationClock(60);
            clock.Advance(0f, 100);
            Assert.That(clock.RenderTick, Is.EqualTo(98f));

            for (var tick = 101; tick <= 160; tick++)
            {
                clock.Advance(_tickSeconds, tick);
            }

            Assert.That(clock.RenderTick, Is.InRange(158f, 160f), "it stays behind the newest tick it has");

            clock.Advance(_tickSeconds, 400);
            Assert.That(clock.RenderTick, Is.EqualTo(398f));
        }

        [Test]
        public void HistoryKeepsAnOlderSnapshotAsABaselineWithoutMakingItTheLatest()
        {
            var history = new SnapshotHistory(4);
            Assert.That(history.Add(new SnapshotMessage { Tick = 8 }), Is.Not.Null);
            Assert.That(history.Add(new SnapshotMessage { Tick = 7 }), Is.Null);
            Assert.That(history.TryGet(7, out _), Is.True);
            Assert.That(history.Add(new SnapshotMessage { Tick = 4 }), Is.Null);
            Assert.That(history.TryGet(8, out _), Is.True, "a snapshot as old as the history is long must not replace a newer one");
            Assert.That(history.LatestTick, Is.EqualTo(8));
        }

        private static CharacterMotorState Step(OwnerPrediction prediction, int tick, Vector2 move)
        {
            var frame = new InputFrame(tick, move, 0f, InputButtons.None);
            return prediction.Predict(frame, SnapshotMotion.ToInput(frame, false, default));
        }

        private static CharacterMotorState Standing(Vector3 position)
        {
            return new CharacterMotorState(position, Vector3.Zero, true, false);
        }

        private static OwnerPrediction Prediction()
        {
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
            });
            var settings = NetMotorSettings.Default;
            var body = new CharacterBody(new BvhWorldCollision(world), settings.Mover, settings.Stance);
            return new OwnerPrediction(new CharacterMotorSimulation(body, new PlayerMotor(settings.Motor), _tickSeconds, -50f));
        }
    }
}
