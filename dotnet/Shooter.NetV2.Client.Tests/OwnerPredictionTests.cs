using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class OwnerPredictionTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;

        // 床の上面は y=0。体の原点はカプセルの中心で、足元はそれより 1 m 下
        private static readonly Vector3 _high = new Vector3(0f, 20f, 0f);
        private const int _openTick = 5;
        private const int _lastTick = 400;
        private const int _lag = 6;

        [Test]
        public void ThePredictionAndTheServerGlideToTheSamePlacesWithTheSameInputs()
        {
            var rules = new MatchRulesSync { Parachute = Parachute.On };
            var server = NewSimulation();
            var client = new OwnerPrediction(NewSimulation());
            var state = new CharacterMotorState(_high, Vector3.Zero, false, false);
            client.Start(-1, state);

            var serverStates = new CharacterMotorState[_lastTick + 1];
            var opened = false;
            var landed = false;
            for (var tick = 0; tick <= _lastTick; tick++)
            {
                var frame = Frame(tick);
                var predicted = client.Predict(frame, SnapshotMotion.ToInput(frame, false, rules));
                state = server.Step(state, SnapshotMotion.ToInput(frame, false, rules));
                serverStates[tick] = state;
                opened |= state.IsParachuting;
                landed |= opened && !state.IsParachuting && state.IsGrounded;

                Assert.That(predicted, Is.EqualTo(state), "tick " + tick);

                // サーバーの答えは数 tick 遅れて届く
                if (tick >= _lag)
                {
                    Assert.That(client.Reconcile(tick - _lag, serverStates[tick - _lag]).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
                }
            }

            Assert.That(serverStates[_openTick - 1].IsParachuting, Is.False);
            Assert.That(serverStates[_openTick].IsParachuting, Is.True, "the open bit opened it on the server");
            Assert.That(opened && landed, Is.True, "it glided down and landed within the test");
            Assert.That(client.Current.Position, Is.EqualTo(state.Position));
        }

        [Test]
        public void ThePredictionMatchesTheGlideReadBackFromTheSnapshotRow()
        {
            var rules = new MatchRulesSync { Parachute = Parachute.On };
            var server = NewSimulation();
            var client = new OwnerPrediction(NewSimulation());
            var state = new CharacterMotorState(_high, Vector3.Zero, false, false);
            client.Start(-1, state);

            var serverStates = new CharacterMotorState[_lastTick + 1];
            for (var tick = 0; tick <= _lastTick; tick++)
            {
                var frame = Frame(tick);
                client.Predict(frame, SnapshotMotion.ToInput(frame, false, rules));
                state = server.Step(state, SnapshotMotion.ToInput(frame, false, rules));
                serverStates[tick] = state;
                if (tick < _lag)
                {
                    continue;
                }

                var authoritative = SnapshotMotion.ToState(Row(serverStates[tick - _lag]));
                Assert.That(authoritative.Parachute, Is.EqualTo(serverStates[tick - _lag].Parachute), "the side speed comes back from the velocity, tick " + tick);
                Assert.That(client.Reconcile(tick - _lag, authoritative).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
            }

            Assert.That(client.Current.Position, Is.EqualTo(state.Position));
        }

        [Test]
        public void AServerDropIsCorrectedOnceAndThenPredicted()
        {
            var rules = new MatchRulesSync { Parachute = Parachute.On };
            var server = NewSimulation();
            var client = new OwnerPrediction(NewSimulation());
            var state = new CharacterMotorState(_high, Vector3.Zero, false, false);
            client.Start(-1, state);

            const int dropTick = 3;
            const int ticks = 120;
            var serverStates = new CharacterMotorState[ticks];
            for (var tick = 0; tick < ticks; tick++)
            {
                var frame = new InputFrame(tick, new Vector2(0f, 1f), 0f, InputButtons.None);
                client.Predict(frame, SnapshotMotion.ToInput(frame, false, rules));
                state = server.Step(state, SnapshotMotion.ToInput(frame, false, rules));

                // ServerMovement.Drop と同じく、処理した入力の後の状態でサーバーだけが開く
                if (tick == dropTick)
                {
                    state = server.Deploy(state, true);
                }

                serverStates[tick] = state;
            }

            Assert.That(serverStates[dropTick].IsParachuting, Is.True);
            Assert.That(client.Reconcile(dropTick, serverStates[dropTick]).Outcome, Is.EqualTo(ReconcileOutcome.Corrected), "the client could not know of the drop");
            Assert.That(client.Current, Is.EqualTo(serverStates[ticks - 1]), "the replay glides the same way as the server");
            for (var tick = dropTick + 1; tick < ticks; tick++)
            {
                Assert.That(client.Reconcile(tick, serverStates[tick]).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
            }
        }

        [Test]
        public void TheRowCarriesTheParachuteAndItsSideSpeed()
        {
            var open = new PlayerSnapshot { Position = _high, Velocity = new Vector3(1.5f, -4f, -2f), IsParachuting = true };
            var closed = new PlayerSnapshot { Position = _high, Velocity = new Vector3(1.5f, -4f, -2f) };

            Assert.That(SnapshotMotion.ToState(open).Parachute, Is.EqualTo(CharacterParachuteState.Open(new Vector2(1.5f, -2f))));
            Assert.That(SnapshotMotion.ToState(closed).IsParachuting, Is.False);
            Assert.That(
                SnapshotMotion.Complete(SnapshotMotion.ToState(open), default, NoWaterVolumes.Instance, NetMotorSettings.Default.Stance).IsParachuting,
                Is.True);
        }

        [Test]
        public void TheButtonsAndTheRulesBecomeTheInput()
        {
            var buttons = SnapshotMotion.Buttons(false, true, false, false, false, false, openParachute: true, closeParachute: true);
            var frame = new InputFrame(1, Vector2.Zero, 0f, buttons);

            var allowed = SnapshotMotion.ToInput(frame, false, new MatchRulesSync { Parachute = Parachute.On });
            var refused = SnapshotMotion.ToInput(frame, false, default);

            Assert.That(buttons, Is.EqualTo(InputButtons.Jump | InputButtons.OpenParachute | InputButtons.CloseParachute));
            Assert.That(allowed.OpenParachute, Is.True);
            Assert.That(allowed.CloseParachute, Is.True);
            Assert.That(allowed.ParachuteAllowed, Is.True);
            Assert.That(refused.ParachuteAllowed, Is.False, "nothing opens before the rules arrive");
            Assert.That(SnapshotMotion.ToInput(frame, true, new MatchRulesSync { Parachute = Parachute.On }).Restrained().ParachuteAllowed, Is.True,
                "being downed keeps an open parachute open");
        }

        [Test]
        public void TheSlideBitIsSentButNotPredicted()
        {
            var buttons = SnapshotMotion.Buttons(true, false, true, false, false, false, slide: true);
            var frame = new InputFrame(1, Vector2.Zero, 0f, buttons);

            var input = SnapshotMotion.ToInput(frame, false, new MatchRulesSync());

            Assert.That(buttons, Is.EqualTo(InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide));
            Assert.That(input.Slide, Is.True);
            Assert.That(input.SlideAllowed, Is.False, "the slide rule and state do not reach the client yet, so the prediction leaves it to the server");
            Assert.That(SnapshotMotion.Buttons(true, false, true, false, false, false), Is.EqualTo(InputButtons.Sprint | InputButtons.Crouch));
        }

        [Test]
        public void TheSimulationTakesTheParachuteTuning()
        {
            var defaults = NetMotorSettings.Default;
            var tuning = new ParachuteTuning(descentSpeed: 3f);
            var motor = new NetMotorSettings(defaults.Motor, defaults.Stance, defaults.Mover, defaults.Prone, defaults.Swim, defaults.WallRun, tuning);
            var world = new ClientWorld("Test", Floor(), NetMapLadders.None);

            Assert.That(world.CreateSimulation(motor, 60, -50f).Parachute, Is.SameAs(tuning));
            Assert.That(world.CreateSimulation(defaults, 60, -50f).Parachute, Is.SameAs(ParachuteTuning.Default));
        }

        // 開いた後は横へ揺らしながら進め、横の速さの寄せ方まで予測とサーバーで揃うかを見る
        private static InputFrame Frame(int tick)
        {
            var buttons = tick == _openTick ? InputButtons.Jump | InputButtons.OpenParachute : InputButtons.None;
            return new InputFrame(tick, new Vector2(MathF.Sin(tick * 0.15f), 1f), tick * 0.01f, buttons);
        }

        private static PlayerSnapshot Row(CharacterMotorState state)
        {
            return new PlayerSnapshot
            {
                Position = state.Position,
                Velocity = state.Velocity,
                IsGrounded = state.IsGrounded,
                IsCrouched = state.IsCrouched,
                IsProne = state.IsProne,
                Dodge = state.Dodge,
                Prone = state.Prone,
                Ladder = state.Ladder,
                Lean = state.Lean.Amount,
                Swimming = state.Swim.IsSwimming,
                AirJumpsUsed = (byte)state.AirJumpsUsed,
                JumpGraceLeft = state.JumpGraceLeft,
                IsParachuting = state.IsParachuting,
            };
        }

        private static WorldMap Floor()
        {
            return new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(80f, 0.5f, 80f), Quaternion.Identity),
            });
        }

        private static CharacterMotorSimulation NewSimulation()
        {
            var settings = NetMotorSettings.Default;
            var body = new CharacterBody(new BvhWorldCollision(Floor()), settings.Mover, settings.Stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(settings.Motor), _tickSeconds, -50f) { Parachute = settings.Parachute };
        }
    }
}
