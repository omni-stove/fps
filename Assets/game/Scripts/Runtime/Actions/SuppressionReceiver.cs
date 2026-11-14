using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // プレイヤーと敵で影響は違うが、ニアミスの受け取りと度合いの計算・ログは同じなので、受け皿だけを共通の部品にする
    // (影響は PlayerSuppression と EnemySuppression が度合いを読んで決める)。
    [DisallowMultipleComponent]
    public sealed class SuppressionReceiver : MonoBehaviour, INearMissTarget
    {
        [SerializeField, Tooltip("制圧されるか。Off なら弾が近くを通っても何もしない")]
        private Suppression _rule = Suppression.Off;

        [SerializeField, Tooltip("ログに出す識別子。空なら Damageable の Id (無ければ GameObject の名前)")]
        private string _id = string.Empty;

        [Header("Tuning")]
        [SerializeField, Min(0.01f), Tooltip("弾の線がこの距離 (m) より近くを通ったらニアミス")]
        private float _nearMissRadius = SuppressionSpec.DefaultNearMissRadius;

        [SerializeField, Min(0.001f), Tooltip("体の中心をかすめたニアミス 1 回でたまる度合い")]
        private float _buildPerNearMiss = SuppressionSpec.DefaultBuildPerNearMiss;

        [SerializeField, Min(0f), Tooltip("最後のニアミスから度合いが抜け始めるまでの秒数")]
        private float _decayDelaySeconds = SuppressionSpec.DefaultDecayDelaySeconds;

        [SerializeField, Min(0.001f), Tooltip("抜け始めてから 1 秒あたりに減る度合い")]
        private float _decayPerSecond = SuppressionSpec.DefaultDecayPerSecond;

        [SerializeField, Range(0.01f, 1f), Tooltip("度合いがこれ以上になったら制圧される")]
        private float _suppressedLevel = SuppressionSpec.DefaultSuppressedLevel;

        [SerializeField, Range(0f, 1f), Tooltip("制圧されている間、度合いがこれを下回ったら解ける。制圧される度合いより小さくする")]
        private float _releasedLevel = SuppressionSpec.DefaultReleasedLevel;

        [SerializeField, Min(0f), Tooltip("度合い 1 のときに弾の散らばりの半角に足す角度 (度)")]
        private float _maxSpreadDegrees = SuppressionSpec.DefaultMaxSpreadDegrees;

        [SerializeField, Range(0f, 1f), Tooltip("度合い 1 のときに照準の広がりに足す量")]
        private float _maxCrosshairSpread = SuppressionSpec.DefaultMaxCrosshairSpread;

        [SerializeField, Range(0f, 1f), Tooltip("度合い 1 のときの画面の端の暗さ")]
        private float _maxVignetteAlpha = SuppressionSpec.DefaultMaxVignetteAlpha;

        private SuppressionMeter _meter;
        private Damageable _damageable;
        private bool _registered;
        private bool _wasSuppressed;
        private bool _hadLevel;

        public event Action<SuppressionReceiver, HitscanNearMiss> NearMissed;

        public event Action<SuppressionReceiver, bool> SuppressedChanged;

        public Suppression Rule => _rule;

        // isActiveAndEnabled は OnEnable が走るまで false のままなので、OnEnable の走らない Edit Mode でも同じに読めるよう分けて見る。
        public bool IsActive => _rule == Suppression.On && enabled && gameObject.activeInHierarchy;

        // Awake より先に読まれても (Edit Mode や、無効な GameObject に付けた直後) 使えるよう、初めて読んだときに作る。
        public SuppressionMeter Meter => EnsureMeter();

        public SuppressionSpec Spec => Meter.Spec;

        public float Level => Meter.Level;

        public bool IsSuppressed => Meter.IsSuppressed;

        public string Id
        {
            get
            {
                if (!string.IsNullOrEmpty(_id))
                {
                    return _id;
                }

                var damageable = _damageable != null ? _damageable : GetComponent<Damageable>();
                return damageable != null ? damageable.Id : name;
            }

            set => _id = value ?? string.Empty;
        }

        Transform INearMissTarget.Root => transform;

        float INearMissTarget.NearMissRadius => IsActive ? Spec.NearMissRadius : 0f;

        /// <exception cref="ArgumentException">Inspector の調整値が不正。</exception>
        public SuppressionSpec CreateSpec()
        {
            return new SuppressionSpec(
                _nearMissRadius,
                _buildPerNearMiss,
                _decayDelaySeconds,
                _decayPerSecond,
                _suppressedLevel,
                _releasedLevel,
                _maxSpreadDegrees,
                _maxCrosshairSpread,
                _maxVignetteAlpha);
        }

        public void Configure(Suppression rule)
        {
            Configure(rule, Spec);
        }

        // 調整値が今と違えば度合いを 0 から数え直す (前の値でためた度合いを新しい閾値で読ませない)。
        /// <exception cref="ArgumentNullException">spec が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">rule が <see cref="Suppression"/> の値でない。</exception>
        public void Configure(Suppression rule, SuppressionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!Enum.IsDefined(typeof(Suppression), rule))
            {
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "rule must be a Suppression value");
            }

            // 既定の spec は「調整の指定なし」なので、受け皿に設定済みの Inspector の値を潰さない。
            if (!ReferenceEquals(spec, SuppressionSpec.Default) && !ReferenceEquals(spec, EnsureMeter().Spec))
            {
                CopyToFields(spec);
                _meter = new SuppressionMeter(spec);
            }

            EnsureMeter();
            _rule = rule;
            if (rule != Suppression.On)
            {
                _meter.Reset();
            }

            ReportChanges();
            SyncRegistration();
            Debug.Log(SuppressionLog.Configured(Id, rule), this);
        }

        public void ResetLevel()
        {
            Meter.Reset();
            ReportChanges();
        }

        public void Tick(float deltaSeconds)
        {
            if (_rule != Suppression.On)
            {
                return;
            }

            Meter.Tick(deltaSeconds);
            ReportChanges();
        }

        bool INearMissTarget.TryGetNearMissCenter(out Vector3 center)
        {
            var damageable = _damageable != null ? _damageable : GetComponent<Damageable>();
            if (damageable != null && damageable.IsDead)
            {
                center = default;
                return false;
            }

            center = damageable != null ? damageable.AimPoint : transform.position;
            return true;
        }

        void INearMissTarget.OnNearMiss(HitscanNearMiss nearMiss)
        {
            ReceiveNearMiss(nearMiss);
        }

        public bool ReceiveNearMiss(HitscanNearMiss nearMiss)
        {
            if (!IsActive || !Meter.AddNearMiss(nearMiss.Distance))
            {
                return false;
            }

            Debug.Log(SuppressionLog.NearMissed(Id, nearMiss.Distance, Meter), this);
            ReportChanges();
            NearMissed?.Invoke(this, nearMiss);
            return true;
        }

        private void Awake()
        {
            _damageable = GetComponent<Damageable>();
            EnsureMeter();
        }

        private void OnEnable()
        {
            if (_damageable == null)
            {
                _damageable = GetComponent<Damageable>();
            }

            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
                _damageable.Died += OnDied;
            }

            SyncRegistration();
        }

        private void OnDisable()
        {
            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
            }

            // 消されるときは enabled も activeInHierarchy も true のまま呼ばれるので、IsActive を見ずに外す。
            if (_registered)
            {
                Hitscan.UnregisterNearMissTarget(this);
                _registered = false;
            }

            if (_meter != null)
            {
                _meter.Reset();
                ReportChanges();
            }
        }

        private void OnDestroy()
        {
            if (_registered)
            {
                Hitscan.UnregisterNearMissTarget(this);
                _registered = false;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDied(Damageable self)
        {
            ResetLevel();
        }

        private SuppressionMeter EnsureMeter()
        {
            if (_meter != null)
            {
                return _meter;
            }

            SuppressionSpec spec;
            try
            {
                spec = CreateSpec();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{SuppressionLog.LogTag} '{name}' の調整値が不正なため既定値を使います: {e.Message}", this);
                spec = SuppressionSpec.Default;
            }

            _meter = new SuppressionMeter(spec);
            return _meter;
        }

        private void SyncRegistration()
        {
            var wanted = IsActive;
            if (wanted == _registered)
            {
                return;
            }

            if (wanted)
            {
                Hitscan.RegisterNearMissTarget(this);
            }
            else
            {
                Hitscan.UnregisterNearMissTarget(this);
            }

            _registered = wanted;
        }

        // 毎フレーム出すと読めないので、制圧の入り・抜けと、度合いが抜け切ったときだけ知らせる。
        private void ReportChanges()
        {
            var meter = EnsureMeter();
            var suppressed = meter.IsSuppressed;
            if (suppressed != _wasSuppressed)
            {
                _wasSuppressed = suppressed;
                Debug.Log(SuppressionLog.SuppressedChanged(Id, suppressed, meter.Level), this);
                SuppressedChanged?.Invoke(this, suppressed);
            }

            var hasLevel = meter.Level > 0f;
            if (_hadLevel && !hasLevel)
            {
                Debug.Log(SuppressionLog.Cleared(Id), this);
            }

            _hadLevel = hasLevel;
        }

        // Inspector に今効いている値を見せ、CreateSpec で作り直しても同じ値になるよう書き戻す。
        private void CopyToFields(SuppressionSpec spec)
        {
            _nearMissRadius = spec.NearMissRadius;
            _buildPerNearMiss = spec.BuildPerNearMiss;
            _decayDelaySeconds = spec.DecayDelaySeconds;
            _decayPerSecond = spec.DecayPerSecond;
            _suppressedLevel = spec.SuppressedLevel;
            _releasedLevel = spec.ReleasedLevel;
            _maxSpreadDegrees = spec.MaxSpreadDegrees;
            _maxCrosshairSpread = spec.MaxCrosshairSpread;
            _maxVignetteAlpha = spec.MaxVignetteAlpha;
        }
    }
}
