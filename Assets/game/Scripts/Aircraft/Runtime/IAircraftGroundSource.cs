using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public interface IAircraftGroundSource
    {
        AircraftGroundContact GroundContact(VehicleMotorInput input, Vector3 localVelocity, Vector3 localAngularVelocity);
    }
}
