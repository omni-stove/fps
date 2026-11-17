using System;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // 球の半径・跳ね返り・摩擦・回転の減りを種類の GrenadeSpec (grenade.json) から読み、その値で飛ぶことを確かめる
    public sealed class ServerGrenadeBodyTuningTests
    {
        private const float _dt = 1f / 60f;

        [Test]
        public void TheFlightTakesItsBallFromTheSpec()
        {
            var spec = new GrenadeSpec(bodyRadius: 0.2f, bounciness: 0.9f, friction: 0.1f, angularDamping: 3f);

            var flight = new ServerGrenadeFlight(Empty(), Vector3.Zero, Vector3.Zero, spec);

            Assert.That(flight.BodyRadius, Is.EqualTo(0.2f));
            Assert.That(flight.Bounciness, Is.EqualTo(0.9f));
            Assert.That(flight.Friction, Is.EqualTo(0.1f));
            Assert.That(flight.AngularDamping, Is.EqualTo(3f));
        }

        [Test]
        public void TheDefaultSpecKeepsTheBallItHadBefore()
        {
            var flight = new ServerGrenadeFlight(Empty(), Vector3.Zero, Vector3.Zero, GrenadeSpec.Default);

            Assert.That(flight.BodyRadius, Is.EqualTo(0.08f));
            Assert.That(flight.Bounciness, Is.EqualTo(0.35f));
            Assert.That(flight.Friction, Is.EqualTo(0.6f));
            Assert.That(flight.AngularDamping, Is.EqualTo(0.05f));
        }

        [TestCase(0.35f, -10.5f)]
        [TestCase(0.9f, -27f)]
        public void ABouncierGrenadeComesBackFasterFromAWall(float bounciness, float expectedSpeedX)
        {
            var flight = new ServerGrenadeFlight(Wall(), new Vector3(0f, 1f, 0f), new Vector3(30f, 0f, 0f), new GrenadeSpec(bounciness: bounciness));

            while (flight.Bounces == 0)
            {
                flight.Step(_dt);
            }

            Assert.That(flight.Velocity.X, Is.EqualTo(expectedSpeedX).Within(1e-3f));
        }

        [Test]
        public void ANonBouncyGrenadeStopsAtTheWall()
        {
            var flight = new ServerGrenadeFlight(Wall(), new Vector3(0f, 1f, 0f), new Vector3(30f, 0f, 0f), new GrenadeSpec(bounciness: 0f));

            Run(flight, 30);

            Assert.That(flight.Bounces, Is.Zero);
            Assert.That(flight.Velocity.X, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(flight.Position.X, Is.EqualTo(2.9f - GrenadeSpec.DefaultBodyRadius).Within(0.01f));
        }

        [Test]
        public void ALargerBallRestsHigherOnTheFloor()
        {
            var flight = new ServerGrenadeFlight(Floor(), new Vector3(0f, 2f, 0f), Vector3.Zero, new GrenadeSpec(bodyRadius: 0.25f));

            Run(flight, 300);

            Assert.That(flight.Position.Y, Is.EqualTo(0.25f).Within(0.01f));
        }

        [Test]
        public void TheSpinFadesByTheAngularDampingInTheAir()
        {
            // 床で斜めに跳ねて回りだし、宙にいる間は 1 歩ごとに 1 / (1 + dt × angularDamping) 倍になる
            var free = Bounced(new GrenadeSpec(angularDamping: 0f));
            var damped = Bounced(new GrenadeSpec(angularDamping: 6f));
            Assert.That(free.AngularVelocity.Length(), Is.GreaterThan(1f));

            Run(free, 10);
            Run(damped, 10);

            var expected = MathF.Pow(1f + (_dt * 6f), -11f);
            Assert.That(damped.AngularVelocity.Length() / free.AngularVelocity.Length(), Is.EqualTo(expected).Within(1e-3f));
        }

        [Test]
        public void LessFrictionLeavesMoreSlideAfterLanding()
        {
            var grippy = Bounced(new GrenadeSpec(friction: 0.6f));
            var slippery = Bounced(new GrenadeSpec(friction: 0.05f));

            Assert.That(slippery.Velocity.X, Is.GreaterThan(grippy.Velocity.X));
            Assert.That(slippery.AngularVelocity.Length(), Is.LessThan(grippy.AngularVelocity.Length()));
        }

        private static ServerGrenadeFlight Bounced(GrenadeSpec spec)
        {
            var flight = new ServerGrenadeFlight(Floor(), new Vector3(0f, spec.BodyRadius + 0.05f, 0f), new Vector3(8f, -6f, 0f), spec);
            flight.Step(_dt);
            Assert.That(flight.Bounces, Is.EqualTo(1));
            Assert.That(flight.Velocity.Y, Is.GreaterThan(0f));
            return flight;
        }

        private static void Run(ServerGrenadeFlight flight, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                flight.Step(_dt);
            }
        }

        private static BvhWorldCollision Wall()
        {
            return new BvhWorldCollision(new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(3f, 0f, 0f), new Vector3(0.1f, 5f, 5f), Quaternion.Identity),
            }));
        }

        private static BvhWorldCollision Empty()
        {
            return new BvhWorldCollision(new WorldMap(Array.Empty<WorldCollider>()));
        }

        private static BvhWorldCollision Floor()
        {
            return new BvhWorldCollision(new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
            }));
        }
    }
}
