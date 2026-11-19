using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーは PayloadCart を読めないので、Editor がシーンの台車から書き出した道筋・中継点・押す範囲を持つ。
    // 攻め側を持つのは、道筋の始点がどちらの陣地の側にあるかがマップで決まるため。
    public sealed class NetMapPayload
    {
        public NetMapPayload(Team attackers, IReadOnlyList<Vector3> points, IReadOnlyList<int> checkpointPoints, float radius, float height)
        {
            if (attackers != Team.Alpha && attackers != Team.Bravo)
            {
                throw new ArgumentOutOfRangeException(nameof(attackers), attackers, "attackers must be Alpha or Bravo");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(height > 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be positive and finite");
            }

            Route = new PayloadRoute(points, checkpointPoints);
            var checkpoints = new int[Route.CheckpointCount];
            for (var i = 0; i < checkpoints.Length; i++)
            {
                checkpoints[i] = Route.CheckpointPoint(i);
            }

            Attackers = attackers;
            CheckpointPoints = Array.AsReadOnly(checkpoints);
            Radius = radius;
            Height = height;
        }

        public Team Attackers { get; }

        // PayloadRoute は作った後に変わらないので、試合ごとに作り直さず同じものを Payload に渡せる
        public PayloadRoute Route { get; }

        public IReadOnlyList<int> CheckpointPoints { get; }

        public float Radius { get; }

        public float Height { get; }

        // 範囲は台車の位置を中心に上下へ半分ずつ取る (台車に付けた ControlPointZone と同じ形)
        public ControlPointArea AreaAt(double distance)
        {
            return new ControlPointArea(0, Route.PositionAt(distance), Radius, Height);
        }
    }
}
