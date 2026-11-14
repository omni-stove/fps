using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [CreateAssetMenu(fileName = "CameraShakeSettings", menuName = "Fps/Camera Shake Settings")]
    public sealed class CameraShakeSettings : ScriptableObject
    {
        public const string ResourcePath = PresentationCatalog.ResourceFolder + "/CameraShakeSettings";

        public const float DefaultDecayPerSecond = CameraShakeModel.DefaultDecayPerSecond;
        public const float DefaultFrequency = CameraShakeModel.DefaultFrequency;
        public const float DefaultFireTrauma = 0.08f;
        public const float DefaultExplosionTrauma = 1f;
        public const float DefaultExplosionRadius = 25f;
        public const float DefaultTraumaPerDamage = 0.012f;
        public const float DefaultMaxDamageTrauma = 0.6f;

        // ジャンプの着地 (約 5 m/s) では揺らさず、段差から飛び降りたときから揺らす。
        public const float DefaultLandingMinSpeed = 7f;

        // 落下ダメージで倒れる速さ (MotorSpec.DefaultLethalFallSpeed) より手前で頭打ちにし、大きな落下はどれも同じ強さで揺らす。
        public const float DefaultLandingFullSpeed = 20f;
        public const float DefaultLandingMaxTrauma = 0.6f;

        public static readonly Vector3 DefaultMaxAngles = new Vector3(4f, 4f, 3f);
        public static readonly Vector3 DefaultMaxOffset = new Vector3(0.04f, 0.04f, 0.02f);

        [SerializeField, Min(0f), Tooltip("トラウマが 1 秒に減る量")]
        private float _decayPerSecond = DefaultDecayPerSecond;

        [SerializeField, Min(0f), Tooltip("揺れの細かさ (Hz)")]
        private float _frequency = DefaultFrequency;

        [SerializeField, Tooltip("トラウマ 1 での回転の揺れの最大 (度)。X が pitch、Y が yaw、Z が roll")]
        private Vector3 _maxAngles = DefaultMaxAngles;

        [SerializeField, Tooltip("トラウマ 1 での位置の揺れの最大 (m)")]
        private Vector3 _maxOffset = DefaultMaxOffset;

        [SerializeField, Tooltip("自分が 1 発撃つたびに足すトラウマ (武器の種別ごと)")]
        private List<CameraShakeFireTrauma> _fireByCategory = DefaultFireByCategory();

        [SerializeField, Range(0f, 1f), Tooltip("種別が表に無い・分からない武器で 1 発撃つたびに足すトラウマ")]
        private float _defaultFireTrauma = DefaultFireTrauma;

        [SerializeField, Range(0f, 1f), Tooltip("爆発の中心で足すトラウマ。離れるほど直線で減る")]
        private float _explosionTrauma = DefaultExplosionTrauma;

        [SerializeField, Min(0.01f), Tooltip("この距離 (m) より遠い爆発では揺らさない")]
        private float _explosionRadius = DefaultExplosionRadius;

        [SerializeField, Min(0f), Tooltip("被弾のダメージ 1 あたりに足すトラウマ")]
        private float _traumaPerDamage = DefaultTraumaPerDamage;

        [SerializeField, Range(0f, 1f), Tooltip("1 回の被弾で足すトラウマの上限")]
        private float _maxDamageTrauma = DefaultMaxDamageTrauma;

        [SerializeField, Min(0f), Tooltip("この落下の速さ (m/s) 以下の着地では揺らさない")]
        private float _landingMinSpeed = DefaultLandingMinSpeed;

        [SerializeField, Min(0f), Tooltip("この落下の速さ (m/s) 以上の着地で最大の強さになる")]
        private float _landingFullSpeed = DefaultLandingFullSpeed;

        [SerializeField, Range(0f, 1f), Tooltip("着地で足すトラウマの最大")]
        private float _landingMaxTrauma = DefaultLandingMaxTrauma;

        public float DecayPerSecond
        {
            get => _decayPerSecond;
            set => _decayPerSecond = value;
        }

        public float Frequency
        {
            get => _frequency;
            set => _frequency = value;
        }

        public Vector3 MaxAngles
        {
            get => _maxAngles;
            set => _maxAngles = value;
        }

        public Vector3 MaxOffset
        {
            get => _maxOffset;
            set => _maxOffset = value;
        }

        public IReadOnlyList<CameraShakeFireTrauma> FireByCategory => _fireByCategory ??= new List<CameraShakeFireTrauma>();

        public float DefaultFire
        {
            get => _defaultFireTrauma;
            set => _defaultFireTrauma = value;
        }

        public float ExplosionTrauma
        {
            get => _explosionTrauma;
            set => _explosionTrauma = value;
        }

        public float ExplosionRadius
        {
            get => _explosionRadius;
            set => _explosionRadius = value;
        }

        public float TraumaPerDamage
        {
            get => _traumaPerDamage;
            set => _traumaPerDamage = value;
        }

        public float MaxDamageTrauma
        {
            get => _maxDamageTrauma;
            set => _maxDamageTrauma = value;
        }

        public float LandingMinSpeed
        {
            get => _landingMinSpeed;
            set => _landingMinSpeed = value;
        }

        public float LandingFullSpeed
        {
            get => _landingFullSpeed;
            set => _landingFullSpeed = value;
        }

        public float LandingMaxTrauma
        {
            get => _landingMaxTrauma;
            set => _landingMaxTrauma = value;
        }

        public static CameraShakeSettings Load()
        {
            return Resources.Load<CameraShakeSettings>(ResourcePath);
        }

        // 連射の武器は 1 発ごとに足すので、1 秒に撃つ数を掛けても DefaultDecayPerSecond を大きく超えない値にして、撃ち続けても揺れが張り付かないようにする。
        public static List<CameraShakeFireTrauma> DefaultFireByCategory()
        {
            return new List<CameraShakeFireTrauma>
            {
                new CameraShakeFireTrauma(WeaponCategory.AssaultRifle, 0.07f),
                new CameraShakeFireTrauma(WeaponCategory.SubmachineGun, 0.05f),
                new CameraShakeFireTrauma(WeaponCategory.Shotgun, 0.3f),
                new CameraShakeFireTrauma(WeaponCategory.MarksmanRifle, 0.18f),
                new CameraShakeFireTrauma(WeaponCategory.SniperRifle, 0.35f),
                new CameraShakeFireTrauma(WeaponCategory.LightMachineGun, 0.08f),
                new CameraShakeFireTrauma(WeaponCategory.Handgun, 0.1f),
                new CameraShakeFireTrauma(WeaponCategory.Launcher, 0.45f),
            };
        }

        public void ResetToDefaults()
        {
            _decayPerSecond = DefaultDecayPerSecond;
            _frequency = DefaultFrequency;
            _maxAngles = DefaultMaxAngles;
            _maxOffset = DefaultMaxOffset;
            _fireByCategory = DefaultFireByCategory();
            _defaultFireTrauma = DefaultFireTrauma;
            _explosionTrauma = DefaultExplosionTrauma;
            _explosionRadius = DefaultExplosionRadius;
            _traumaPerDamage = DefaultTraumaPerDamage;
            _maxDamageTrauma = DefaultMaxDamageTrauma;
            _landingMinSpeed = DefaultLandingMinSpeed;
            _landingFullSpeed = DefaultLandingFullSpeed;
            _landingMaxTrauma = DefaultLandingMaxTrauma;
        }

        public void SetFireByCategory(IEnumerable<CameraShakeFireTrauma> entries)
        {
            _fireByCategory = entries == null ? new List<CameraShakeFireTrauma>() : new List<CameraShakeFireTrauma>(entries);
        }

        public float FireTrauma(WeaponCategory? category)
        {
            if (category.HasValue)
            {
                foreach (var entry in FireByCategory)
                {
                    if (entry != null && entry.Category == category.Value)
                    {
                        return entry.Trauma;
                    }
                }
            }

            return _defaultFireTrauma;
        }

        public float ExplosionTraumaAt(float distance)
        {
            return CameraShakeTrauma.ForExplosion(distance, _explosionTrauma, _explosionRadius);
        }

        public float DamageTrauma(int amount)
        {
            return CameraShakeTrauma.ForDamage(amount, _traumaPerDamage, _maxDamageTrauma);
        }

        public float LandingTrauma(float fallSpeed)
        {
            return CameraShakeTrauma.ForLanding(fallSpeed, _landingMinSpeed, _landingFullSpeed, _landingMaxTrauma);
        }

        public void ApplyTo(CameraShakeModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            model.DecayPerSecond = _decayPerSecond;
            model.Frequency = _frequency;
            model.MaxAngles = _maxAngles.ToNumerics();
            model.MaxOffset = _maxOffset.ToNumerics();
        }

        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed camera shake settings)" : name;
            var problems = new List<string>();

            RequireNonNegative(problems, _decayPerSecond, "decay per second");
            RequireNonNegative(problems, _frequency, "frequency");
            RequireNonNegative(problems, _maxAngles, "max angles");
            RequireNonNegative(problems, _maxOffset, "max offset");
            RequireTrauma(problems, _defaultFireTrauma, "default fire trauma");
            RequireTrauma(problems, _explosionTrauma, "explosion trauma");
            RequirePositive(problems, _explosionRadius, "explosion radius");
            RequireNonNegative(problems, _traumaPerDamage, "trauma per damage");
            RequireTrauma(problems, _maxDamageTrauma, "max damage trauma");
            RequireNonNegative(problems, _landingMinSpeed, "landing min speed");
            RequireTrauma(problems, _landingMaxTrauma, "landing max trauma");
            if (!(_landingFullSpeed > _landingMinSpeed) || float.IsInfinity(_landingFullSpeed))
            {
                problems.Add("landing full speed " + Format(_landingFullSpeed) + " must be finite and greater than landing min speed " + Format(_landingMinSpeed));
            }

            var seen = new HashSet<WeaponCategory>();
            for (var i = 0; i < FireByCategory.Count; i++)
            {
                var entry = FireByCategory[i];
                if (entry == null)
                {
                    problems.Add($"fire trauma #{i} is empty");
                    continue;
                }

                if (!Enum.IsDefined(typeof(WeaponCategory), entry.Category))
                {
                    problems.Add($"fire trauma #{i} has an unknown weapon category {(int)entry.Category}");
                }
                else if (!seen.Add(entry.Category))
                {
                    problems.Add($"fire trauma #{i} repeats the weapon category {entry.Category}");
                }

                RequireTrauma(problems, entry.Trauma, $"fire trauma #{i} ({entry.Category})");
            }

            if (problems.Count > 0)
            {
                throw new CameraShakeSettingsException($"camera shake settings '{source}': {string.Join("; ", problems)}");
            }
        }

        private static void RequireNonNegative(List<string> problems, float value, string what)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                problems.Add($"{what} {Format(value)} must be a non-negative finite number");
            }
        }

        private static void RequireNonNegative(List<string> problems, Vector3 value, string what)
        {
            RequireNonNegative(problems, value.x, what + ".x");
            RequireNonNegative(problems, value.y, what + ".y");
            RequireNonNegative(problems, value.z, what + ".z");
        }

        private static void RequirePositive(List<string> problems, float value, string what)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                problems.Add($"{what} {Format(value)} must be a positive finite number");
            }
        }

        private static void RequireTrauma(List<string> problems, float value, string what)
        {
            if (!(value >= 0f && value <= 1f))
            {
                problems.Add($"{what} {Format(value)} must be between 0 and 1");
            }
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    [Serializable]
    public sealed class CameraShakeFireTrauma
    {
        [SerializeField, Tooltip("武器の種別")]
        private WeaponCategory _category;

        [SerializeField, Range(0f, 1f), Tooltip("1 発撃つたびに足すトラウマ")]
        private float _trauma;

        public CameraShakeFireTrauma()
        {
        }

        public CameraShakeFireTrauma(WeaponCategory category, float trauma)
        {
            _category = category;
            _trauma = trauma;
        }

        public WeaponCategory Category => _category;

        public float Trauma => _trauma;
    }

    public sealed class CameraShakeSettingsException : Exception
    {
        public CameraShakeSettingsException(string message)
            : base(message)
        {
        }
    }
}
