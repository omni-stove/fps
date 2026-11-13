using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 敵が隠れに行く遮蔽点を選び、どの敵がどの点を使っているかを持つ。同じ一覧を使う敵 (<see cref="EnemyBrain"/>) で 1 つを共有する。
    /// 選ぶのは、脅威 (プレイヤー) の方向を遮っていて、ほかの敵が使っておらず、敵から <see cref="MaxDistance"/> 以内の点のうち最も近いもの。
    /// 座標は Y が上で、遮っているかは水平面で判定する。
    /// </summary>
    public sealed class CoverPointSelector
    {
        /// <summary>敵からこの距離 (m) より遠い点は選ばない。</summary>
        public const float DefaultMaxDistance = 15f;

        /// <summary>点の <see cref="CoverPoint.Facing"/> と点から脅威への方向のなす角がこの角度 (度) 以下なら遮っているとみなす。</summary>
        public const float DefaultMaxCoverAngleDegrees = 60f;

        /// <summary>脅威が点からこの水平距離 (m) より近ければ、壁越しでも撃たれるので遮っているとみなさない。</summary>
        public const float DefaultMinThreatDistance = 2f;

        // 角度の境界ちょうどを float の丸めで落とさないための余裕。
        private const float _boundaryEpsilon = 1e-4f;

        private readonly CoverPoint[] _points;
        private readonly object[] _occupants;
        private readonly float _minCoverCos;

        /// <param name="points">遮蔽点の一覧。複製して持つ。</param>
        /// <param name="maxDistance">敵から選べる点までの距離の上限 (m)。0 より大きい有限値。</param>
        /// <param name="maxCoverAngleDegrees">遮っているとみなす角度の上限 (度)。0 以上 180 以下。</param>
        /// <param name="minThreatDistance">遮っているとみなす、点から脅威までの水平距離の下限 (m)。0 以上の有限値。</param>
        public CoverPointSelector(
            IReadOnlyList<CoverPoint> points,
            float maxDistance = DefaultMaxDistance,
            float maxCoverAngleDegrees = DefaultMaxCoverAngleDegrees,
            float minThreatDistance = DefaultMinThreatDistance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (!(maxDistance > 0f) || float.IsInfinity(maxDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "maxDistance must be positive and finite");
            }

            if (!(maxCoverAngleDegrees >= 0f) || maxCoverAngleDegrees > 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCoverAngleDegrees), maxCoverAngleDegrees, "maxCoverAngleDegrees must be between 0 and 180");
            }

            if (!(minThreatDistance >= 0f) || float.IsInfinity(minThreatDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(minThreatDistance), minThreatDistance, "minThreatDistance must be non-negative and finite");
            }

            _points = new CoverPoint[points.Count];
            for (var i = 0; i < _points.Length; i++)
            {
                _points[i] = points[i];
            }

            Points = Array.AsReadOnly(_points);
            _occupants = new object[_points.Length];
            _minCoverCos = (float)Math.Cos(maxCoverAngleDegrees * (Math.PI / 180.0));
            MaxDistance = maxDistance;
            MaxCoverAngleDegrees = maxCoverAngleDegrees;
            MinThreatDistance = minThreatDistance;
        }

        /// <summary>遮蔽点の一覧。</summary>
        public IReadOnlyList<CoverPoint> Points { get; }

        public float MaxDistance { get; }

        public float MaxCoverAngleDegrees { get; }

        public float MinThreatDistance { get; }

        /// <summary>index の点が threat の方向を遮っているか。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index が範囲外のとき、threat が有限でないとき。</exception>
        public bool Protects(int index, Vector3 threat)
        {
            RequireIndex(index);
            RequireFinite(threat, nameof(threat));
            return ProtectsUnchecked(_points[index], threat);
        }

        /// <summary>
        /// self にいる敵が threat から隠れるのに使う点の添字を返す。選べる点が無ければ -1。
        /// owner が使っている点は選べる (ほかの敵が使っている点だけを外す)。excluded に入っている添字は選ばない。
        /// 距離が同じなら添字の小さい方を選ぶ。選ぶだけで、使っている印は付けない (<see cref="TryClaim"/>)。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">self・threat が有限でないとき。</exception>
        public int Select(Vector3 self, Vector3 threat, object owner = null, ICollection<int> excluded = null)
        {
            RequireFinite(self, nameof(self));
            RequireFinite(threat, nameof(threat));

            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < _points.Length; i++)
            {
                var occupant = _occupants[i];
                if (occupant != null && !ReferenceEquals(occupant, owner))
                {
                    continue;
                }

                if (excluded != null && excluded.Contains(i))
                {
                    continue;
                }

                var distance = Vector3.Distance(self, _points[i].Position);
                if (distance > MaxDistance || distance >= bestDistance)
                {
                    continue;
                }

                if (!ProtectsUnchecked(_points[i], threat))
                {
                    continue;
                }

                best = i;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>
        /// owner が index の点を使う印を付ける。owner が別の点を使っていたらそちらは放す。
        /// ほかの敵が使っていれば何もせず false を返す。
        /// </summary>
        /// <exception cref="ArgumentNullException">owner が null のとき。</exception>
        /// <exception cref="ArgumentOutOfRangeException">index が範囲外のとき。</exception>
        public bool TryClaim(int index, object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            RequireIndex(index);
            var occupant = _occupants[index];
            if (occupant != null && !ReferenceEquals(occupant, owner))
            {
                return false;
            }

            Release(owner);
            _occupants[index] = owner;
            return true;
        }

        /// <summary>owner が使っている点を放す。放す点があれば true。</summary>
        public bool Release(object owner)
        {
            if (owner == null)
            {
                return false;
            }

            var released = false;
            for (var i = 0; i < _occupants.Length; i++)
            {
                if (ReferenceEquals(_occupants[i], owner))
                {
                    _occupants[i] = null;
                    released = true;
                }
            }

            return released;
        }

        /// <summary>index の点を誰かが使っているか。</summary>
        public bool IsOccupied(int index)
        {
            RequireIndex(index);
            return _occupants[index] != null;
        }

        /// <summary>owner が使っている点の添字。使っていなければ -1。</summary>
        public int IndexClaimedBy(object owner)
        {
            if (owner == null)
            {
                return -1;
            }

            for (var i = 0; i < _occupants.Length; i++)
            {
                if (ReferenceEquals(_occupants[i], owner))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool ProtectsUnchecked(CoverPoint point, Vector3 threat)
        {
            var toThreat = threat - point.Position;
            toThreat.Y = 0f;
            var distance = toThreat.Length();
            if (distance < MinThreatDistance || !(distance > 0f))
            {
                return false;
            }

            return Vector3.Dot(point.Facing, toThreat / distance) >= _minCoverCos - _boundaryEpsilon;
        }

        private void RequireIndex(int index)
        {
            if (index < 0 || index >= _points.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be within the cover points");
            }
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!CoverPoint.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }
    }
}
