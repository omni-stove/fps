using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Fps.Runtime
{
    // 枠は任意 (PresentationCatalog の規約)。無いか中身が足りなければ DeathBodyPresenter は 1 つの塊の抜け殻で倒す。
    [CreateAssetMenu(fileName = "RagdollCatalog", menuName = "Fps/Ragdoll Catalog")]
    public sealed class RagdollCatalog : ScriptableObject
    {
        public const string CatalogName = "Ragdoll";

        // PresentationCatalog.CatalogResourcePath(CatalogName) と同じ値。メソッドを呼ばず const にして、ログの文字列にも使えるようにする。
        public const string ResourcePath = PresentationCatalog.ResourceFolder + "/" + CatalogName + PresentationCatalog.CatalogSuffix;

        public const float DefaultSettleSeconds = 2f;
        public const float DefaultBaseSpeed = 1.5f;
        public const float DefaultSpeedPerDamage = 0.04f;
        public const float DefaultMaxSpeed = 6f;

        [SerializeField, Tooltip("ボーンの名前と、そのボーンのコライダー・質量・関節の可動域。名前は重複させない")]
        private List<RagdollBone> _bones = new List<RagdollBone>();

        [SerializeField, Min(0.01f), Tooltip("倒れてから物理を止めて固めるまでの秒数")]
        private float _settleSeconds = DefaultSettleSeconds;

        [SerializeField, Min(0f), Tooltip("ダメージが 0 のときに上半身に与える水平の速さ (m/s)")]
        private float _baseSpeed = DefaultBaseSpeed;

        [SerializeField, Min(0f), Tooltip("ダメージ 1 あたりに足す速さ (m/s)")]
        private float _speedPerDamage = DefaultSpeedPerDamage;

        [SerializeField, Min(0f), Tooltip("与える速さの上限 (m/s)")]
        private float _maxSpeed = DefaultMaxSpeed;

        public IReadOnlyList<RagdollBone> Bones => _bones ??= new List<RagdollBone>();

        public float SettleSeconds
        {
            get => _settleSeconds;
            set => _settleSeconds = value;
        }

        public float BaseSpeed
        {
            get => _baseSpeed;
            set => _baseSpeed = value;
        }

        public float SpeedPerDamage
        {
            get => _speedPerDamage;
            set => _speedPerDamage = value;
        }

        public float MaxSpeed
        {
            get => _maxSpeed;
            set => _maxSpeed = value;
        }

        public static RagdollCatalog Load()
        {
            return Resources.Load<RagdollCatalog>(ResourcePath);
        }

        public void SetBones(IEnumerable<RagdollBone> bones)
        {
            _bones = bones == null ? new List<RagdollBone>() : new List<RagdollBone>(bones);
        }

        // 負のダメージ (回復を同じ口で流す場合) で逆向きに押さないよう 0 に揃える。
        public float SpeedFor(int damage)
        {
            var speed = _baseSpeed + (_speedPerDamage * Math.Max(0, damage));
            return Mathf.Clamp(speed, 0f, _maxSpeed);
        }

        public bool TryGetBone(string bone, out RagdollBone entry)
        {
            entry = null;
            if (bone == null)
            {
                return false;
            }

            foreach (var candidate in Bones)
            {
                if (candidate != null && string.Equals(candidate.Bone, bone, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        // 1 つ目で止めず全部並べるのは、手で直すときに何度も走らせ直さずに済むようにするため。
        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed ragdoll catalog)" : name;
            var problems = new List<string>();

            if (Bones.Count == 0)
            {
                problems.Add("has no bones");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < Bones.Count; i++)
            {
                var entry = Bones[i];
                if (entry == null)
                {
                    problems.Add($"bone #{i.ToString(CultureInfo.InvariantCulture)} is empty");
                    continue;
                }

                var label = $"bone #{i.ToString(CultureInfo.InvariantCulture)} ('{entry.Bone}')";
                if (string.IsNullOrEmpty(entry.Bone))
                {
                    problems.Add($"bone #{i.ToString(CultureInfo.InvariantCulture)} has no bone name");
                }
                else if (!seen.Add(entry.Bone))
                {
                    problems.Add($"{label} uses a bone name already used by another entry");
                }

                entry.CollectProblems(label, problems);
            }

            RequirePositive(problems, _settleSeconds, "settle seconds");
            RequireNonNegative(problems, _baseSpeed, "base speed");
            RequireNonNegative(problems, _speedPerDamage, "speed per damage");
            RequireNonNegative(problems, _maxSpeed, "max speed");
            if (IsFinite(_baseSpeed) && IsFinite(_maxSpeed) && _maxSpeed < _baseSpeed)
            {
                problems.Add("max speed is smaller than base speed");
            }

            if (problems.Count > 0)
            {
                throw new RagdollCatalogException($"ragdoll catalog '{source}': {string.Join("; ", problems)}");
            }
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static void RequirePositive(List<string> problems, float value, string what)
        {
            if (!(value > 0f) || !IsFinite(value))
            {
                problems.Add($"{what} must be positive and finite");
            }
        }

        internal static void RequireNonNegative(List<string> problems, float value, string what)
        {
            if (!(value >= 0f) || !IsFinite(value))
            {
                problems.Add($"{what} must be zero or more and finite");
            }
        }
    }

    public enum RagdollShape
    {
        Box,
        Capsule,
        Sphere,
    }

    [Serializable]
    public sealed class RagdollBone
    {
        public const float MaxTwist = 180f;
        public const float MaxSwing = 180f;

        [SerializeField, Tooltip("リグのボーン (Transform) の名前。大文字小文字を区別する")]
        private string _bone = string.Empty;

        [SerializeField, Tooltip("コライダーの形")]
        private RagdollShape _shape = RagdollShape.Box;

        [SerializeField, Tooltip("コライダーの中心 (ボーンの座標系)")]
        private Vector3 _center = Vector3.zero;

        [SerializeField, Tooltip("コライダーを囲む箱の大きさ (ボーンの座標系)。Capsule は最も長い辺の向きに伸ばし、Sphere は最も長い辺を直径にする")]
        private Vector3 _size = Vector3.one * 0.1f;

        [SerializeField, Min(0.01f), Tooltip("質量 (kg)")]
        private float _mass = 1f;

        [SerializeField, Tooltip("ねじれの軸 (ボーンの座標系)。根のボーンでは使わない")]
        private Vector3 _twistAxis = Vector3.up;

        [SerializeField, Tooltip("振りの 1 つ目の軸 (ボーンの座標系)。ねじれの軸と平行にしない")]
        private Vector3 _swingAxis = Vector3.right;

        [SerializeField, Range(-MaxTwist, MaxTwist), Tooltip("ねじれの下限 (度)")]
        private float _lowTwist = -20f;

        [SerializeField, Range(-MaxTwist, MaxTwist), Tooltip("ねじれの上限 (度)")]
        private float _highTwist = 20f;

        [SerializeField, Range(0f, MaxSwing), Tooltip("振りの 1 つ目の軸まわりの可動域 (度)")]
        private float _swing1 = 30f;

        [SerializeField, Range(0f, MaxSwing), Tooltip("振りの 2 つ目の軸まわりの可動域 (度)")]
        private float _swing2 = 30f;

        public RagdollBone()
        {
        }

        public RagdollBone(
            string bone,
            RagdollShape shape,
            Vector3 center,
            Vector3 size,
            float mass,
            Vector3 twistAxis,
            Vector3 swingAxis,
            float lowTwist,
            float highTwist,
            float swing1,
            float swing2)
        {
            _bone = bone?.Trim() ?? string.Empty;
            _shape = shape;
            _center = center;
            _size = size;
            _mass = mass;
            _twistAxis = twistAxis;
            _swingAxis = swingAxis;
            _lowTwist = lowTwist;
            _highTwist = highTwist;
            _swing1 = swing1;
            _swing2 = swing2;
        }

        public string Bone => _bone ?? string.Empty;

        public RagdollShape Shape => _shape;

        public Vector3 Center => _center;

        public Vector3 Size => _size;

        public float Mass => _mass;

        public Vector3 TwistAxis => _twistAxis;

        public Vector3 SwingAxis => _swingAxis;

        public float LowTwist => _lowTwist;

        public float HighTwist => _highTwist;

        public float Swing1 => _swing1;

        public float Swing2 => _swing2;

        // CapsuleCollider.direction にそのまま入れられるよう、軸を 0: X, 1: Y, 2: Z の数で返す。
        public int LongestAxis
        {
            get
            {
                var size = Abs(_size);
                if (size.x >= size.y && size.x >= size.z)
                {
                    return 0;
                }

                return size.y >= size.z ? 1 : 2;
            }
        }

        internal void CollectProblems(string label, List<string> problems)
        {
            if (!Enum.IsDefined(typeof(RagdollShape), _shape))
            {
                problems.Add($"{label} has an unknown shape {(int)_shape}");
            }

            if (!IsFinite(_center))
            {
                problems.Add($"{label} center must be finite");
            }

            if (!(_size.x > 0f && _size.y > 0f && _size.z > 0f) || !IsFinite(_size))
            {
                problems.Add($"{label} size must be positive and finite on every axis");
            }

            RagdollCatalog.RequirePositive(problems, _mass, $"{label} mass");

            var twistOk = IsFinite(_twistAxis) && _twistAxis.sqrMagnitude > 1e-6f;
            var swingOk = IsFinite(_swingAxis) && _swingAxis.sqrMagnitude > 1e-6f;
            if (!twistOk)
            {
                problems.Add($"{label} twist axis must be a finite non-zero vector");
            }

            if (!swingOk)
            {
                problems.Add($"{label} swing axis must be a finite non-zero vector");
            }

            if (twistOk && swingOk && Vector3.Cross(_twistAxis.normalized, _swingAxis.normalized).sqrMagnitude < 1e-4f)
            {
                problems.Add($"{label} swing axis must not be parallel to the twist axis");
            }

            if (!InRange(_lowTwist, -MaxTwist, MaxTwist) || !InRange(_highTwist, -MaxTwist, MaxTwist))
            {
                problems.Add($"{label} twist limits must be within [-{MaxTwist}, {MaxTwist}] degrees");
            }
            else if (_lowTwist > _highTwist)
            {
                problems.Add($"{label} low twist is greater than high twist");
            }

            if (!InRange(_swing1, 0f, MaxSwing) || !InRange(_swing2, 0f, MaxSwing))
            {
                problems.Add($"{label} swing limits must be within [0, {MaxSwing}] degrees");
            }
        }

        private static bool InRange(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        private static bool IsFinite(Vector3 value)
        {
            return RagdollCatalog.IsFinite(value.x) && RagdollCatalog.IsFinite(value.y) && RagdollCatalog.IsFinite(value.z);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }

    public sealed class RagdollCatalogException : Exception
    {
        public RagdollCatalogException(string message)
            : base(message)
        {
        }
    }
}
