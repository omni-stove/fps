using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour
    {
        public const string LogTag = "[CameraShake]";

        public const string SourceFire = "fire";
        public const string SourceExplosion = "explosion";
        public const string SourceDamage = "damage";
        public const string SourceLanding = "landing";

        // 設定画面で選んだ倍率は、プレイヤーが作り直されても (リスポーン・シーンの切り替え) 保たれるよう本体ごとではなく全体で持つ。
        private static float _globalMultiplier = 1f;

        // 同じ画面に揺れが複数あっても同じ揺れ方にならないよう、作るたびに種を変える。
        private static int _nextSeed = 1;

        [SerializeField, Tooltip("揺れの強さ。空なら Resources の CameraShakeSettings を探す")]
        private CameraShakeSettings _settings;

        private readonly CameraShakeModel _model = new CameraShakeModel(_nextSeed++);
        private ViewRig _rig;
        private AimDownSights _ads;
        private bool _settingsResolved;
        private bool _subscribed;
        private bool _shakeApplied;
        private CameraShakeSample _lastSample = CameraShakeSample.Zero;

        public static float GlobalMultiplier
        {
            get => _globalMultiplier;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "multiplier must be a non-negative finite number");
                }

                if (Mathf.Approximately(_globalMultiplier, value))
                {
                    return;
                }

                _globalMultiplier = value;
                Debug.Log(FormatMultiplierLog(value));
            }
        }

        public CameraShakeSettings Settings
        {
            get
            {
                if (_settings == null && !_settingsResolved)
                {
                    _settingsResolved = true;
                    _settings = Accept(CameraShakeSettings.Load(), "Resources/" + CameraShakeSettings.ResourcePath);
                }

                return _settings;
            }
            set
            {
                _settingsResolved = true;
                _settings = value != null ? Accept(value, value.name) : null;
            }
        }

        public ViewRig Rig => _rig;

        public float Trauma => _model.Trauma;

        public CameraShakeSample LastSample => _lastSample;

        public Func<WeaponSpec> WeaponSource { get; set; }

        public bool IsLocalView => _rig != null && _rig.enabled && _rig.gameObject.activeInHierarchy && _rig.IsLocalView;

        private void Awake()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            if (_rig == null)
            {
                var rig = GetComponent<ViewRig>();
                if (rig == null)
                {
                    Debug.LogError($"{LogTag} '{name}' has no {nameof(ViewRig)}; the screen does not shake", this);
                    enabled = false;
                    return;
                }

                Bind(rig);
            }
        }

        private void OnEnable()
        {
            if (_rig != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetShake();
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        public void Bind(ViewRig rig)
        {
            if (rig == null)
            {
                throw new ArgumentNullException(nameof(rig));
            }

            if (rig.gameObject != gameObject)
            {
                throw new ArgumentException($"view rig '{rig.name}' must be on the same GameObject as the camera shake '{name}'", nameof(rig));
            }

            _rig = rig;
        }

        public float AddTrauma(float amount, string source, string detail)
        {
            var added = _model.AddTrauma(amount);
            if (amount > 0f)
            {
                Debug.Log(FormatTraumaLog(source, added, _model.Trauma, detail), this);
            }

            return added;
        }

        public void Tick(float deltaSeconds)
        {
            var settings = Settings;
            if (settings != null)
            {
                settings.ApplyTo(_model);
            }

            _model.Tick(deltaSeconds);
            _lastSample = _model.Sample(_globalMultiplier);
            ApplySample(_lastSample);
        }

        public void ResetShake()
        {
            _model.Reset();
            _lastSample = CameraShakeSample.Zero;
            ApplySample(_lastSample);
        }

        public static string FormatTraumaLog(string source, float added, float total, string detail)
        {
            var line = LogTag
                + " trauma +" + added.ToString("0.000", CultureInfo.InvariantCulture)
                + " total=" + total.ToString("0.000", CultureInfo.InvariantCulture)
                + " source=" + (string.IsNullOrEmpty(source) ? "unknown" : source);
            return string.IsNullOrEmpty(detail) ? line : line + " " + detail;
        }

        public static string FormatMultiplierLog(float multiplier)
        {
            return LogTag + " multiplier=" + multiplier.ToString("0.00", CultureInfo.InvariantCulture);
        }

        internal void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;
            CombatEvents.ShotFired += HandleShotFired;
            CombatEvents.Exploded += HandleExploded;
            CombatEvents.DamageTaken += HandleDamageTaken;
            CombatEvents.Landed += HandleLanded;
        }

        internal void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            CombatEvents.ShotFired -= HandleShotFired;
            CombatEvents.Exploded -= HandleExploded;
            CombatEvents.DamageTaken -= HandleDamageTaken;
            CombatEvents.Landed -= HandleLanded;
        }

        internal void HandleShotFired(ShotFiredInfo shot)
        {
            if (!IsLocalView || !IsOwnBody(shot.Shooter) || !TryGetSettings(out var settings))
            {
                return;
            }

            var category = ResolveCategory(shot.WeaponId);
            AddTrauma(
                settings.FireTrauma(category),
                SourceFire,
                "weapon=" + shot.WeaponId + " category=" + (category.HasValue ? category.Value.ToString() : "unknown"));
        }

        internal void HandleExploded(ExplosionInfo explosion)
        {
            if (!IsLocalView || !TryGetSettings(out var settings))
            {
                return;
            }

            var distance = Vector3.Distance(ViewOrigin(), explosion.Center);
            AddTrauma(
                settings.ExplosionTraumaAt(distance),
                SourceExplosion,
                "weapon=" + explosion.WeaponId + " distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture));
        }

        // DamageTaken は撃たれた本人の画面にだけ届き、どの本体宛てかは載らないので、この画面の視点かどうかだけで決める。
        internal void HandleDamageTaken(DamageTakenInfo damage)
        {
            if (!IsLocalView || !TryGetSettings(out var settings))
            {
                return;
            }

            AddTrauma(
                settings.DamageTrauma(damage.Amount),
                SourceDamage,
                "amount=" + damage.Amount.ToString(CultureInfo.InvariantCulture));
        }

        internal void HandleLanded(LandingInfo landing)
        {
            if (!IsLocalView || !IsOwnBody(landing.Body) || !TryGetSettings(out var settings))
            {
                return;
            }

            AddTrauma(
                settings.LandingTrauma(landing.FallSpeed),
                SourceLanding,
                "speed=" + landing.FallSpeed.ToString("0.00", CultureInfo.InvariantCulture));
        }

        // オフラインの WeaponController は撃った者を照準のルートで渡すので、本体と同じか、本体を含む側か、本体の下のどれでも自分とみなす。
        private bool IsOwnBody(GameObject other)
        {
            if (other == null)
            {
                return false;
            }

            var otherTransform = other.transform;
            return otherTransform == transform || transform.IsChildOf(otherTransform) || otherTransform.IsChildOf(transform);
        }

        private WeaponCategory? ResolveCategory(string weaponId)
        {
            var source = WeaponSource;
            if (source == null)
            {
                if (_ads == null)
                {
                    _ads = GetComponent<AimDownSights>();
                }

                source = _ads != null ? _ads.WeaponSource : null;
            }

            var spec = source?.Invoke();
            if (spec == null)
            {
                return null;
            }

            // 持ち替えの直後に前の武器の弾の知らせが届くことがあるので、Id が違えば種別は分からないものとして既定の強さにする。
            return string.IsNullOrEmpty(weaponId) || string.Equals(spec.Id, weaponId, StringComparison.Ordinal) ? spec.Category : (WeaponCategory?)null;
        }

        private Vector3 ViewOrigin()
        {
            if (_rig != null && _rig.Camera != null)
            {
                return _rig.Camera.transform.position;
            }

            return transform.position;
        }

        private bool TryGetSettings(out CameraShakeSettings settings)
        {
            settings = Settings;
            return settings != null;
        }

        private void ApplySample(CameraShakeSample sample)
        {
            var root = _rig != null ? _rig.ShakeRoot : null;
            if (root == null)
            {
                return;
            }

            if (sample.IsZero && !_shakeApplied)
            {
                return;
            }

            root.localPosition = sample.Offset.ToUnity();
            root.localRotation = Quaternion.Euler(sample.Angles.ToUnity());
            _shakeApplied = !sample.IsZero;
        }

        private CameraShakeSettings Accept(CameraShakeSettings settings, string source)
        {
            if (settings == null)
            {
                Debug.LogWarning($"{LogTag} {source} was not found; run Fps.Editor.PresentationAssetSteps.BuildAll. The screen does not shake", this);
                return null;
            }

            try
            {
                settings.Validate();
            }
            catch (CameraShakeSettingsException e)
            {
                Debug.LogError($"{LogTag} {e.Message}; the screen does not shake", this);
                return null;
            }

            return settings;
        }
    }
}
