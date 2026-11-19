using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // EnemyAgent が判断を進める前に見え方の材料を渡す必要があるので、EnemyAgent より先に Update させる。
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    public sealed class EnemyStealth : MonoBehaviour, IMeleeWitness
    {
        public const string LogTag = "[Stealth]";

        public const float DefaultIndicatorHeightAboveHead = 0.5f;

        public const float FallbackHeadHeight = 1.8f;

        private static readonly List<EnemyStealth> _active = new List<EnemyStealth>();

        [Header("Awareness")]
        [SerializeField, Tooltip("見つかるまでのゲージのたまり方・減り方と、物音の聞こえ方")]
        private EnemyAwarenessSettings _settings = new EnemyAwarenessSettings();

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

        public EnemyAwarenessSettings Settings => _settings;

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

        public bool IsUnaware => Stage == AwarenessStage.Unaware;

        public string StageName => Stage.ToString();

        public AwarenessSpec CreateSpec()
        {
            return (_settings ?? new EnemyAwarenessSettings()).ToSpec();
        }

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

            if (_settings == null)
            {
                _settings = new EnemyAwarenessSettings();
            }

            _settings.CopyFrom(spec);
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

        // Off でも物音には調べに行かせる。ゲージの無い今までの敵にも、注意をそらす能力が効くように。
        // 鳴らした者が味方なら聞き流す。見張り同士の足音や撃ち合いでゲージがたまらないように。
        public HearingOutcome Hear(GameObject source, SoundStimulus sound)
        {
            if (_awareness == null || _agent == null || _agent.Brain == null || !IsHostileSource(source))
            {
                return HearingOutcome.NotHeard;
            }

            var brain = _agent.Brain;
            var outcome = _awareness.Hear(brain, _agent.GroundPosition.ToNumerics(), sound);
            if (outcome == HearingOutcome.RuleOff && sound.Draws)
            {
                if (StealthAwareness.IsFighting(brain.State))
                {
                    outcome = HearingOutcome.Engaged;
                }
                else
                {
                    _agent.Alert(sound.Position.ToUnity());
                    outcome = HearingOutcome.Investigating;
                }
            }

            // 足音と銃声は毎歩・毎発届くので、段階が変わったときのログ (FormatStageLog) に任せる。
            if (sound.Draws && outcome != HearingOutcome.NotHeard)
            {
                Debug.Log(FormatHeardLog(Id, sound, outcome, _awareness.Rule), this);
            }

            return outcome;
        }

        public bool IsHostileSource(GameObject source)
        {
            if (source == null)
            {
                return true;
            }

            if (source.transform.IsChildOf(transform))
            {
                return false;
            }

            var mine = _side != null ? _side : GetComponent<SideMember>();
            var theirs = source.GetComponentInParent<SideMember>();
            if (mine != null && theirs != null)
            {
                return mine.Side != theirs.Side;
            }

            // 陣営を持たない敵 (キャンペーン) はみな味方なので、陣営の無い敵の音は仲間の音として扱う (IsAllyOf と同じ)。
            return mine != null || theirs != null || source.GetComponentInParent<EnemyAgent>() == null;
        }

        // ゲージは今の目標しか見ていないので、ほかの相手を見ているかは分からない。目標が attacker で、今見えているときだけ上げる。
        public bool Witness(Damageable attacker, float level)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (_awareness == null || (_agent.Brain != null && _agent.Brain.IsDead) || _agent.PerceivedTarget != attacker || !_awareness.Meter.IsSeeing)
            {
                return false;
            }

            return _awareness.Witness(attacker.GroundPoint.ToNumerics(), level);
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

        public static string FormatHeardLog(string id, SoundStimulus sound, HearingOutcome outcome, Stealth rule)
        {
            return $"{LogTag} id={id} heard kind={sound.Kind} at={sound.Position.ToUnity().ToString("F1")} outcome={outcome} rule={rule}";
        }

        public static string HeardLogPrefix(string id)
        {
            return $"{LogTag} id={id} heard ";
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

            MeleeWitnesses.Add(this);
        }

        private void OnDisable()
        {
            _active.Remove(this);
            MeleeWitnesses.Remove(this);
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
            MeleeWitnesses.Remove(this);
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
            // 囮を見せられている間は囮の姿勢と明るさでゲージをためる。
            var target = _agent.PerceivedTarget;
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
