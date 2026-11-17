using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Shooter.Runtime
{
    // 敵が CoverPointSelector を 1 つ共有するのは、同じ selector を渡した敵どうしが同じ点を選ばないようにするため。
    // 点は動かない地形に置く前提なので、点の位置を NavMesh に寄せた selector は、点の一覧か選び方の設定を変えるまで作り直さない。
    [DisallowMultipleComponent]
    public sealed class CoverPointRegistry : MonoBehaviour
    {
        public const string LogTag = "[Cover]";

        public const float NavMeshSnapRadius = 1f;

        [SerializeField, Tooltip("シーンの遮蔽点。シーン生成スクリプトが配線する")]
        private CoverPoint[] _points = Array.Empty<CoverPoint>();

        [SerializeField, Min(0.01f), Tooltip("敵からこの距離 (m) より遠い点は選ばない")]
        private float _maxDistance = CoverPointSelector.DefaultMaxDistance;

        [SerializeField, Range(0f, 180f), Tooltip("点の向きと点から脅威への方向のなす角がこの角度 (度) 以下なら遮っているとみなす")]
        private float _maxCoverAngleDegrees = CoverPointSelector.DefaultMaxCoverAngleDegrees;

        [SerializeField, Min(0f), Tooltip("脅威が点からこの水平距離 (m) より近ければ遮っているとみなさない")]
        private float _minThreatDistance = CoverPointSelector.DefaultMinThreatDistance;

        private CoverPointSelector _selector;
        private bool _built;
        private readonly List<CoverPoint> _markers = new List<CoverPoint>();
        private readonly List<Vector3> _peekPositions = new List<Vector3>();

        public IReadOnlyList<CoverPoint> Points => _points;

        public float MaxDistance
        {
            get => _maxDistance;
            set
            {
                _maxDistance = value;
                Invalidate();
            }
        }

        public float MaxCoverAngleDegrees
        {
            get => _maxCoverAngleDegrees;
            set
            {
                _maxCoverAngleDegrees = value;
                Invalidate();
            }
        }

        public float MinThreatDistance
        {
            get => _minThreatDistance;
            set
            {
                _minThreatDistance = value;
                Invalidate();
            }
        }

        public CoverPointSelector Selector
        {
            get
            {
                if (!_built)
                {
                    Build();
                }

                return _selector;
            }
        }

        public void SetPoints(IReadOnlyList<CoverPoint> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var copy = new List<CoverPoint>(points.Count);
            foreach (var point in points)
            {
                if (point != null)
                {
                    copy.Add(point);
                }
            }

            _points = copy.ToArray();
            Invalidate();
        }

        public CoverPoint MarkerAt(int index)
        {
            return _built && index >= 0 && index < _markers.Count ? _markers[index] : null;
        }

        public Vector3? PeekPositionAt(int index)
        {
            return _built && index >= 0 && index < _peekPositions.Count ? _peekPositions[index] : (Vector3?)null;
        }

        public bool IsOccupied(CoverPoint marker)
        {
            var selector = Selector;
            if (selector == null || marker == null)
            {
                return false;
            }

            var index = _markers.IndexOf(marker);
            return index >= 0 && selector.IsOccupied(index);
        }

        private void Invalidate()
        {
            _built = false;
            _selector = null;
            _markers.Clear();
            _peekPositions.Clear();
        }

        private void Build()
        {
            _built = true;
            _selector = null;
            _markers.Clear();
            _peekPositions.Clear();

            var points = new List<Shooter.Core.CoverPoint>(_points.Length);
            foreach (var marker in _points)
            {
                if (marker == null)
                {
                    Debug.LogWarning($"{LogTag} '{name}' has a missing cover point; rerun the scene builder", this);
                    continue;
                }

                var position = Snap(marker, marker.Position, "cover point");
                if (!marker.TryToCore(position, out var point, out var error))
                {
                    Debug.LogError($"{LogTag} cover point '{marker.name}' is invalid: {error}", marker);
                    continue;
                }

                points.Add(point);
                _markers.Add(marker);
                _peekPositions.Add(Snap(marker, marker.PeekPosition, "peek position"));
            }

            if (points.Count == 0)
            {
                _markers.Clear();
                _peekPositions.Clear();
                Debug.LogWarning($"{LogTag} '{name}' has no usable cover points; enemies will not take cover", this);
                return;
            }

            try
            {
                _selector = new CoverPointSelector(points, _maxDistance, _maxCoverAngleDegrees, _minThreatDistance);
            }
            catch (ArgumentOutOfRangeException e)
            {
                _markers.Clear();
                _peekPositions.Clear();
                Debug.LogError($"{LogTag} '{name}' has invalid settings: {e.Message}", this);
                return;
            }

            Debug.Log($"{LogTag} registry={name} points={points.Count}", this);
        }

        // EnemyAgent が巡回地点を寄せるのと同じく、寄せられなければ警告してそのまま使う (着けなければ EnemyBrain が別の点を選ぶ)。
        private static Vector3 Snap(CoverPoint marker, Vector3 position, string what)
        {
            if (NavMesh.SamplePosition(position, out var hit, NavMeshSnapRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            Debug.LogWarning($"{LogTag} {what} of '{marker.name}' {position} is not on the NavMesh; using it as is", marker);
            return position;
        }

        private void OnValidate()
        {
            Invalidate();
        }
    }
}
