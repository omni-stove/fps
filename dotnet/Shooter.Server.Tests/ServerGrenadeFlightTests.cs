using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerGrenadeFlightTests
    {
        private const float _dt = 1f / 60f;
        private const int _pieceGroup = 7;
        private static readonly StanceGeometry _geometry = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        [Test]
        public void ItFallsUnderGravity()
        {
            var flight = new ServerGrenadeFlight(Empty(), new Vector3(0f, 10f, 0f), Vector3.Zero, GrenadeSpec.Default);

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
            var flight = new ServerGrenadeFlight(Floor(), new Vector3(0f, 3f, 0f), Vector3.Zero, GrenadeSpec.Default);
            var lowest = float.PositiveInfinity;

            for (var i = 0; i < 300; i++)
            {
                flight.Step(_dt);
                lowest = Math.Min(lowest, flight.Position.Y);
            }

            Assert.That(flight.Bounces, Is.GreaterThanOrEqualTo(1));
            Assert.That(lowest, Is.GreaterThan(GrenadeSpec.DefaultBodyRadius - 0.01f));
            Assert.That(flight.Position.Y, Is.EqualTo(GrenadeSpec.DefaultBodyRadius).Within(0.01f));
            Assert.That(Math.Abs(flight.Velocity.Y), Is.LessThan(0.5f));
        }

        [Test]
        public void ARollingGrenadeSpinsAndKeepsMostOfItsSpeed()
        {
            var flight = new ServerGrenadeFlight(Floor(), new Vector3(0f, GrenadeSpec.DefaultBodyRadius + 0.001f, 0f), new Vector3(7f, 0f, 0f), GrenadeSpec.Default);

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
            var flight = new ServerGrenadeFlight(new BvhWorldCollision(new WorldMap(new[] { wall })), new Vector3(0f, 1f, 0f), new Vector3(30f, 0f, 0f), GrenadeSpec.Default);

            for (var i = 0; i < 30; i++)
            {
                flight.Step(_dt);
                Assert.That(flight.Position.X, Is.LessThan(2.9f));
            }

            Assert.That(flight.Velocity.X, Is.LessThan(0f));
        }

        [Test]
        public void AStickyGrenadeStopsOnTheWallItHitsInsteadOfBouncing()
        {
            var flight = new ServerGrenadeFlight(Wall(out _), new Vector3(0f, 1f, 0f), new Vector3(30f, 0f, 0f), Sticky(GrenadeStickiness.Surfaces));

            for (var i = 0; i < 60; i++)
            {
                flight.Step(_dt);
            }

            Assert.That(flight.IsStuck, Is.True);
            Assert.That(flight.Bounces, Is.Zero);
            Assert.That(flight.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(flight.Position.X, Is.EqualTo(2.9f - GrenadeSpec.DefaultBodyRadius).Within(0.01f));
            Assert.That(flight.StuckToPlayerId, Is.Null);
            var stuckAt = flight.Position;
            Run(flight, 60);
            Assert.That(flight.Position, Is.EqualTo(stuckAt), "it hangs on the wall without falling");
        }

        [Test]
        public void AStuckGrenadeFallsWhenThePieceItIsOnBreaksAndSticksToTheFloor()
        {
            var world = new BvhWorldCollision(new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                WorldCollider.Box(2, 0, _pieceGroup, new Vector3(3f, 2f, 0f), new Vector3(0.1f, 2f, 5f), Quaternion.Identity),
            }));
            var flight = new ServerGrenadeFlight(world, new Vector3(0f, 1f, 0f), new Vector3(30f, 0f, 0f), Sticky(GrenadeStickiness.Surfaces));
            Run(flight, 30);
            Assert.That(flight.IsStuck, Is.True);

            world.SetGroupEnabled(_pieceGroup, false);
            Run(flight, 120);

            Assert.That(flight.IsStuck, Is.True);
            Assert.That(flight.Position.Y, Is.EqualTo(GrenadeSpec.DefaultBodyRadius).Within(0.01f));
        }

        [Test]
        public void AGrenadeThatSticksOnlyToSurfacesPassesThroughPeople()
        {
            var bodies = Bodies(out _);
            var flight = new ServerGrenadeFlight(Empty(), new Vector3(0f, 1f, 0f), new Vector3(20f, 0f, 0f), Sticky(GrenadeStickiness.Surfaces));
            flight.CollideWithCharacters(bodies, ignoredPlayerId: 1, _ => true);

            Run(flight, 30);

            Assert.That(flight.IsStuck, Is.False);
            Assert.That(flight.Position.X, Is.GreaterThan(5f));
        }

        [Test]
        public void ItSticksToAPersonAndMovesAndTurnsWithThem()
        {
            var bodies = Bodies(out var target);
            var flight = new ServerGrenadeFlight(
                Empty(), new Vector3(0f, 1f, 0f), new Vector3(20f, 0f, 0f), Sticky(GrenadeStickiness.SurfacesAndCharacters));
            flight.CollideWithCharacters(bodies, ignoredPlayerId: 1, _ => true);

            Run(flight, 30);

            Assert.That(flight.IsStuck, Is.True);
            Assert.That(flight.StuckToPlayerId, Is.EqualTo(2));
            target.Capsule(out var bottom, out var top);
            Assert.That(
                ServerCombat.DistanceToCapsule(flight.Position, bottom - new Vector3(0f, target.Radius, 0f), top + new Vector3(0f, target.Radius, 0f), target.Radius),
                Is.EqualTo(GrenadeSpec.DefaultBodyRadius).Within(0.01f),
                "it stops touching the body");
            var offset = flight.Position - target.Position;

            target.SetPose(target.Position + new Vector3(0f, 0f, 4f), 0f, PlayerStance.Standing);
            flight.Step(_dt);
            Assert.That(flight.Position - target.Position, Is.EqualTo(offset).Using<Vector3>(Near));

            target.SetPose(target.Position, MathF.PI, PlayerStance.Standing);
            flight.Step(_dt);
            var turned = flight.Position - target.Position;
            Assert.That(turned.X, Is.EqualTo(-offset.X).Within(1e-3f), "a half turn puts it on the other side");
            Assert.That(turned.Y, Is.EqualTo(offset.Y).Within(1e-3f));
        }

        [Test]
        public void ItDoesNotStickToTheThrowerOrToPeopleWhoCannotBeHit()
        {
            var bodies = Bodies(out _);
            var flight = new ServerGrenadeFlight(
                Empty(), new Vector3(0f, 1f, 0f), new Vector3(20f, 0f, 0f), Sticky(GrenadeStickiness.SurfacesAndCharacters));
            flight.CollideWithCharacters(bodies, ignoredPlayerId: 1, id => id != 2);

            Run(flight, 30);

            Assert.That(flight.IsStuck, Is.False);
            Assert.That(flight.StuckToPlayerId, Is.Null);
        }

        [Test]
        public void ItFallsOffAPersonWhoDies()
        {
            var bodies = Bodies(out _);
            var alive = true;
            var flight = new ServerGrenadeFlight(
                Floor(), new Vector3(0f, 1f, 0f), new Vector3(20f, 0f, 0f), Sticky(GrenadeStickiness.SurfacesAndCharacters));
            flight.CollideWithCharacters(bodies, ignoredPlayerId: 1, _ => alive);
            Run(flight, 30);
            Assert.That(flight.StuckToPlayerId, Is.EqualTo(2));

            alive = false;
            Run(flight, 120);

            Assert.That(flight.StuckToPlayerId, Is.Null);
            Assert.That(flight.IsStuck, Is.True, "it sticks to the floor it lands on");
            Assert.That(flight.Position.Y, Is.EqualTo(GrenadeSpec.DefaultBodyRadius).Within(0.01f));
        }

        [Test]
        public void ASweptPointMeetsTheSideAndTheCapsOfACapsule()
        {
            var bottom = new Vector3(0f, 0.5f, 0f);
            var top = new Vector3(0f, 1.5f, 0f);

            Assert.That(ServerGrenadeFlight.SweepCapsule(new Vector3(-5f, 1f, 0f), Vector3.UnitX, 10f, bottom, top, 0.5f, out var side), Is.True);
            Assert.That(side, Is.EqualTo(4.5f).Within(1e-4f));
            Assert.That(ServerGrenadeFlight.SweepCapsule(new Vector3(0f, 5f, 0f), -Vector3.UnitY, 10f, bottom, top, 0.5f, out var cap), Is.True);
            Assert.That(cap, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(ServerGrenadeFlight.SweepCapsule(new Vector3(-5f, 1f, 0f), Vector3.UnitX, 4f, bottom, top, 0.5f, out _), Is.False);
            Assert.That(ServerGrenadeFlight.SweepCapsule(new Vector3(-5f, 3f, 0f), Vector3.UnitX, 10f, bottom, top, 0.5f, out _), Is.False);
            Assert.That(ServerGrenadeFlight.SweepCapsule(new Vector3(0f, 1f, 0.1f), Vector3.UnitX, 10f, bottom, top, 0.5f, out var inside), Is.True);
            Assert.That(inside, Is.Zero);
        }

        private static GrenadeSpec Sticky(GrenadeStickiness stickiness)
        {
            return new GrenadeSpec(stickiness: stickiness);
        }

        private static void Run(ServerGrenadeFlight flight, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                flight.Step(_dt);
            }
        }

        private static bool Near(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b) < 1e-3f;
        }

        private static ServerBodies Bodies(out ServerBody target)
        {
            var bodies = new ServerBodies(_geometry);
            bodies.Add(1);
            bodies.Teleport(1, Vector3.Zero, 0f, 1);
            bodies.Add(2);
            bodies.Teleport(2, new Vector3(3f, 0f, 0f), 0f, 1);
            bodies.TryGet(2, out target);
            return bodies;
        }

        private static BvhWorldCollision Wall(out WorldCollider wall)
        {
            wall = WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(3f, 0f, 0f), new Vector3(0.1f, 5f, 5f), Quaternion.Identity);
            return new BvhWorldCollision(new WorldMap(new[] { wall }));
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
