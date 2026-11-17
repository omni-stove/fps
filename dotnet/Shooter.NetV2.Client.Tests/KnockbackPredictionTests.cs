using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class KnockbackPredictionTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly Vector3 _spawn = new Vector3(0f, 1.25f, 0f);
        private static readonly KnockbackSpec _knockback = new KnockbackSpec(gravity: 12f, airControl: 2f);

        [Test]
        public void TheOwnersRowCarriesTheFlightFromTheOwnerSnapshot()
        {
            var row = new PlayerSnapshot { Position = new Vector3(1f, 2f, 3f), Velocity = new Vector3(4f, 3f, 0f) };
            var flying = CharacterKnockbackState.Flying(new Vector3(4f, 3f, 0f), 0.25f);

            Assert.That(SnapshotMotion.ToState(row, flying).Knockback, Is.EqualTo(flying));
            Assert.That(SnapshotMotion.ToState(row).IsKnockedBack, Is.False, "other players' rows have no flight");
        }

        [Test]
        public void AfterTheLaunchIsReconciledThePredictionFollowsTheServerUntilLanding()
        {
            var server = NewSimulation();
            var client = new OwnerPrediction(NewSimulation());
            var state = new CharacterMotorState(_spawn, Vector3.Zero, false, false);
            for (var i = 0; i < 30; i++)
            {
                state = server.Step(state, Input(i, Vector2.Zero));
            }

            client.Start(29, state);
            var serverStates = new CharacterMotorState[140];
            for (var tick = 30; tick < 140; tick++)
            {
                client.Predict(Frame(tick, Move(tick)), Input(tick, Move(tick)));
                state = server.Step(state, Input(tick, Move(tick)));

                // サーバーは tick 40 の入力を処理した後に爆発で飛ばす
                if (tick == 40)
                {
                    state = CharacterMotorSimulation.Launch(state, new Vector3(5f, 6f, 0f));
                }

                serverStates[tick] = state;
            }

            Assert.That(client.Reconcile(40, serverStates[40]).Outcome, Is.EqualTo(ReconcileOutcome.Corrected), "the client could not know of the blast");
            Assert.That(client.Current, Is.EqualTo(serverStates[139]), "the replay flies the same way as the server");

            var flew = false;
            for (var tick = 41; tick < 140; tick++)
            {
                flew |= serverStates[tick].IsKnockedBack;
                Assert.That(client.Reconcile(tick, serverStates[tick]).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
            }

            Assert.That(flew, Is.True);
            Assert.That(serverStates[139].IsKnockedBack, Is.False, "it has landed by the end");
        }

        [Test]
        public void TheSimulationTakesTheServersKnockbackSettings()
        {
            var world = new ClientWorld(
                "Test",
                new WorldMap(new[]
                {
                    WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                }),
                NetMapLadders.None);

            Assert.That(world.CreateSimulation(NetMotorSettings.Default, 60, -50f, _knockback).Knockback, Is.SameAs(_knockback));
            Assert.That(world.CreateSimulation(NetMotorSettings.Default, 60, -50f).Knockback, Is.SameAs(KnockbackSpec.Default));
        }

        private static Vector2 Move(int tick)
        {
            return new Vector2(MathF.Sin(tick * 0.2f), 1f);
        }

        private static InputFrame Frame(int tick, Vector2 move)
        {
            return new InputFrame(tick, move, 0f, InputButtons.None);
        }

        private static CharacterMotorInput Input(int tick, Vector2 move)
        {
            return SnapshotMotion.ToInput(Frame(tick, move), false, default);
        }

        private static CharacterMotorSimulation NewSimulation()
        {
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
            });
            var settings = NetMotorSettings.Default;
            var body = new CharacterBody(new BvhWorldCollision(world), settings.Mover, settings.Stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(settings.Motor), _tickSeconds, -50f) { Knockback = _knockback };
        }
    }
}
