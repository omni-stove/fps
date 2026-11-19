using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    // 点を Transform で持つのは、シーンに置いた点を動かせば走る道もそのまま変わるようにするため。
    [DisallowMultipleComponent]
    public sealed class VehicleRoute : MonoBehaviour
    {
        [SerializeField, Tooltip("通る点の並び。空なら直下の子をヒエラルキーの順に使う")]
        private Transform[] _points = Array.Empty<Transform>();

        [SerializeField, Tooltip("最後の点の次に最初の点へ戻って回り続けるか。切ると最後の点で止まる")]
        private bool _loop = true;

        private readonly List<System.Numerics.Vector3> _gizmoPositions = new List<System.Numerics.Vector3>();

        public bool Loop
        {
            get => _loop;
            set => _loop = value;
        }

        public IReadOnlyList<Transform> Points => _points;

        public void SetPoints(params Transform[] points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            for (var i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                {
                    throw new ArgumentException($"points[{i}] is null", nameof(points));
                }
            }

            _points = (Transform[])points.Clone();
        }

        // 消えた点は飛ばす。点を 1 つ消しただけで道全体が使えなくなるのを避けるため。
        public void CopyPositions(List<System.Numerics.Vector3> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            if (_points.Length > 0)
            {
                foreach (var point in _points)
                {
                    if (point != null)
                    {
                        into.Add(point.position.ToNumerics());
                    }
                }

                return;
            }

            for (var i = 0; i < transform.childCount; i++)
            {
                into.Add(transform.GetChild(i).position.ToNumerics());
            }
        }

        // 位置でなく Transform を渡すのは、点を Transform で持つ部品 (IBotPilot の部品) にも、動かした点をそのまま追わせるため。
        public void CopyPoints(List<Transform> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            if (_points.Length > 0)
            {
                foreach (var point in _points)
                {
                    if (point != null)
                    {
                        into.Add(point);
                    }
                }

                return;
            }

            for (var i = 0; i < transform.childCount; i++)
            {
                into.Add(transform.GetChild(i));
            }
        }

        private void OnDrawGizmos()
        {
            var positions = _gizmoPositions;
            CopyPositions(positions);
            if (positions.Count == 0)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (var i = 0; i < positions.Count; i++)
            {
                var from = positions[i].ToUnity();
                Gizmos.DrawWireSphere(from, 0.5f);
                var last = i == positions.Count - 1;
                if (last && !_loop)
                {
                    break;
                }

                Gizmos.DrawLine(from, positions[last ? 0 : i + 1].ToUnity());
            }
        }
    }
}
