using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed partial class ServerCombatTests
    {
        [Test]
        public void AnAcceptedShotAndItsHitAreSignalled()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var accepted = new List<(int Shooter, Vector3 Origin, Vector3 Direction, float Tick)>();
            var hits = new List<(int Shooter, int Victim, HitZone Zone, float RewindTicks)>();
            var rejected = new List<(int Shooter, ShotRejection Reason)>();
            _signals.ShotAccepted += (shooter, origin, direction, tick) => accepted.Add((shooter, origin, direction, tick));
            _signals.Hit += (shooter, victim, zone, rewind) => hits.Add((shooter, victim, zone, rewind));
            _signals.ShotRejected += (shooter, reason) => rejected.Add((shooter, reason));
            var eye = Body(1).EyePosition;
            var direction = Body(2).Center - eye;
            var tick = (int)_host.TickCount;

            Fire(1, eye, direction, tick);

            Assert.That(accepted, Is.EqualTo(new[] { (1, eye, direction, (float)tick) }));
            Assert.That(hits, Is.EqualTo(new[] { (1, 2, HitZone.Body, 0f) }), _log.ToString());
            Assert.That(rejected, Is.Empty);
        }

        [Test]
        public void AShotFromBehindAWallIsSignalledAsRejectedAndNotAccepted()
        {
            Start(Rules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);
            var accepted = 0;
            var hits = 0;
            var rejected = new List<(int Shooter, ShotRejection Reason)>();
            _signals.ShotAccepted += (_, _, _, _) => accepted++;
            _signals.Hit += (_, _, _, _) => hits++;
            _signals.ShotRejected += (shooter, reason) => rejected.Add((shooter, reason));

            _context.Bodies.Teleport(1, new Vector3(-1.5f, _alphaSpawn.Y, 0f), 0f, _host.TickCount);
            var origin = Body(1).EyePosition + new Vector3(2f, 0f, 0f);
            Fire(1, origin, Body(2).Center - origin, (int)_host.TickCount);

            Assert.That(rejected, Is.EqualTo(new[] { (1, ShotRejection.OriginBlocked) }));
            Assert.That(accepted, Is.Zero);
            Assert.That(hits, Is.Zero);
        }

        [Test]
        public void EveryRejectionIsSignalledEvenWhenTheWarningIsThrottled()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var accepted = 0;
            var rejected = new List<(int Shooter, ShotRejection Reason)>();
            _signals.ShotAccepted += (_, _, _, _) => accepted++;
            _signals.ShotRejected += (shooter, reason) => rejected.Add((shooter, reason));

            for (var i = 0; i < ShotBudget.DefaultBurst + 2; i++)
            {
                FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            }

            Assert.That(accepted, Is.EqualTo(ShotBudget.DefaultBurst));
            Assert.That(rejected, Is.EqualTo(new[] { (1, ShotRejection.FireRateExceeded), (1, ShotRejection.FireRateExceeded) }));
            Assert.That(
                _log.ToString().Split("reason=FireRateExceeded").Length - 1,
                Is.EqualTo(1),
                "the log line is still written once a second");
        }

        [Test]
        public void EachProcessedInputSignalsWhereThePlayerLooksWithoutAPitch()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var looked = new List<(int Player, int Tick, float Yaw, float? Pitch)>();
            _signals.Looked += (player, tick, yaw, pitch) => looked.Add((player, tick, yaw, pitch));

            var input = new InputMessage();
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, 1.25f, InputButtons.None));
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, 1.5f, InputButtons.None));
            _host.OnMessage(new ConnectionId(1UL), input);

            Assert.That(looked, Is.EqualTo(new[] { (1, _inputTick - 1, 1.25f, (float?)null), (1, _inputTick, 1.5f, (float?)null) }));
        }

        [Test]
        public void ThePitchOfAnAcceptedShotRidesOnlyTheNextProcessedInput()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var looked = new List<(int Tick, float? Pitch)>();
            _signals.Looked += (_, tick, _, pitch) => looked.Add((tick, pitch));

            // 長さ 1 でない向きでも、上へ 45 度の pitch になる
            Fire(1, Body(1).EyePosition, new Vector3(0f, 3f, 3f), (int)_host.TickCount);
            var input = new InputMessage();
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, 0f, InputButtons.None));
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, 0f, InputButtons.None));
            _host.OnMessage(new ConnectionId(1UL), input);

            Assert.That(looked, Has.Count.EqualTo(2));
            Assert.That(looked[0].Tick, Is.EqualTo(_inputTick - 1));
            Assert.That(looked[0].Pitch, Is.EqualTo(MathF.PI / 4f).Within(1e-6f));
            Assert.That(looked[1], Is.EqualTo((_inputTick, (float?)null)), "a later input does not repeat the old pitch");
        }

        [Test]
        public void ARejectedShotGivesNoPitch()
        {
            Start(Rules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);
            var looked = new List<float?>();
            _signals.Looked += (_, _, _, pitch) => looked.Add(pitch);

            _context.Bodies.Teleport(1, new Vector3(-1.5f, _alphaSpawn.Y, 0f), 0f, _host.TickCount);
            var origin = Body(1).EyePosition + new Vector3(2f, 0f, 0f);
            Fire(1, origin, new Vector3(0f, 1f, 1f), (int)_host.TickCount);
            var input = new InputMessage();
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, 0f, InputButtons.None));
            _host.OnMessage(new ConnectionId(1UL), input);

            Assert.That(looked, Is.EqualTo(new float?[] { null }));
        }

        [Test]
        public void RespawningForgetsThePitchOfAShotBeforeIt()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var looked = new List<float?>();
            _signals.Looked += (_, _, _, pitch) => looked.Add(pitch);

            Fire(1, Body(1).EyePosition, new Vector3(0f, -1f, 1f), (int)_host.TickCount);
            _context.Bodies.Teleport(1, _alphaSpawn, 0f, _host.TickCount);
            var input = new InputMessage();
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, 0f, InputButtons.None));
            _host.OnMessage(new ConnectionId(1UL), input);

            Assert.That(looked, Is.EqualTo(new float?[] { null }));
        }

        [Test]
        public void PitchIsRadiansUpPositiveAndZeroWhenLevel()
        {
            Assert.That(ServerCheatSignals.PitchOf(new Vector3(1f, 0f, 1f)), Is.Zero);
            Assert.That(ServerCheatSignals.PitchOf(new Vector3(0f, -2f, 0f)), Is.EqualTo(-MathF.PI / 2f).Within(1e-6f));
            Assert.That(ServerCheatSignals.PitchOf(new Vector3(0f, 1f, -1f)), Is.EqualTo(MathF.PI / 4f).Within(1e-6f));
        }
    }
}
