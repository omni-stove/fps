using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class HorseControlSchemeTests
    {
        private readonly List<Object> _created = new List<Object>();
        private HorseControlScheme _scheme;

        [SetUp]
        public void SetUp()
        {
            var horse = new GameObject("Horse");
            _created.Add(horse);
            _scheme = horse.AddComponent<HorseControlScheme>();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheVehicleUsesItWhenItIsOnTheSameObject()
        {
            Assert.That(VehicleControlSchemes.For(_scheme.gameObject), Is.SameAs(_scheme));
        }

        [Test]
        public void ForwardRightAndBoostGallopTurningRight()
        {
            var input = _scheme.ToMotorInput(null, Seat(new Vector2(1f, 1f), 0f, true), 0.02f);

            Assert.That(input[HorseStride.ThrottleAxis], Is.EqualTo(1f));
            Assert.That(input[HorseStride.TurnAxis], Is.EqualTo(1f));
            Assert.That(input[HorseStride.GallopAxis], Is.EqualTo(1f));
            Assert.That(input[HorseStride.JumpAxis], Is.EqualTo(0f));
        }

        [Test]
        public void MoveIsPassedThroughAsThrottleAndTurn()
        {
            var input = _scheme.ToMotorInput(null, Seat(new Vector2(-0.25f, 0.5f), 0f, false), 0.02f);

            Assert.That(input, Is.EqualTo(new VehicleMotorInput(0.5f, -0.25f, 0f, 0f)));
        }

        [Test]
        public void BackingUpKeepsTheNegativeThrottle()
        {
            var input = _scheme.ToMotorInput(null, Seat(new Vector2(0f, -1f), 0f, false), 0.02f);

            Assert.That(input[HorseStride.ThrottleAxis], Is.EqualTo(-1f));
            Assert.That(input[HorseStride.GallopAxis], Is.EqualTo(0f), "gallop is only on while boost is held");
        }

        [Test]
        public void PositiveVerticalJumps()
        {
            var input = _scheme.ToMotorInput(null, Seat(Vector2.zero, 1f, false), 0.02f);

            Assert.That(input[HorseStride.JumpAxis], Is.EqualTo(1f));
        }

        [Test]
        public void NegativeVerticalDoesNotJump()
        {
            var input = _scheme.ToMotorInput(null, Seat(Vector2.zero, -1f, false), 0.02f);

            Assert.That(input[HorseStride.JumpAxis], Is.EqualTo(0f));
            Assert.That(input.IsIdle, Is.True);
        }

        [Test]
        public void ANeutralSeatIsIdle()
        {
            var input = _scheme.ToMotorInput(null, VehicleSeatInput.Idle, 0.02f);

            Assert.That(input.IsIdle, Is.True);
        }

        private static VehicleSeatInput Seat(Vector2 move, float vertical, bool boost)
        {
            return new VehicleSeatInput(move, vertical, boost, false, false, false, Vector3.zero, Vector3.forward);
        }
    }
}
