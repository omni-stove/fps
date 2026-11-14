using System;
using System.Numerics;

namespace Fps.Core
{
    public sealed class CharacterCoverSensor
    {
        public const float ProbeHeight = 0.5f;

        public const float TopProbeHeight = 3f;

        // 上端を下ろす Ray で探すとき、当たった面から壁の内側へ入れる距離 (m)。CoverController と同じ値
        private const float _topProbeInset = 0.02f;

        // 端の検査の Ray を、壁までの距離よりこれだけ長く飛ばす (m)。CoverController と同じ値
        private const float _edgeProbeSlack = 0.3f;

        private static readonly Vector3 _down = new Vector3(0f, -1f, 0f);

        private readonly CharacterBody _body;

        public CharacterCoverSensor(CharacterBody body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
        }

        public CoverProbeResult Probe(Vector3 position, Vector3 direction, Vector3 facing, out float topY)
        {
            topY = 0f;
            direction = new Vector3(direction.X, 0f, direction.Z);
            var length = MathF.Sqrt((direction.X * direction.X) + (direction.Z * direction.Z));
            if (!(length * length >= 1e-12f))
            {
                return CoverProbeResult.None;
            }

            direction = new Vector3(direction.X / length, 0f, direction.Z / length);
            var feetY = _body.FeetY(position);
            var origin = new Vector3(position.X, feetY + ProbeHeight, position.Z);
            var world = _body.World;
            var mask = _body.LayerMask;
            if (!world.Raycast(origin, direction, CoverProbe.MaxEnterDistance, mask, out var hit))
            {
                return CoverProbeResult.None;
            }

            var normal = hit.Normal;
            var topHeight = MeasureTopHeight(hit, feetY);
            var flatNormal = new Vector3(normal.X, 0f, normal.Z);
            var wallOnLeft = true;
            var wallOnRight = true;
            var flatLength = MathF.Sqrt((flatNormal.X * flatNormal.X) + (flatNormal.Z * flatNormal.Z));
            if (flatLength * flatLength > 1e-12f)
            {
                flatNormal = new Vector3(flatNormal.X / flatLength, 0f, flatNormal.Z / flatLength);
                var right = CoverProbe.RightAlong(flatNormal);
                var reach = hit.Distance + _edgeProbeSlack;
                wallOnLeft = world.Raycast(origin - (right * CoverProbe.EdgeProbeDistance), -flatNormal, reach, mask, out _);
                wallOnRight = world.Raycast(origin + (right * CoverProbe.EdgeProbeDistance), -flatNormal, reach, mask, out _);
            }

            var sample = new CoverWallSample(hit.Distance, normal, topHeight, wallOnLeft, wallOnRight);
            var result = CoverProbe.Evaluate(sample, facing);
            if (result.IsWall)
            {
                topY = feetY + topHeight;
            }

            return result;
        }

        // 当たった壁の形だけに、上から下ろした Ray を当てて上端を測る。始点が壁の中 (それより高い壁) なら当たらないので上限の高さとする
        private float MeasureTopHeight(WorldHit hit, float feetY)
        {
            var flat = new Vector3(hit.Normal.X, 0f, hit.Normal.Z);
            var flatLength = MathF.Sqrt((flat.X * flat.X) + (flat.Z * flat.Z));
            var inset = flatLength * flatLength > 1e-12f ? hit.Point - (flat / flatLength * _topProbeInset) : hit.Point;
            var start = new Vector3(inset.X, feetY + TopProbeHeight, inset.Z);
            return _body.World.RaycastCollider(hit.ColliderId, start, _down, TopProbeHeight, out var top)
                ? top.Point.Y - feetY
                : TopProbeHeight;
        }
    }
}
