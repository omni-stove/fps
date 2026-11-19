using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class FakeAircraftConditionSource : MonoBehaviour, IAircraftConditionSource
    {
        public AircraftCondition Condition { get; set; } = AircraftCondition.Intact;
    }
}
