using System;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerGrenadeFlightTests
    {
        private const float _dt = 1f / 60f;

        [Test]
        public void ItFallsUnderGravity()
        {
            var flight = new ServerGrenadeFlight(Empty(), new Vector3(0f, 10f, 0f), Vector3.Zero);

            for (var i = 0; i < 60; i++)
            {
                flight.Step(_dt);
            }

            Assert.That(flight.Velocity.Y, Is.EqualTo(ServerGrenadeFlight.Gravity).Within(1e-3f));
            Assert.That(flight.Position.Y, Is.EqualTo(10f + (0.5f * ServerGrenadeFlight.Gravity)).Within(0.1f));
        }

        [Test]
        public void ItBouncesOffTheFloorAndComesToRestOnIt()
        {
            var flight = new ServerGrenadeFlight(Floor(), new Vector3(0f, 3f, 0f), Vector3.Zero);
            var lowest = float.PositiveInfinity;

            for (var i = 0; i < 300; i++)
            {
                flight.Step(_dt);
                lowest = Math.Min(lowest, flight.Position.Y);
            }

            Assert.That(flight.Bounces, Is.GreaterThanOrEqualTo(1));
            Assert.That(lowest, Is.GreaterThan(ServerGrenadeFlight.BodyRadius - 0.01f));
            Assert.That(flight.Position.Y, Is.EqualTo(ServerGrenadeFlight.BodyRadius).Within(0.01f));
            Assert.That(Math.Abs(flight.Velocity.Y), Is.LessThan(0.5f));
        }

        [Test]
        public void ARollingGrenadeSpinsAndKeepsMostOfItsSpeed()
        {
            var flight = new ServerGrenadeFlight(Floor(), new Vector3(0f, ServerGrenadeFlight.BodyRadius + 0.001f, 0f), new Vector3(7f, 0f, 0f));

            for (var i = 0; i < 60; i++)
            {
                flight.Step(_dt);
            }

            // 滑りが転がりに変わると、中身の詰まった球は 5/7 の速さで転がる
            Assert.That(flight.Velocity.X, Is.EqualTo(5f).Within(0.3f));
            Assert.That(flight.AngularVelocity.Length(), Is.GreaterThan(1f));
            Assert.That(flight.Rotation, Is.Not.EqualTo(Quaternion.Identity));
        }

        [Test]
        public void ItDoesNotPassThroughAWall()
        {
            var wall = WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(3f, 0f, 0f), new Vector3(0.1f, 5f, 5f), Quaternion.Identity);
            var flight = new ServerGrenadeFlight(new BvhWorldCollision(new WorldMap(new[] { wall })), new Vector3(0f, 1f, 0f), new Vector3(30f, 0f, 0f));

            for (var i = 0; i < 30; i++)
            {
                flight.Step(_dt);
                Assert.That(flight.Position.X, Is.LessThan(2.9f));
            }

            Assert.That(flight.Velocity.X, Is.LessThan(0f));
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
