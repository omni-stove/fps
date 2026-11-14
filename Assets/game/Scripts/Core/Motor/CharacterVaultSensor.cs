using System;
using System.Numerics;

namespace Fps.Core
{
    public sealed class CharacterVaultSensor
    {
        public const float ProbeHeight = 0.35f;

        public const float TopProbeMargin = 0.3f;

        public const float SurfaceInset = 0.02f;

        public const float DepthSlack = 0.05f;

        private static readonly Vector3 _down = new Vector3(0f, -1f, 0f);

        private readonly CharacterBody _body;
        private readonly Func<VaultPath, bool> _isPathClear;
        private Vector3 _pathOrigin;

        public CharacterVaultSensor(CharacterBody body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _isPathClear = IsPathClear;
        }

        public VaultProbeResult Probe(Vector3 position, float yaw, float maxHeight)
        {
            var forward = CharacterMotorSimulation.YawForward(yaw);
            var feetY = _body.FeetY(position);
            var origin = new Vector3(position.X, feetY + ProbeHeight, position.Z);
            var world = _body.World;
            if (!world.Raycast(origin, forward, VaultProbe.MaxReach, _body.LayerMask, out var hit))
            {
                return VaultProbeResult.None;
            }

            var flat = new Vector3(hit.Normal.X, 0f, hit.Normal.Z);
            var flatLength = MathF.Sqrt((flat.X * flat.X) + (flat.Z * flat.Z));
            var into = flatLength > 0f ? -flat / flatLength : Vector3.Zero;
            var topHeight = MeasureTop(hit, into, feetY, maxHeight);
            var depth = into == Vector3.Zero ? float.PositiveInfinity : MeasureDepth(hit, into);
            var along = (forward.X * into.X) + (forward.Z * into.Z);
            var sample = new VaultObstacleSample(hit.Distance * along, hit.Normal, topHeight, depth);
            _pathOrigin = position;
            return VaultProbe.Evaluate(sample, forward, maxHeight, _body.Radius, _isPathClear);
        }

        // 当たった形だけに上から Ray を下ろして上端を測る。始点が中 (それより高い) なら当たらないので、始点の高さ (上限より高い) とする
        private float MeasureTop(WorldHit hit, Vector3 into, float feetY, float maxHeight)
        {
            var startHeight = maxHeight + TopProbeMargin;
            var inset = hit.Point + (into * SurfaceInset);
            var start = new Vector3(inset.X, feetY + startHeight, inset.Z);
            return _body.World.RaycastCollider(hit.ColliderId, start, _down, startHeight, out var top)
                ? Math.Max(0f, top.Point.Y - feetY)
                : startHeight;
        }

        // 手前の面から MaxDepth より先から当たった形へ戻る Ray で向こうの面を探す。始点が中 (もっと厚い) なら当たらないので無限大とする
        private float MeasureDepth(WorldHit hit, Vector3 into)
        {
            var reach = VaultProbe.MaxDepth + DepthSlack;
            return _body.World.RaycastCollider(hit.ColliderId, hit.Point + (into * reach), -into, reach, out var back)
                ? Math.Max(0f, reach - back.Distance)
                : float.PositiveInfinity;
        }

        // 軌跡の頂点と各区間の中点に立った体を置き、どこも重ならなければ通れるとする (PlayerController と同じ粗さ)
        private bool IsPathClear(VaultPath path)
        {
            var points = path.Points;
            for (var i = 1; i < points.Count; i++)
            {
                var from = points[i - 1];
                var to = points[i];
                if (!_body.IsStandingClear(_pathOrigin + ((from + to) * 0.5f)) || !_body.IsStandingClear(_pathOrigin + to))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
