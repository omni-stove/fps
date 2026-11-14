using System;
using Fps.Core;
using UnityEngine;

// ルールの列挙 Fps.Core.Zipline と同じ名前なので、Fps.Runtime に置くと GameRulesDefinition などが書いている Zipline がこの型を指してしまう。
namespace Fps.Runtime.Traversal
{
    // 見た目は持たない。線の形は置く側 (シーンの生成スクリプトやプレハブ) が決め、ここは掴まる線分と滑り方だけを持つ。
    [DisallowMultipleComponent]
    public sealed class Zipline : MonoBehaviour
    {
        public const string LogTag = "[Zipline]";

        // 両端を子の Transform でなくローカル座標で持つのは、スクリプトから置くときに参照の配線を要らなくするため。
        [SerializeField, Tooltip("一方の端 (この Transform から見た位置)")]
        private Vector3 _start = new Vector3(0f, 0f, -5f);

        [SerializeField, Tooltip("もう一方の端 (この Transform から見た位置)")]
        private Vector3 _end = new Vector3(0f, -1f, 5f);

        [SerializeField, Min(0.01f), Tooltip("滑る速さの下限 (m/s)。高低差の無い線はこの速さで進む")]
        private float _minSpeed = ZiplineSpec.DefaultMinSpeed;

        [SerializeField, Min(0.01f), Tooltip("滑る速さの上限 (m/s)")]
        private float _maxSpeed = ZiplineSpec.DefaultMaxSpeed;

        [SerializeField, Min(0f), Tooltip("高低差を速さに変える重力加速度 (m/s²)")]
        private float _gravity = ZiplineSpec.DefaultGravity;

        [SerializeField, Min(0f), Tooltip("両端の手前で止まる距離 (m)")]
        private float _endMargin = ZiplineSpec.DefaultEndMargin;

        public Vector3 LocalStart => _start;

        public Vector3 LocalEnd => _end;

        public Vector3 StartPoint => transform.TransformPoint(_start);

        public Vector3 EndPoint => transform.TransformPoint(_end);

        public float MinSpeed => _minSpeed;

        public float MaxSpeed => _maxSpeed;

        public float Gravity => _gravity;

        public float EndMargin => _endMargin;

        public bool IsUsable => enabled && gameObject.activeInHierarchy;

        // 線の検査を値を変える側で済ませておけば、掴まる瞬間に不正な線で失敗しない。
        public void SetEnds(Vector3 startWorld, Vector3 endWorld)
        {
            var start = transform.InverseTransformPoint(startWorld);
            var end = transform.InverseTransformPoint(endWorld);
            _ = new ZiplineMotion(startWorld.ToNumerics(), endWorld.ToNumerics(), CreateSpec());
            _start = start;
            _end = end;
        }

        public void SetSpeeds(float minSpeed, float maxSpeed, float gravity, float endMargin)
        {
            var spec = new ZiplineSpec(minSpeed, maxSpeed, gravity, endMargin);
            _ = new ZiplineMotion(StartPoint.ToNumerics(), EndPoint.ToNumerics(), spec);
            _minSpeed = minSpeed;
            _maxSpeed = maxSpeed;
            _gravity = gravity;
            _endMargin = endMargin;
        }

        public ZiplineSpec CreateSpec()
        {
            return new ZiplineSpec(_minSpeed, _maxSpeed, _gravity, _endMargin);
        }

        // Inspector で壊された値はここで初めて分かるので、投げずに理由を残して掴まらせない。
        public bool TryCreateMotion(out ZiplineMotion motion)
        {
            try
            {
                motion = new ZiplineMotion(StartPoint.ToNumerics(), EndPoint.ToNumerics(), CreateSpec());
                return true;
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"{LogTag} '{name}' cannot be ridden: {e.Message}", this);
                motion = null;
                return false;
            }
        }

        // 登録表を持たずに探すのは、Edit Mode のテストで OnEnable が走らなくても見つけられるようにするため。掴まろうとしたときにしか呼ばない。
        public static Zipline FindNearest(Vector3 point, float maxDistance)
        {
            Zipline best = null;
            var bestDistance = maxDistance;
            foreach (var line in FindObjectsByType<Zipline>(FindObjectsSortMode.None))
            {
                if (!line.IsUsable)
                {
                    continue;
                }

                var distance = line.DistanceTo(point);
                if (distance <= bestDistance)
                {
                    best = line;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public float DistanceTo(Vector3 point)
        {
            var start = StartPoint;
            var segment = EndPoint - start;
            var lengthSquared = segment.sqrMagnitude;
            var t = lengthSquared > 0f ? Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared) : 0f;
            return Vector3.Distance(point, start + (segment * t));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(StartPoint, EndPoint);
        }
    }
}
