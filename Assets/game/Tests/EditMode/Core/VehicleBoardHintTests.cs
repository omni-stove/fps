using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class VehicleBoardHintTests
    {
        [Test]
        public void HintsWithTheSameSeatRoleAndInputAreEqual()
        {
            var hint = new VehicleBoardHint(VehicleSeatRole.Driver, "Player/Interact");

            Assert.That(hint, Is.EqualTo(new VehicleBoardHint(VehicleSeatRole.Driver, "Player/Interact")));
            Assert.That(hint.GetHashCode(), Is.EqualTo(new VehicleBoardHint(VehicleSeatRole.Driver, "Player/Interact").GetHashCode()));
            Assert.That(hint, Is.Not.EqualTo(new VehicleBoardHint(VehicleSeatRole.Gunner, "Player/Interact")));
            Assert.That(hint, Is.Not.EqualTo(new VehicleBoardHint(VehicleSeatRole.Driver, "Player/Use")));
            Assert.That(hint.ToString(), Is.EqualTo("vehicleBoard role=Driver input=Player/Interact"));
        }

        [Test]
        public void AMissingInputIsEmptyAndAnUnknownRoleIsRejected()
        {
            Assert.That(new VehicleBoardHint(VehicleSeatRole.Passenger, null).InputActionPath, Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleBoardHint((VehicleSeatRole)99, "Player/Interact"));
        }
    }
}
