using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // EnemyAgent が判断を進める前に見え方の材料を渡す必要があるので、EnemyAgent より先に Update させる。
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    public sealed class EnemyStealth : MonoBehaviour
    {
        public const string LogTag = "[Stealth]";

        public const float DefaultIndicatorHeightAboveHead = 0.5f;

        // 当たり判定が無いときに、足元から頭までとみなす高さ (m)。
        public const float FallbackHeadHeight = 1.8f;

        private static readonly List<EnemyStealth> _active = new List<EnemyStealth>();

        [Header("Awareness")]
        [SerializeField, Range(1f, 360f), Tooltip("視野の全角 (度)。外ではゲージがたまらない")]
        private float _fieldOfViewDegrees = AwarenessSpec.DefaultFieldOfViewDegrees;

        [SerializeField, Min(0.01f), Tooltip("すべての係数が 1 のときに 1 秒でたまる量 (満ちて 1)")]
        private float _gainPerSecond = AwarenessSpec.DefaultGainPerSecond;

        [SerializeField, Min(0f), Tooltip("これより近ければ距離でたまり方を落とさない (m)")]
        private float _fullGainDistance = AwarenessSpec.DefaultFullGainDistance;

        [SerializeField, Range(0f, 1f), Tooltip("視認距離の端でのたまり方の係数")]
        private float _farGainFactor = AwarenessSpec.DefaultFarGainFactor;

        [SerializeField, Range(0f, 1f), Tooltip("視野の端でのたまり方の係数")]
        private float _peripheralGainFactor = AwarenessSpec.DefaultPeripheralGainFactor;

        [SerializeField, Range(0f, 1f), Tooltip("相手がしゃがんでいるときの係数")]
        private float _crouchGainFactor = AwarenessSpec.DefaultCrouchGainFactor;

        [SerializeField, Range(0f, 1f), Tooltip("相手が伏せているときの係数")]
        private float _proneGainFactor = AwarenessSpec.DefaultProneGainFactor;

        [SerializeField, Range(0f, 1f), Tooltip("相手が止まっているときの係数")]
        private float _stillGainFactor = AwarenessSpec.DefaultStillGainFactor;

        [SerializeField, Min(0.01f), Tooltip("相手がこの速さ (m/s) 以上で動くと動きの係数が 1")]
        private float _fullMotionSpeed = AwarenessSpec.DefaultFullMotionSpeed;

        [SerializeField, Min(0f), Tooltip("見えなくなってから減り始めるまでの秒数")]
        private float _decayDelaySeconds = AwarenessSpec.DefaultDecayDelaySeconds;

        [SerializeField, Min(0f), Tooltip("見えていない間に 1 秒で減る量")]
        private float _decayPerSecond = AwarenessSpec.DefaultDecayPerSecond;

        [SerializeField, Range(0.01f, 0.99f), Tooltip("これ以上で気づき")]
        private float _noticeThreshold = AwarenessSpec.DefaultNoticeThreshold;

        [SerializeField, Range(0.01f, 0.99f), Tooltip("これ以上で警戒。満ちると交戦")]
        private float _alertThreshold = AwarenessSpec.DefaultAlertThreshold;

        [SerializeField, Range(0.02f, 1f), Tooltip("交戦に入った後、これを下回るまで交戦のまま。警戒の境目より上")]
        private float _engagedHoldThreshold = AwarenessSpec.DefaultEngagedHoldThreshold;

        [SerializeField, Min(0f), Tooltip("交戦に入ったことを仲間に伝える範囲 (m)。0 なら伝えない")]
        private float _alarmRadius = AwarenessSpec.DefaultAlarmRadius;

        [Header("Indicator")]
        [SerializeField, Tooltip("頭上に出す印。空なら AwarenessIndicatorCatalog の印、それも無ければ仮の印")]
        private GameObject _indicatorPrefab;

        [SerializeField, Min(0f)]
        private float _indicatorHeightAboveHead = DefaultIndicatorHeightAboveHead;

        private EnemyAgent _agent;
        private Damageable _self;
        private SideMember _side;
        private StealthAwareness _awareness;
        private Damageable _observedTarget;
        private StealthTarget _observedStealth;
        private float _nextStealthLookup;
        private IAwarenessIndicator _indicator;
        private GameObject _indicatorObject;
        private bool _warnedIndicator;

        public static IReadOnlyList<EnemyStealth> Active => _active;

        public StealthAwareness Awareness => _awareness;

        public AwarenessStage Stage => _awareness?.Stage ?? AwarenessStage.Unaware;

        public float Level => _awareness?.Level ?? 0f;

        public Stealth Rule => _awareness?.Rule ?? Stealth.Off;

        public IAwarenessIndicator Indicator => _indicator;

        public GameObject IndicatorPrefab
        {
            get => _indicatorPrefab;
            set => _indicatorPrefab = value;
        }

        public string Id => _agent != null ? _agent.Id : name;

        public AwarenessSpec CreateSpec()
        {
            return new AwarenessSpec(
                _fieldOfViewDegrees,
                _gainPerSecond,
                _fullGainDistance,
                _farGainFactor,
                _peripheralGainFactor,
                _crouchGainFactor,
                _proneGainFactor,
                _stillGainFactor,
                _fullMotionSpeed,
                _decayDelaySeconds,
                _decayPerSecond,
                _noticeThreshold,
                _alertThreshold,
                _alarmRadius,
                _engagedHoldThreshold);
        }

        // 初期化前に呼ぶ。EnemyAgent.ApplySpec と同じく、作った後の StealthAwareness の設定は変えない。
        public void ApplySpec(AwarenessSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_awareness != null)
            {
                throw new InvalidOperationException($"EnemyStealth '{name}' is already initialized; apply the spec before activating it");
            }

            _fieldOfViewDegrees = spec.FieldOfViewDegrees;
            _gainPerSecond = spec.GainPerSecond;
            _fullGainDistance = spec.FullGainDistance;
            _farGainFactor = spec.FarGainFactor;
            _peripheralGainFactor = spec.PeripheralGainFactor;
            _crouchGainFactor = spec.CrouchGainFactor;
            _proneGainFactor = spec.ProneGainFactor;
            _stillGainFactor = spec.StillGainFactor;
            _fullMotionSpeed = spec.FullMotionSpeed;
            _decayDelaySeconds = spec.DecayDelaySeconds;
            _decayPerSecond = spec.DecayPerSecond;
            _noticeThreshold = spec.NoticeThreshold;
            _alertThreshold = spec.AlertThreshold;
            _alarmRadius = spec.AlarmRadius;
            _engagedHoldThreshold = spec.EngagedHoldThreshold;
        }

        // EnemyAgent の初期化 (Awake) の後でないと Brain が無いので、Start でつなぐ。テストからは直接呼ぶ。
        public bool TryInitialize()
        {
            if (_awareness != null)
            {
                return true;
            }

            _agent = GetComponent<EnemyAgent>();
            if (_agent == null || !_agent.TryInitialize())
            {
                Debug.LogError($"{LogTag} '{name}' には初期化できる EnemyAgent が必要です。", this);
                return false;
            }

            AwarenessSpec spec;
            try
            {
                spec = CreateSpec();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} id={Id} の設定が不正です: {e.Message}", this);
                return false;
            }

            _self = GetComponent<Damageable>();
            _side = GetComponent<SideMember>();
            _awareness = new StealthAwareness(spec);
            _awareness.StageChanged += OnStageChanged;
            _agent.Brain.AddPerceptionContributor(_awareness);
            _agent.Brain.AddBehavior(_awareness);
            if (_self != null)
            {
                _self.Died += OnDied;
            }

            return true;
        }

        public void ResetAwareness()
        {
            _awareness?.Reset();
        }

        // 伝わった場所は物音として渡す。EnemyBrain は物音の位置を調べに行くので、見えていない相手のいる方へ向かわせられる。
        public bool ReceiveAlarm(EnemyStealth from, Vector3 position)
        {
            if (_awareness == null || (_agent.Brain != null && _agent.Brain.IsDead) || !_awareness.Alarm(position.ToNumerics()))
            {
                return false;
            }

            _agent.Alert(position);
            Debug.Log(FormatAlarmLog(Id, from != null ? from.Id : "unknown", position), this);
            return true;
        }

        public bool IsAllyOf(EnemyStealth other)
        {
            if (other == null || other == this)
            {
                return false;
            }

            var mine = _side != null ? _side : GetComponent<SideMember>();
            var theirs = other._side != null ? other._side : other.GetComponent<SideMember>();
            if (mine == null || theirs == null)
            {
                return mine == null && theirs == null;
            }

            return mine.Side == theirs.Side;
        }

        public static string FormatStageLog(string id, AwarenessStage previous, AwarenessStage next, float level, Stealth rule, System.Numerics.Vector3? lastSeen)
        {
            var last = lastSeen.HasValue ? lastSeen.Value.ToUnity().ToString("F1") : "none";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} stage={2} prev={3} level={4:F2} rule={5} last={6}",
                LogTag, id, next, previous, level, rule, last);
        }

        public static string StageLogPrefix(string id)
        {
            return $"{LogTag} id={id} stage=";
        }

        public static string FormatAlarmLog(string id, string from, Vector3 position)
        {
            return $"{LogTag} id={id} alarmed by={from} at={position.ToString("F1")}";
        }

        private void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        private void OnDisable()
        {
            _active.Remove(this);
            if (_indicatorObject != null)
            {
                _indicatorObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (!TryInitialize())
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            _active.Remove(this);
            if (_awareness != null)
            {
                _awareness.StageChanged -= OnStageChanged;
                if (_agent != null && _agent.Brain != null)
                {
                    _agent.Brain.RemovePerceptionContributor(_awareness);
                    _agent.Brain.RemoveBehavior(_awareness);
                }
            }

            if (_self != null)
            {
                _self.Died -= OnDied;
            }

            if (_indicatorObject != null)
            {
                Destroy(_indicatorObject);
            }
        }

        private void Update()
        {
            if (_awareness == null)
            {
                return;
            }

            // ルールの取り付けは敵の目標探しより後になることがある (キャンペーンはルールを Start で配る) ので、見つからなければ間を置いて探し直す。
            var target = _agent.Target;
            if (target != _observedTarget || (target != null && _observedStealth == null && Time.time >= _nextStealthLookup))
            {
                _observedTarget = target;
                _observedStealth = target != null ? target.GetComponentInParent<StealthTarget>() : null;
                _nextStealthLookup = Time.time + EnemyAgent.TargetSearchInterval;
            }

            if (_observedStealth != null)
            {
                _awareness.Rule = _observedStealth.Rule;
                _awareness.Observe(
                    transform.forward.ToNumerics(), _observedStealth.Posture, Mathf.Clamp01(_observedStealth.LightFactor), _observedStealth.Speed);
            }
            // 目標が無いあいだはルールを持ち越す。ResetTo で目標が外れると、次の tick で目標を拾ったその判断が Off のまま走ってすぐ交戦するため。
            else if (target != null)
            {
                _awareness.Rule = Stealth.Off;
                _awareness.Observe(transform.forward.ToNumerics(), AwarenessPosture.Standing, 1f, 0f);
            }
        }

        private void LateUpdate()
        {
            if (_awareness == null)
            {
                return;
            }

            // Off では今までどおりにするので印も出さない。Off のままのモードで bot ごとに印を作らないよう、On になって初めて作る。
            if (_awareness.Rule != Stealth.On)
            {
                if (_indicatorObject != null && _indicatorObject.activeSelf)
                {
                    _indicatorObject.SetActive(false);
                }

                return;
            }

            if (_indicatorObject == null)
            {
                CreateIndicator();
            }
            else if (!_indicatorObject.activeSelf)
            {
                _indicatorObject.SetActive(true);
            }

            _indicator.Show(_awareness.Stage, _awareness.Level);
            _indicatorObject.transform.position = IndicatorPosition();
        }

        private Vector3 IndicatorPosition()
        {
            float top;
            Vector3 center;
            if (_self != null && _self.TryGetBounds(out var bounds))
            {
                top = bounds.max.y;
                center = bounds.center;
            }
            else
            {
                center = transform.position;
                top = _agent.GroundPosition.y + FallbackHeadHeight;
            }

            return new Vector3(center.x, top + _indicatorHeightAboveHead, center.z);
        }

        // 敵の子にしないのは、やられ演出が敵の Renderer を拾って印まで抜け殻に含めないようにするため。毎フレーム位置だけを追う。
        private void CreateIndicator()
        {
            var prefab = _indicatorPrefab;
            if (prefab == null)
            {
                var catalog = AwarenessIndicatorCatalog.Load();
                prefab = catalog != null ? catalog.IndicatorPrefab : null;
            }

            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                instance.name = prefab.name + "-" + Id;
                var indicator = instance.GetComponent<IAwarenessIndicator>();
                if (indicator != null)
                {
                    _indicatorObject = instance;
                    _indicator = indicator;
                    return;
                }

                Destroy(instance);
                if (!_warnedIndicator)
                {
                    Debug.LogWarning($"{LogTag} id={Id} 印のプレハブ '{prefab.name}' の根に IAwarenessIndicator がありません。仮の印を出します。", this);
                    _warnedIndicator = true;
                }
            }

            var marker = AwarenessMarker.Create(Id);
            _indicatorObject = marker.gameObject;
            _indicator = marker;
        }

        private void OnStageChanged(AwarenessStage previous, AwarenessStage next)
        {
            Debug.Log(FormatStageLog(Id, previous, next, _awareness.Level, _awareness.Rule, _awareness.LastSeen), this);
            if (next == AwarenessStage.Engaged && _awareness.Rule == Stealth.On && !_awareness.RaisedByAlarm)
            {
                RaiseAlarm();
            }
        }

        private void RaiseAlarm()
        {
            var radius = _awareness.Spec.AlarmRadius;
            if (radius <= 0f)
            {
                return;
            }

            var position = _awareness.LastSeen.HasValue ? _awareness.LastSeen.Value.ToUnity() : _agent.GroundPosition;
            var origin = _agent.GroundPosition;

            // 伝えた先が StageChanged の中で一覧を変えても回し続けられるよう、写しを回す。
            foreach (var other in _active.ToArray())
            {
                if (other == null || !other.isActiveAndEnabled || !IsAllyOf(other) || other._agent == null)
                {
                    continue;
                }

                if (Vector3.Distance(origin, other._agent.GroundPosition) <= radius)
                {
                    other.ReceiveAlarm(this, position);
                }
            }
        }

        private void OnDied(Damageable self)
        {
            _awareness?.Reset();
        }
    }
}
