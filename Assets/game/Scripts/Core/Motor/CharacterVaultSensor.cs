using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class CharacterVaultSensor
    {
        public const float ProbeHeight = VaultProbe.LowestProbeHeight;

        public const float TopProbeMargin = 0.3f;

        public const float SurfaceInset = 0.02f;

        public const float DepthSlack = 0.05f;

        private static readonly Vector3 _down = new Vector3(0f, -1f, 0f);

        private readonly CharacterBody _body;
        private readonly Func<VaultPath, bool> _isPathClear;
        private readonly VaultProbe.FaceRaycast _faceRaycast;
        private Vector3 _pathOrigin;

        public CharacterVaultSensor(CharacterBody body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _isPathClear = IsPathClear;
            _faceRaycast = FaceRaycast;
        }

        public VaultProbeResult Probe(Vector3 position, float yaw, MotorSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return TryMeasure(position, yaw, spec.VaultMaxHeight, spec.VaultReach, spec.VaultMaxDepth, out var sample, out var forward)
                ? VaultProbe.Evaluate(sample, forward, spec, _body.Radius, _isPathClear)
                : VaultProbeResult.None;
        }

        // 面の当て方と上端の測り方は乗り越えと同じで、高さの上限だけを崖のぼりの上限に替える。
        // 奥行きは上に立てる広さの先から測る (PlayerController.MeasureMantle と同じ)。乗り越えの上限で測ると、立てる段を狭いと誤るため。
        public VaultProbeResult ProbeMantle(Vector3 position, float yaw, MotorSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var footing = VaultProbe.MantleFootingDepth(_body.Radius);
            return TryMeasure(position, yaw, spec.MantleMaxHeight, spec.VaultReach, footing, out var sample, out var forward)
                ? VaultProbe.EvaluateMantle(sample, forward, spec, _body.Radius, _isPathClear)
                : VaultProbeResult.None;
        }

        private bool TryMeasure(
            Vector3 position, float yaw, float maxHeight, float reach, float depthReach, out VaultObstacleSample sample, out Vector3 forward)
        {
            forward = CharacterMotorSimulation.YawForward(yaw);
            sample = default;
            var feetY = _body.FeetY(position);
            if (!TryFindFace(position, feetY, forward, maxHeight, reach, out var hit))
            {
                return false;
            }

            var flat = new Vector3(hit.Normal.X, 0f, hit.Normal.Z);
            var flatLength = MathF.Sqrt((flat.X * flat.X) + (flat.Z * flat.Z));
            var into = flatLength > 0f ? -flat / flatLength : Vector3.Zero;
            var topHeight = MeasureTop(hit, into, feetY, maxHeight);
            var depth = into == Vector3.Zero ? float.PositiveInfinity : MeasureDepth(hit, into, depthReach);
            var along = (forward.X * into.X) + (forward.Z * into.Z);
            var width = into == Vector3.Zero ? 0f : VaultProbe.MeasureFaceWidth(hit.Point, into, _body.Radius, _faceRaycast);
            sample = new VaultObstacleSample(hit.Distance * along, hit.Normal, topHeight, depth, width);
            _pathOrigin = position;
            return true;
        }

        // 1 本では張り出した縁の下をくぐるので高さを並べる。並べ方と選び方は PlayerController.TryFindVaultFace と揃え、予測と答えを同じにする
        private bool TryFindFace(Vector3 position, float feetY, Vector3 forward, float maxHeight, float reach, out WorldHit face)
        {
            var found = false;
            face = default;
            var count = VaultProbe.ProbeHeightCount(maxHeight);
            for (var i = 0; i < count; i++)
            {
                var origin = new Vector3(position.X, feetY + VaultProbe.ProbeHeightAt(i), position.Z);
                if (_body.World.Raycast(origin, forward, reach, _body.LayerMask, out var hit)
                    && VaultProbe.IsBetterFace(found, face.Normal, face.Distance, hit.Normal, hit.Distance))
                {
                    face = hit;
                    found = true;
                }
            }

            return found;
        }

        private bool FaceRaycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 normal, out float distance)
        {
            if (_body.World.Raycast(origin, direction, maxDistance, _body.LayerMask, out var hit))
            {
                normal = hit.Normal;
                distance = hit.Distance;
                return true;
            }

            normal = Vector3.Zero;
            distance = 0f;
            return false;
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

        // 手前の面から奥行きの上限より先から当たった形へ戻る Ray で向こうの面を探す。始点が中 (もっと厚い) なら当たらないので無限大とする
        private float MeasureDepth(WorldHit hit, Vector3 into, float maxDepth)
        {
            var reach = maxDepth + DepthSlack;
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
