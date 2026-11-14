using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CapsuleMoverTests
    {
        private const float _radius = 0.5f;
        private const float _standingHeight = 2f;
        private const float _crouchHeight = 1.2f;
        private const float _skin = CapsuleMoverSpec.DefaultSkinWidth;
        private const float _tick = 1f / 60f;
        private const float _walkStep = 5f * _tick;
        private const float _gravityStep = -0.05f;

        private const int _framesPerWaypoint = 80;

        // 段を上がって壁に当たり、別の壁に沿って滑り、坂を登り、低い天井の下で跳ぶ。
        private static readonly Vector2[] _waypoints =
        {
            new Vector2(5.5f, 0f), new Vector2(0f, -5.5f), new Vector2(-6f, 0f), new Vector2(0f, 3f),
        };

        private static readonly int _courseFrames = _framesPerWaypoint * _waypoints.Length;

        private static readonly Vector3 _standingCenter = new Vector3(0f, _standingHeight / 2f, 0f);
        private static readonly Vector3 _crouchCenter = new Vector3(0f, _crouchHeight / 2f, 0f);

        private FakeWorldCollision _world;

        [SetUp]
        public void SetUp()
        {
            _world = new FakeWorldCollision();
        }

        [Test]
        public void FallingLandsOnTheFloorAndKeepsTheSkinGap()
        {
            AddFloor();
            var mover = NewMover(new Vector3(0f, 1f, 0f));

            var flags = mover.Move(new Vector3(0f, -2f, 0f));

            Assert.That(flags, Is.EqualTo(CapsuleContacts.Below));
            Assert.That(mover.IsGrounded, Is.True);
            Assert.That(mover.Position.Y, Is.EqualTo(_skin).Within(1e-3f));

            mover.Move(new Vector3(0f, _gravityStep, 0f));

            Assert.That(mover.IsGrounded, Is.True);
            Assert.That(mover.Position.Y, Is.EqualTo(_skin).Within(1e-3f));
        }

        [Test]
        public void FallingWithNothingBelowIsNotGrounded()
        {
            var mover = NewMover(new Vector3(0f, 10f, 0f));

            var flags = mover.Move(new Vector3(0f, -1f, 0f));

            Assert.That(flags, Is.EqualTo(CapsuleContacts.None));
            Assert.That(mover.IsGrounded, Is.False);
            Assert.That(mover.Position.Y, Is.EqualTo(9f).Within(1e-6f));
        }

        [Test]
        public void WalksTheFullDistanceOnFlatGround()
        {
            AddFloor();
            var mover = Grounded(new Vector3(0f, 1f, 0f));

            for (var i = 0; i < 60; i++)
            {
                mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
                Assert.That(mover.IsGrounded, Is.True, $"frame {i}");
            }

            Assert.That(mover.Position.X, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(mover.Position.Y, Is.EqualTo(_skin).Within(1e-3f));
        }

        [Test]
        public void StopsAtAWallItWalksStraightInto()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 0f, -10f), new Vector3(3f, 5f, 10f));
            var mover = Grounded(new Vector3(0f, 1f, 0f));
            var touched = CapsuleContacts.None;

            for (var i = 0; i < 60; i++)
            {
                touched |= mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
            }

            Assert.That(touched & CapsuleContacts.Sides, Is.EqualTo(CapsuleContacts.Sides));
            Assert.That(mover.Position.X, Is.EqualTo(2f - _radius - _skin).Within(1e-3f));
            Assert.That(mover.Position.Z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(mover.IsGrounded, Is.True);
        }

        [Test]
        public void SlidesAlongAWallItWalksAtAnAngle()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 0f, -10f), new Vector3(3f, 5f, 20f));
            var mover = Grounded(new Vector3(0f, 1f, 0f));

            for (var i = 0; i < 60; i++)
            {
                mover.Move(new Vector3(_walkStep, _gravityStep, _walkStep));
            }

            Assert.That(mover.Position.X, Is.LessThan(2f - _radius).And.GreaterThan(2f - _radius - (_skin * 2f)));
            Assert.That(mover.Position.Z, Is.GreaterThan(4.9f).And.LessThanOrEqualTo(5f + 1e-4f));
        }

        [Test]
        public void SlidesAlongTheCreaseOfTwoWalls()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 0f, -10f), new Vector3(3f, 5f, 10f));
            _world.AddBox(new Vector3(-10f, 0f, 2f), new Vector3(10f, 5f, 3f));
            var mover = Grounded(new Vector3(0f, 1f, 0f));

            for (var i = 0; i < 120; i++)
            {
                mover.Move(new Vector3(_walkStep, _gravityStep, _walkStep));
            }

            Assert.That(mover.Position.X, Is.LessThan(2f - _radius));
            Assert.That(mover.Position.Z, Is.LessThan(2f - _radius));
            Assert.That(mover.Position.X, Is.GreaterThan(2f - _radius - (_skin * 2f)));
            Assert.That(mover.Position.Z, Is.GreaterThan(2f - _radius - (_skin * 2f)));
            Assert.That(mover.IsGrounded, Is.True);
        }

        [Test]
        public void ClimbsAStepNoHigherThanTheStepOffset()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 0f, -10f), new Vector3(4f, 0.25f, 10f));
            var mover = Grounded(new Vector3(0f, 1f, 0f));

            for (var i = 0; i < 40; i++)
            {
                mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
                Assert.That(mover.IsGrounded, Is.True, $"frame {i}");
            }

            Assert.That(mover.Position.X, Is.GreaterThan(3f));
            Assert.That(mover.Position.Y, Is.EqualTo(0.25f + _skin).Within(1e-3f));
        }

        [Test]
        public void IsBlockedByAStepHigherThanTheStepOffset()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 0f, -10f), new Vector3(4f, 0.4f, 10f));
            var mover = Grounded(new Vector3(0f, 1f, 0f));
            var touched = CapsuleContacts.None;

            for (var i = 0; i < 60; i++)
            {
                touched |= mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
            }

            Assert.That(touched & CapsuleContacts.Sides, Is.EqualTo(CapsuleContacts.Sides));
            // 丸い底は段の角の下へ回り込むので、段の面へ半径より近くまで寄れる。
            Assert.That(mover.Position.X, Is.LessThan(1.6f));
            Assert.That(mover.Position.Y, Is.EqualTo(_skin).Within(1e-3f));
        }

        [Test]
        public void WalksDownAStepLowerThanTheStepOffset()
        {
            AddFloor();
            _world.AddBox(new Vector3(-10f, 0f, -10f), new Vector3(0f, 0.25f, 10f));
            var mover = NewMover(new Vector3(-2f, 1f, 0f));
            mover.Move(new Vector3(0f, -2f, 0f));
            Assume.That(mover.Position.Y, Is.EqualTo(0.25f + _skin).Within(1e-3f));
            var airborne = 0;
            var previousY = mover.Position.Y;

            for (var i = 0; i < 40; i++)
            {
                mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
                airborne += mover.IsGrounded ? 0 : 1;
                Assert.That(mover.Position.Y, Is.LessThanOrEqualTo(previousY + 1e-5f), $"frame {i}");
                previousY = mover.Position.Y;
            }

            // 丸い底が段の角を転がり落ちる間だけは、下ろす距離 (段の高さと重力のぶん) に床が届かない。
            Assert.That(airborne, Is.LessThanOrEqualTo(3));
            Assert.That(mover.IsGrounded, Is.True);
            Assert.That(mover.Position.X, Is.GreaterThan(1f));
            Assert.That(mover.Position.Y, Is.EqualTo(_skin).Within(1e-3f));
        }

        [Test]
        public void WalksUpASlopeWithinTheLimit()
        {
            AddSlope(Vector3.Zero, 30f);
            var mover = Grounded(new Vector3(0f, 2f, 0f));
            var startY = mover.Position.Y;

            for (var i = 0; i < 60; i++)
            {
                mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
                Assert.That(mover.IsGrounded, Is.True, $"frame {i}");
            }

            var climbed = mover.Position.Y - startY;
            Assert.That(mover.Position.X, Is.GreaterThan(3f));
            Assert.That(climbed, Is.EqualTo(mover.Position.X * MathF.Tan(30f * MathF.PI / 180f)).Within(0.05f));
        }

        [Test]
        public void DoesNotClimbASlopeSteeperThanTheLimit()
        {
            AddFloor();
            AddSlope(new Vector3(1f, 0f, 0f), 60f);
            var mover = Grounded(new Vector3(-1f, 1f, 0f));
            var touched = CapsuleContacts.None;

            for (var i = 0; i < 120; i++)
            {
                touched |= mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
            }

            Assert.That(touched & CapsuleContacts.Sides, Is.EqualTo(CapsuleContacts.Sides));
            Assert.That(mover.Position.Y, Is.LessThan(0.2f));
            Assert.That(mover.Position.X, Is.LessThan(1f));
            Assert.That(mover.IsGrounded, Is.True);
        }

        [Test]
        public void SlidesDownASlopeSteeperThanTheLimit()
        {
            AddSlope(Vector3.Zero, 60f);
            var mover = NewMover(new Vector3(2f, 6f, 0f));
            mover.Move(new Vector3(0f, -10f, 0f));
            var start = mover.Position;

            for (var i = 0; i < 30; i++)
            {
                mover.Move(new Vector3(0f, _gravityStep, 0f));
                Assert.That(mover.IsGrounded, Is.False, $"frame {i}");
            }

            Assert.That(mover.Position.Y, Is.LessThan(start.Y - 0.5f));
            Assert.That(mover.Position.X, Is.LessThan(start.X - 0.2f));
        }

        [Test]
        public void StopsUnderACeilingWhenMovingUp()
        {
            AddFloor();
            _world.AddBox(new Vector3(-10f, 3f, -10f), new Vector3(10f, 4f, 10f));
            var mover = Grounded(new Vector3(0f, 1f, 0f));

            var flags = mover.Move(new Vector3(0f, 2f, 0f));

            Assert.That(flags, Is.EqualTo(CapsuleContacts.Above));
            Assert.That(mover.IsGrounded, Is.False);
            Assert.That(mover.Position.Y + _standingHeight, Is.EqualTo(3f - _skin).Within(1e-3f));
        }

        [Test]
        public void DoesNotJumpOntoALedgeByTheStepOffset()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 0f, -10f), new Vector3(4f, 1.5f, 10f));
            var mover = NewMover(new Vector3(1f, 1f, 0f));

            mover.Move(new Vector3(0.5f, 0.1f, 0f));

            Assert.That(mover.Position.X, Is.LessThan(2f - _radius));
            Assert.That(mover.Position.Y, Is.EqualTo(1.1f).Within(1e-4f));
        }

        [Test]
        public void HeadroomIsBlockedOnlyByWhatOverlapsTheTallerShape()
        {
            AddFloor();
            _world.AddBox(new Vector3(-10f, 1.5f, -10f), new Vector3(10f, 2.5f, 10f));
            var mover = Grounded(new Vector3(0f, 0.5f, 0f), _crouchHeight, _crouchCenter);

            Assert.That(mover.HasHeadroom(_crouchHeight, _crouchCenter), Is.True);
            Assert.That(mover.HasHeadroom(_standingHeight, _standingCenter), Is.False);
            Assert.That(mover.IsClear(mover.Position + new Vector3(30f, 0f, 0f), _standingHeight, _standingCenter), Is.True);
        }

        [Test]
        public void HeadroomIgnoresAWallTouchedAtTheSkinGap()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 0f, -10f), new Vector3(3f, 5f, 10f));
            var mover = Grounded(new Vector3(0f, 1f, 0f));
            for (var i = 0; i < 60; i++)
            {
                mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
            }

            Assert.That(mover.HasHeadroom(_standingHeight, _standingCenter), Is.True);
        }

        [Test]
        public void ACrouchedShapePassesUnderACeilingThatStopsAStandingOne()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 1.5f, -10f), new Vector3(4f, 3f, 10f));
            var standing = Grounded(new Vector3(0f, 1f, 0f));
            var crouched = Grounded(new Vector3(0f, 1f, 0f), _crouchHeight, _crouchCenter);

            for (var i = 0; i < 120; i++)
            {
                standing.Move(new Vector3(_walkStep, _gravityStep, 0f));
                crouched.Move(new Vector3(_walkStep, _gravityStep, 0f));
            }

            Assert.That(standing.Position.X, Is.LessThan(2f - _radius));
            Assert.That(crouched.Position.X, Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void SetShapeChangesTheSweptCapsule()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 1.5f, -10f), new Vector3(4f, 3f, 10f));
            var mover = Grounded(new Vector3(0f, 1f, 0f));

            mover.SetShape(_crouchHeight, _crouchCenter);
            for (var i = 0; i < 36; i++)
            {
                mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
            }

            Assert.That(mover.Height, Is.EqualTo(_crouchHeight));
            Assert.That(mover.Center, Is.EqualTo(_crouchCenter));
            Assert.That(mover.Position.X, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(mover.HasHeadroom(_standingHeight, _standingCenter), Is.False);
        }

        [Test]
        public void SameInputsGiveBitIdenticalResults()
        {
            var first = Run(BuildCourse(), 0, null);
            var second = Run(BuildCourse(), 0, null);

            Assert.That(second, Is.EqualTo(first));
            var touched = CapsuleContacts.None;
            for (var i = 3; i < first.Count; i += 4)
            {
                touched |= (CapsuleContacts)first[i];
            }

            Assert.That(touched, Is.EqualTo(CapsuleContacts.Sides | CapsuleContacts.Above | CapsuleContacts.Below));
        }

        [Test]
        public void ResimulatingFromAPlacedStateGivesBitIdenticalResults()
        {
            const int resumeFrame = 170;
            var world = BuildCourse();
            var full = Run(world, 0, null);
            var mover = NewMover(world, new Vector3(0f, 1f, 0f), _standingHeight, _standingCenter);
            var states = new List<(Vector3 Position, CapsuleContacts Flags)>();
            for (var frame = 0; frame < _courseFrames; frame++)
            {
                mover.Move(CourseInput(frame, mover.Position));
                states.Add((mover.Position, mover.Contacts));
            }

            var saved = states[resumeFrame - 1];
            var resumed = Run(world, resumeFrame, saved);

            Assert.That(resumed, Is.EqualTo(full.GetRange(resumeFrame * 4, full.Count - (resumeFrame * 4))));
        }

        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CapsuleMoverSpec(91f, 0.3f, 0.08f, ~0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CapsuleMoverSpec(-1f, 0.3f, 0.08f, ~0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CapsuleMoverSpec(45f, -0.1f, 0.08f, ~0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CapsuleMoverSpec(45f, 0.3f, 0f, ~0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CapsuleMoverSpec(45f, float.PositiveInfinity, 0.08f, ~0));
            Assert.Throws<ArgumentNullException>(() => new CapsuleMover(null, CapsuleMoverSpec.Default, _radius, 2f, Vector3.Zero));
            Assert.Throws<ArgumentNullException>(() => new CapsuleMover(_world, null, _radius, 2f, Vector3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CapsuleMover(_world, CapsuleMoverSpec.Default, 0f, 2f, Vector3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CapsuleMover(_world, CapsuleMoverSpec.Default, _radius, -1f, Vector3.Zero));

            var mover = NewMover(Vector3.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => mover.Move(new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => mover.Place(new Vector3(0f, float.PositiveInfinity, 0f), 0));
        }

        [Test]
        public void IgnoresLayersOutsideTheMask()
        {
            AddFloor();
            _world.AddBox(new Vector3(2f, 0f, -10f), new Vector3(3f, 5f, 10f), layer: 3);
            var spec = new CapsuleMoverSpec(45f, 0.3f, _skin, ~(1 << 3));
            var mover = new CapsuleMover(_world, spec, _radius, _standingHeight, _standingCenter);
            mover.Place(new Vector3(0f, 1f, 0f), CapsuleContacts.None);
            mover.Move(new Vector3(0f, -2f, 0f));

            for (var i = 0; i < 60; i++)
            {
                mover.Move(new Vector3(_walkStep, _gravityStep, 0f));
            }

            Assert.That(mover.Position.X, Is.EqualTo(5f).Within(1e-3f));
        }

        private static FakeWorldCollision BuildCourse()
        {
            var world = new FakeWorldCollision();
            world.AddBox(new Vector3(-50f, -1f, -50f), new Vector3(50f, 0f, 50f));
            world.AddBox(new Vector3(3f, 0f, -2f), new Vector3(5f, 0.2f, 2f));
            world.AddBox(new Vector3(-6f, 0f, -6f), new Vector3(6f, 3f, -5f));
            world.AddBox(new Vector3(6f, 0f, -6f), new Vector3(7f, 3f, 6f));
            world.AddHalfSpace(new Vector3(-3f, 0f, 0f), new Vector3(MathF.Sin(0.5f), MathF.Cos(0.5f), 0f));
            world.AddBox(new Vector3(-2f, 2.2f, 2f), new Vector3(2f, 3f, 4f));
            return world;
        }

        private static Vector3 CourseInput(int frame, Vector3 position)
        {
            var waypoint = _waypoints[(frame / _framesPerWaypoint) % _waypoints.Length];
            var seed = ((uint)frame * 1664525u) + 1013904223u;
            var jitter = (((seed >> 8) / (float)(1 << 24)) - 0.5f) * 0.6f;
            var angle = MathF.Atan2(waypoint.Y - position.Z, waypoint.X - position.X) + jitter;
            var jump = (seed & 15u) == 0u;
            return new Vector3(MathF.Cos(angle) * _walkStep * 1.5f, jump ? 0.15f : _gravityStep, MathF.Sin(angle) * _walkStep * 1.5f);
        }

        private static List<int> Run(FakeWorldCollision world, int skip, (Vector3 Position, CapsuleContacts Flags)? start)
        {
            var mover = NewMover(world, new Vector3(0f, 1f, 0f), _standingHeight, _standingCenter);
            if (start.HasValue)
            {
                mover.Place(start.Value.Position, start.Value.Flags);
            }

            var bits = new List<int>();
            for (var frame = skip; frame < _courseFrames; frame++)
            {
                mover.Move(CourseInput(frame, mover.Position));
                bits.Add(BitConverter.SingleToInt32Bits(mover.Position.X));
                bits.Add(BitConverter.SingleToInt32Bits(mover.Position.Y));
                bits.Add(BitConverter.SingleToInt32Bits(mover.Position.Z));
                bits.Add((int)mover.Contacts);
            }

            return bits;
        }

        private static CapsuleMover NewMover(FakeWorldCollision world, Vector3 position, float height, Vector3 center)
        {
            var mover = new CapsuleMover(world, CapsuleMoverSpec.Default, _radius, height, center);
            mover.Place(position, CapsuleContacts.None);
            return mover;
        }

        private CapsuleMover NewMover(Vector3 position)
        {
            return NewMover(_world, position, _standingHeight, _standingCenter);
        }

        private CapsuleMover Grounded(Vector3 position)
        {
            return Grounded(position, _standingHeight, _standingCenter);
        }

        private CapsuleMover Grounded(Vector3 position, float height, Vector3 center)
        {
            var mover = NewMover(_world, position, height, center);
            mover.Move(new Vector3(0f, -10f, 0f));
            Assume.That(mover.IsGrounded, Is.True);
            return mover;
        }

        private void AddFloor()
        {
            _world.AddBox(new Vector3(-50f, -1f, -50f), new Vector3(50f, 0f, 50f));
        }

        private void AddSlope(Vector3 foot, float degrees)
        {
            var radians = degrees * MathF.PI / 180f;
            _world.AddHalfSpace(foot, new Vector3(-MathF.Sin(radians), MathF.Cos(radians), 0f));
        }
    }
}
