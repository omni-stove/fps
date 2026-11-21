using System;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class FakeBotPilotedMotor : MonoBehaviour, IBotPilotedMotor
    {
        public Type PilotType { get; set; } = typeof(FakeBotPilot);

        public Type BotPilotType => PilotType;
    }
}
