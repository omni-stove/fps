using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
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
        public void InputReadsTheJumpRuleAndKeepsTheButton()
        {
            var frame = new InputFrame(3, Vector2.Zero, 0f, SnapshotMotion.Buttons(false, true, false, false, false, false));

            var allowed = SnapshotMotion.ToInput(frame, false, default);
            var forbidden = SnapshotMotion.ToInput(frame, false, new MatchRulesSync { Jump = Jump.Off });

            Assert.That(allowed.JumpAllowed, Is.True);
            Assert.That(forbidden.JumpAllowed, Is.False);
            Assert.That(forbidden.Jump, Is.True);
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

        [Test]
        public void ARowCarriesTheLeanTheSwimTheWallRunAndTheAirJumpsIntoTheState()
        {
            var row = new PlayerSnapshot
            {
                Position = new Vector3(1f, 2f, 3f),
                Lean = -0.5f,
                Swimming = true,
                WallRunSide = Shooter.Protocol.WallRunSide.Right,
                AirJumpsUsed = 1,
            };

            var state = SnapshotMotion.ToState(row);

            Assert.That(state.Lean.Amount, Is.EqualTo(-0.5f));
            Assert.That(state.Swim.IsSwimming, Is.True);
            Assert.That(state.WallRun.Side, Is.EqualTo(Shooter.Core.WallRunSide.Right));
            Assert.That(state.WallRun.Normal, Is.EqualTo(Vector3.Zero), "the snapshot has no wall direction");
            Assert.That(state.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(SnapshotMotion.ToState(new PlayerSnapshot { JumpGraceLeft = 0.05f }).JumpGraceLeft, Is.EqualTo(0.05f));
            Assert.That(SnapshotMotion.ToState(new PlayerSnapshot()).WallRun, Is.EqualTo(CharacterWallRunState.None));
            Assert.That(SnapshotMotion.ToState(new PlayerSnapshot()).Swim, Is.EqualTo(CharacterSwimState.None));
        }

        [TestCase(false, false, 0)]
        [TestCase(true, false, -1)]
        [TestCase(false, true, 1)]
        [TestCase(true, true, 0)]
        public void InputReadsTheLeanButtonsTheWayTheServerDoes(bool left, bool right, int expected)
        {
            var buttons = SnapshotMotion.Buttons(false, false, false, false, false, false, false, left, right);
            var frame = new InputFrame(3, Vector2.Zero, 0f, buttons);

            Assert.That(frame.Has(InputButtons.LeanLeft), Is.EqualTo(left));
            Assert.That(frame.Has(InputButtons.LeanRight), Is.EqualTo(right));
            Assert.That(SnapshotMotion.ToInput(frame, false, default).Lean, Is.EqualTo(expected));
            Assert.That(SnapshotMotion.ToInput(frame, false, default).Lean, Is.EqualTo(LeanInput.Combine(left, right)));
        }

        [Test]
        public void TheMeleeButtonIsCarriedOnTheInputAndDoesNotMoveTheBody()
        {
            var melee = SnapshotMotion.Buttons(false, false, false, false, false, false, melee: true);
            var none = SnapshotMotion.Buttons(false, false, false, false, false, false);
            var frame = new InputFrame(3, new Vector2(0f, 1f), 0.5f, melee);

            Assert.That(melee, Is.EqualTo(InputButtons.Melee));
            Assert.That(none & InputButtons.Melee, Is.EqualTo(InputButtons.None));
            Assert.That(frame.Has(InputButtons.Melee), Is.True);
            Assert.That(
                SnapshotMotion.ToInput(frame, false, default),
                Is.EqualTo(SnapshotMotion.ToInput(new InputFrame(3, new Vector2(0f, 1f), 0.5f, none), false, default)),
                "the motor input is the same with or without melee");
        }

        [Test]
        public void CompleteKeepsThePredictedDetailsWhereTheSnapshotAgrees()
        {
            var wall = new Vector3(1f, 0f, 0f);
            var predicted = Traversal(
                new Vector3(0f, 2f, 0f),
                false,
                new CharacterLeanState(0.503f),
                new CharacterSwimState(true, 4f, true, 2.5f, 1),
                new CharacterWallRunState(Shooter.Core.WallRunSide.Left, 0.4f, wall));
            var row = Row(predicted);
            row.Lean = 64f / 127f;
            var authoritative = SnapshotMotion.ToState(row);

            var completed = SnapshotMotion.Complete(authoritative, predicted, Pool(), NetMotorSettings.Default.Stance);

            Assert.That(completed.Lean.Amount, Is.EqualTo(0.503f), "the unrounded lean of the prediction");
            Assert.That(completed.Swim, Is.EqualTo(predicted.Swim));
            Assert.That(completed.WallRun, Is.EqualTo(predicted.WallRun));
            Assert.That(CharacterMotorState.PositionError(predicted, completed), Is.Zero);
        }

        [Test]
        public void CompleteFillsWhatThePredictionMissedFromTheWaterAndLeavesAnUnknownWallEmpty()
        {
            var stance = NetMotorSettings.Default.Stance;
            var dry = Traversal(new Vector3(0f, 1f, 0f), false, new CharacterLeanState(-0.3f), CharacterSwimState.None, CharacterWallRunState.None);
            var server = Traversal(
                new Vector3(0f, 1f, 0f),
                false,
                new CharacterLeanState(0.4f),
                new CharacterSwimState(true, 4f, true, 3f, 0),
                new CharacterWallRunState(Shooter.Core.WallRunSide.Right, 0.2f, new Vector3(-1f, 0f, 0f)));
            var authoritative = SnapshotMotion.ToState(Row(server));

            var completed = SnapshotMotion.Complete(authoritative, dry, Pool(), stance);

            Assert.That(completed.Lean.Amount, Is.EqualTo(authoritative.Lean.Amount), "the side differs, so the server's lean stays");
            Assert.That(completed.Swim.IsSwimming, Is.True);
            Assert.That(completed.Swim.SurfaceY, Is.EqualTo(4f));
            Assert.That(completed.Swim.IsUnderwater, Is.EqualTo(1f + stance.Standing.EyeY < 4f));
            Assert.That(completed.Swim.BreathUsedSeconds, Is.Zero);
            Assert.That(completed.WallRun.Side, Is.EqualTo(Shooter.Core.WallRunSide.Right));
            Assert.That(completed.WallRun.Normal, Is.EqualTo(Vector3.Zero), "the caller probes the wall");
            Assert.That(CharacterMotorState.PositionError(dry, completed), Is.EqualTo(CharacterMotorState.StanceMismatchError));
        }

        [Test]
        public void CompleteCarriesTheLastWallOnlyWhileInTheAir()
        {
            var wall = new Vector3(0f, 0f, 1f);
            var running = Traversal(
                new Vector3(0f, 3f, 0f), false, CharacterLeanState.None, CharacterSwimState.None,
                new CharacterWallRunState(Shooter.Core.WallRunSide.Left, 0.5f, wall));
            var falling = SnapshotMotion.ToState(Row(Traversal(
                new Vector3(0f, 3f, 0f), false, CharacterLeanState.None, CharacterSwimState.None, CharacterWallRunState.None)));
            var landed = SnapshotMotion.ToState(Row(Traversal(
                new Vector3(0f, 1f, 0f), true, CharacterLeanState.None, CharacterSwimState.None, CharacterWallRunState.None)));

            Assert.That(SnapshotMotion.Complete(falling, running, NetMapWater.None, NetMotorSettings.Default.Stance).WallRun.LastWall, Is.EqualTo(wall));
            Assert.That(SnapshotMotion.Complete(landed, running, NetMapWater.None, NetMotorSettings.Default.Stance).WallRun, Is.EqualTo(CharacterWallRunState.None));
        }

        [Test]
        public void ASwimmingPredictionMatchesTheServerAndKeepsItsBreathThroughAReplay()
        {
            var server = Prediction(Pool());
            server.Start(-1, Standing(_spawn));
            var client = Prediction(Pool());
            client.Start(-1, Standing(_spawn));
            var serverStates = new CharacterMotorState[12];
            for (var tick = 0; tick < serverStates.Length; tick++)
            {
                Step(client, tick, new Vector2(0f, 1f));
                serverStates[tick] = Step(server, tick, new Vector2(0f, 1f));
            }

            Assert.That(serverStates[5].Swim.IsSwimming, Is.True, "the pool is deep enough to swim at the spawn");
            Assert.That(client.Reconcile(5, SnapshotMotion.ToState(Row(serverStates[5]))).Outcome, Is.EqualTo(ReconcileOutcome.Matched));

            var row = Row(serverStates[6]);
            row.Position += new Vector3(1f, 0f, 0f);
            var result = client.Reconcile(6, SnapshotMotion.ToState(row));

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Corrected));
            Assert.That(client.Current.Swim.IsSwimming, Is.True);
            Assert.That(client.Current.Swim.SurfaceY, Is.EqualTo(4f));
            Assert.That(client.Current.Swim.BreathUsedSeconds, Is.EqualTo(serverStates[11].Swim.BreathUsedSeconds), "the breath is kept, not restarted");
            Assert.That(client.Current.Position.X, Is.EqualTo(serverStates[11].Position.X + 1f).Within(0.01f));
        }

        [Test]
        public void TheStartReadsTheSurfaceFromTheWater()
        {
            var prediction = Prediction(Pool());
            var row = Row(Standing(_spawn));
            row.Swimming = true;

            prediction.Start(-1, SnapshotMotion.ToState(row));

            Assert.That(prediction.Current.Swim.IsSwimming, Is.True);
            Assert.That(prediction.Current.Swim.SurfaceY, Is.EqualTo(4f));
        }

        [Test]
        public void ADoubleJumpIsPredictedLikeTheServer()
        {
            var settings = NetMotorSettings.Default;
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
            });
            var motor = new MotorSpec(
                settings.Motor.WalkSpeed,
                settings.Motor.SprintSpeed,
                settings.Motor.JumpHeight,
                settings.Motor.Gravity,
                settings.Motor.AirControl,
                settings.Motor.CrouchSpeed,
                airJumps: 1);
            OwnerPrediction Jumper() => new OwnerPrediction(new CharacterMotorSimulation(
                new CharacterBody(new BvhWorldCollision(world), settings.Mover, settings.Stance), new PlayerMotor(motor), _tickSeconds, -50f));
            var server = Jumper();
            var client = Jumper();
            server.Start(-1, Standing(_spawn));
            client.Start(-1, Standing(_spawn));

            var serverStates = new CharacterMotorState[60];
            var used = 0;
            for (var tick = 0; tick < serverStates.Length; tick++)
            {
                var jump = tick == 2 || tick == 20;
                var frame = new InputFrame(tick, Vector2.Zero, 0f, jump ? InputButtons.Jump : InputButtons.None);
                var input = SnapshotMotion.ToInput(frame, false, default);
                client.Predict(frame, input);
                serverStates[tick] = server.Predict(frame, input);
                used = Math.Max(used, serverStates[tick].AirJumpsUsed);
            }

            Assert.That(used, Is.EqualTo(1), "the second jump is made in the air");
            for (var tick = 0; tick < serverStates.Length; tick += 5)
            {
                Assert.That(
                    client.Reconcile(tick, SnapshotMotion.ToState(Row(serverStates[tick]))).Outcome,
                    Is.EqualTo(ReconcileOutcome.Matched),
                    "tick " + tick);
            }
        }

        [Test]
        public void AJumpInTheGraceStartedFromARowIsPredictedLikeTheServer()
        {
            var settings = NetMotorSettings.Default;
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
            });
            var motor = new MotorSpec(
                settings.Motor.WalkSpeed,
                settings.Motor.SprintSpeed,
                settings.Motor.JumpHeight,
                settings.Motor.Gravity,
                settings.Motor.AirControl,
                settings.Motor.CrouchSpeed,
                jumpGraceSeconds: 0.1f);
            OwnerPrediction Jumper() => new OwnerPrediction(new CharacterMotorSimulation(
                new CharacterBody(new BvhWorldCollision(world), settings.Mover, settings.Stance), new PlayerMotor(motor), _tickSeconds, -50f));
            var plain = new CharacterMotorState(_spawn + new Vector3(0f, 3f, 0f), Vector3.Zero, false, false);
            var leftTheEdge = new CharacterMotorState(
                plain.Position, plain.Velocity, false, plain.Stance, plain.Cover, plain.Dodge, plain.Vault, plain.Prone, plain.Ladder,
                plain.Knockback, plain.Lean, plain.Swim, plain.WallRun, 0, 0.05f);
            var server = Jumper();
            var client = Jumper();
            server.Start(-1, leftTheEdge);
            client.Start(-1, SnapshotMotion.ToState(Row(leftTheEdge)));

            var serverStates = new CharacterMotorState[10];
            for (var tick = 0; tick < serverStates.Length; tick++)
            {
                var frame = new InputFrame(tick, Vector2.Zero, 0f, tick == 0 ? InputButtons.Jump : InputButtons.None);
                var input = SnapshotMotion.ToInput(frame, false, default);
                client.Predict(frame, input);
                serverStates[tick] = server.Predict(frame, input);
            }

            var jumpSpeed = MathF.Sqrt(2f * motor.Gravity * motor.JumpHeight);
            Assert.That(serverStates[0].Velocity.Y, Is.EqualTo(jumpSpeed - (motor.Gravity * _tickSeconds)).Within(1e-4f), "the press in the grace is the ground jump");
            Assert.That(serverStates[0].JumpGraceLeft, Is.Zero);
            Assert.That(serverStates[0].AirJumpsUsed, Is.Zero);
            for (var tick = 0; tick < serverStates.Length; tick++)
            {
                Assert.That(
                    client.Reconcile(tick, SnapshotMotion.ToState(Row(serverStates[tick]))).Outcome,
                    Is.EqualTo(ReconcileOutcome.Matched),
                    "tick " + tick);
            }
        }

        [Test]
        public void ARemotePoseReadsTheLeanTheSwimAndTheWallRunAndTheTrackBlendsOnlyTheLean()
        {
            var row = new PlayerSnapshot
            {
                Position = Vector3.Zero,
                Lean = 1f,
                Swimming = true,
                WallRunSide = Shooter.Protocol.WallRunSide.Left,
            };
            var first = RemotePose.From(row);
            row.Position = new Vector3(2f, 0f, 0f);
            row.Lean = 0f;
            row.Swimming = false;
            row.WallRunSide = Shooter.Protocol.WallRunSide.None;
            var second = RemotePose.From(row);

            Assert.That(first.Lean, Is.EqualTo(1f));
            Assert.That(first.Swimming, Is.True);
            Assert.That(first.WallRun, Is.EqualTo(Shooter.Core.WallRunSide.Left));

            var track = new RemoteTrack();
            track.Record(10, first);
            track.Record(12, second);
            Assert.That(track.TrySample(11f, out var middle), Is.True);
            Assert.That(middle.Lean, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(middle.Swimming, Is.True, "swimming does not blend");
            Assert.That(middle.WallRun, Is.EqualTo(Shooter.Core.WallRunSide.Left));
            Assert.That(track.TrySample(12f, out var late), Is.True);
            Assert.That(late.Swimming, Is.False);
            Assert.That(late.WallRun, Is.EqualTo(Shooter.Core.WallRunSide.None));
        }

        [Test]
        public void ARemotePoseKeepsALeanOutOfRangeWithinOne()
        {
            Assert.That(new RemotePose(Vector3.Zero, 0f, PlayerStance.Standing, "", 3f, false, Shooter.Core.WallRunSide.None).Lean, Is.EqualTo(1f));
            Assert.That(new RemotePose(Vector3.Zero, 0f, PlayerStance.Standing, "", float.NaN, false, Shooter.Core.WallRunSide.None).Lean, Is.Zero);
            Assert.That(new RemotePose(Vector3.Zero, 0f, PlayerStance.Standing, "").WallRun, Is.EqualTo(Shooter.Core.WallRunSide.None));
        }

        [Test]
        public void TheWorldReadsTheMapsWaterAndGivesItToThePrediction()
        {
            var directory = Path.Combine(Path.GetTempPath(), "shooter-client-water-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                using (var stream = File.Create(Path.Combine(directory, "Pool" + ShooterMapFile.Extension)))
                {
                    ShooterMapFile.Write(stream, new WorldMap(Array.Empty<WorldCollider>()));
                }

                Assert.That(ClientWorld.TryLoad(directory, "Pool", out var dry, out var error), Is.True, error);
                Assert.That(dry.Water.Water, Is.Empty, "a map exported before water has none");

                File.WriteAllText(Path.Combine(directory, "Pool" + NetMapWater.Extension), NetMapWater.Write(Pool()));
                Assert.That(ClientWorld.TryLoad(directory, "Pool", out var world, out error), Is.True, error);
                Assert.That(world.Water.Water, Has.Count.EqualTo(1));
                Assert.That(world.LogLine, Does.EndWith(" water=1"));
                var simulation = world.CreateSimulation(NetMotorSettings.Default, 60, -50f);
                Assert.That(simulation.Water.SurfaceAt(new Vector3(0f, 1f, 0f)), Is.EqualTo(4f));

                File.WriteAllText(Path.Combine(directory, "Pool" + NetMapWater.Extension), "{ \"water\": [ { \"min\": [0, 0, 0] } ] }");
                Assert.That(ClientWorld.TryLoad(directory, "Pool", out var broken, out error), Is.False);
                Assert.That(broken, Is.Null);
                Assert.That(error, Does.StartWith("cannot read ").And.Contain(NetMapWater.Extension));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
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

        private static OwnerPrediction Prediction(IWaterVolumes water = null)
        {
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
            });
            var settings = NetMotorSettings.Default;
            var body = new CharacterBody(new BvhWorldCollision(world), settings.Mover, settings.Stance);
            var simulation = new CharacterMotorSimulation(body, new PlayerMotor(settings.Motor), _tickSeconds, -50f);
            if (water != null)
            {
                simulation.Water = water;
            }

            return new OwnerPrediction(simulation);
        }

        // サーバーの ServerMovement.Contribute と同じく、状態のうちスナップショットに載るものだけを行に写す
        private static PlayerSnapshot Row(CharacterMotorState state)
        {
            return new PlayerSnapshot
            {
                Position = state.Position,
                Velocity = state.Velocity,
                IsGrounded = state.IsGrounded,
                IsCrouched = state.IsCrouched,
                IsProne = state.IsProne,
                Cover = new CoverSync(state.Cover.Snapshot, state.Cover.TopY, state.Cover.Edges, state.Cover.AppliedPeek),
                Dodge = state.Dodge,
                Vault = VaultSync.None,
                Prone = state.Prone,
                Ladder = state.Ladder,
                Lean = state.Lean.Amount,
                Swimming = state.Swim.IsSwimming,
                WallRunSide = (Shooter.Protocol.WallRunSide)(byte)state.WallRun.Side,
                AirJumpsUsed = (byte)state.AirJumpsUsed,
                JumpGraceLeft = state.JumpGraceLeft,
            };
        }

        private static CharacterMotorState Traversal(
            Vector3 position, bool grounded, CharacterLeanState lean, CharacterSwimState swim, CharacterWallRunState wallRun)
        {
            return new CharacterMotorState(
                position,
                Vector3.Zero,
                grounded,
                PlayerStance.Standing,
                CharacterCoverState.Out,
                DodgeSnapshot.Ready,
                CharacterVaultState.None,
                CharacterProneState.None,
                CharacterLadderState.None,
                CharacterKnockbackState.None,
                lean,
                swim,
                wallRun,
                0);
        }

        private static NetMapWater Pool()
        {
            return new NetMapWater(new[] { new MapWater(new WaterBox(new Vector3(-20f, -1f, -20f), new Vector3(20f, 4f, 20f), 4f)) });
        }
    }
}
