using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class FakeAircraftGroundSource : MonoBehaviour, IAircraftGroundSource
    {
        public AircraftGroundContact Contact { get; set; }

        public int Calls { get; private set; }

        public VehicleMotorInput LastInput { get; private set; }

        public AircraftGroundContact GroundContact(VehicleMotorInput input, Vector3 localVelocity, Vector3 localAngularVelocity)
        {
            Calls++;
            LastInput = input;
            return Contact;
        }
    }
}
