using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Net;
using UnityEngine;

namespace Fps.Net.Tests
{
    // 試合と同じく、テストのシーンも .fpsmap と同じ書き出しで世界にしてから動かす
    internal static class NetTestWorld
    {
        public static IWorldCollision Collect(IReadOnlyList<GameObject> roots)
        {
            Physics.SyncTransforms();
            return new BvhWorldCollision(MapCollisionExporter.Collect(roots));
        }

        public static NetMotorSimulation CreateSimulation(
            CharacterController controller, MotorSpec spec, float crouchHeight, IReadOnlyList<GameObject> roots)
        {
            var stance = NetStance.FromHierarchy(controller, crouchHeight);
            var mover = new CapsuleMoverSpec(controller.slopeLimit, controller.stepOffset, controller.skinWidth, CapsuleMoverSpec.DefaultLayerMask);
            return new NetMotorSimulation(
                controller,
                new PlayerMotor(spec),
                stance,
                Collect(roots),
                mover,
                NetworkPlayerMotor.TickSeconds,
                NetworkPlayerMotor.KillPlaneY);
        }
    }
}
