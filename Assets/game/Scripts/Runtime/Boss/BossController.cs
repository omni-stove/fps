using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 段階の計算は BossPhases に任せ、ここは体力を読んで渡し、返ってきた段階を判断・速さ・弱点・イベントへ当てはめるだけにする。
    // 個々のボスの動きは持たない。動きは段階の EnemySpec と IEnemyBehavior、または MoveSpeedMultiplier を読む側が作る。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class BossController : MonoBehaviour
    {
        public const string LogTag = "[Boss]";

        [SerializeField, Tooltip("段階。上から順に進む。最初の段階は始めから入っている")]
        private BossPhaseEntry[] _phases = { new BossPhaseEntry("Phase1", 1f, 1f, Array.Empty<string>()) };

        [Header("Zone multipliers")]
        [SerializeField, Min(0f)]
        private float _headMultiplier = (float)DamageCalculator.HeadMultiplier;

        [SerializeField, Min(0f)]
        private float _bodyMultiplier = (float)DamageCalculator.BodyMultiplier;

        [SerializeField, Min(0f)]
        private float _limbMultiplier = (float)DamageCalculator.LimbMultiplier;

        [SerializeField, Min(0f)]
        private float _weakPointMultiplier = (float)DamageCalculator.WeakPointMultiplier;

        private Damageable _self;
        private EnemyAgent _agent;
        private BossPhases _state;
        private BossWeakPoint[] _weakPoints = Array.Empty<BossWeakPoint>();
        private EnemySpec _baseSpec;
        private IEnemyBehavior _activeBehavior;
        private bool _agentNeedsPhase;
        private bool _subscribed;

        public event Action<BossController, BossPhaseChange> PhaseChanged;

        public string Id => _self != null ? _self.Id : name;

        public bool IsInitialized => _state != null;

        public BossPhases Phases => _state;

        public BossPhase CurrentPhase => _state?.Current;

        public float MoveSpeedMultiplier => _state != null ? _state.Current.MoveSpeedMultiplier : 1f;

        public IReadOnlyList<BossWeakPoint> WeakPoints => _weakPoints;

        public IReadOnlyList<BossPhaseEntry> PhaseEntries => _phases;

        public ZoneDamageMultipliers CreateZoneMultipliers()
        {
            return new ZoneDamageMultipliers(_headMultiplier, _bodyMultiplier, _limbMultiplier, _weakPointMultiplier);
        }

        public BossPhases CreatePhases()
        {
            if (_phases == null || _phases.Length == 0)
            {
                throw new ArgumentException("a boss needs at least one phase entry");
            }

            var phases = new BossPhase[_phases.Length];
            for (var i = 0; i < _phases.Length; i++)
            {
                var entry = _phases[i] ?? throw new ArgumentException("phase entry " + i + " is null");
                phases[i] = entry.ToPhase(i == 0);
            }

            return new BossPhases(phases);
        }

        public void Configure(IReadOnlyList<BossPhaseEntry> phases, ZoneDamageMultipliers multipliers)
        {
            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            if (multipliers == null)
            {
                throw new ArgumentNullException(nameof(multipliers));
            }

            var copy = new BossPhaseEntry[phases.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = phases[i] ?? throw new ArgumentException("phase entry " + i + " is null", nameof(phases));
            }

            var previous = _phases;
            _phases = copy;
            try
            {
                _ = CreatePhases();
            }
            catch
            {
                _phases = previous;
                throw;
            }

            _headMultiplier = (float)multipliers.Head;
            _bodyMultiplier = (float)multipliers.Body;
            _limbMultiplier = (float)multipliers.Limb;
            _weakPointMultiplier = (float)multipliers.WeakPoint;
        }

        public bool TryInitialize()
        {
            if (_state != null)
            {
                return true;
            }

            _self = GetComponent<Damageable>();
            if (_self == null)
            {
                Debug.LogError($"{LogTag} '{name}' には Damageable が必要です。", this);
                return false;
            }

            BossPhases phases;
            ZoneDamageMultipliers multipliers;
            try
            {
                phases = CreatePhases();
                multipliers = CreateZoneMultipliers();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} id={Id} の設定が不正です: {e.Message}", this);
                return false;
            }

            _self.ZoneMultipliers = multipliers;
            Begin(phases);
            return true;
        }

        // IEnemyBehavior や任意の条件はシリアライズできないので、それらを使う段階はコードから渡す。
        // 渡した時点で最初の段階から始め直し、今の体力で条件を満たす段階までその場で進める。
        public void UsePhases(BossPhases phases)
        {
            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            if (_self == null)
            {
                _self = GetComponent<Damageable>();
                if (_self == null)
                {
                    throw new InvalidOperationException($"BossController '{name}' needs a Damageable");
                }

                _self.ZoneMultipliers = CreateZoneMultipliers();
            }

            Begin(phases);
        }

        public bool Advance()
        {
            if (_state == null || _self.IsDead)
            {
                return false;
            }

            var change = _state.Advance(CurrentHealthFraction());
            if (!change.HasValue)
            {
                return false;
            }

            OnChanged(change.Value, "cue");
            return true;
        }

        // 次に段階が変わるまで有効。段階の中の行動の合図 (溜めの間だけ弱点を出すなど) で使う。
        public bool SetWeakPointExposed(string weakPointId, bool exposed)
        {
            if (string.IsNullOrWhiteSpace(weakPointId))
            {
                throw new ArgumentException("weakPointId must not be empty", nameof(weakPointId));
            }

            var found = false;
            foreach (var weakPoint in _weakPoints)
            {
                if (weakPoint == null || !string.Equals(weakPoint.Id, weakPointId, StringComparison.Ordinal))
                {
                    continue;
                }

                found = true;
                if (weakPoint.SetExposed(exposed))
                {
                    Debug.Log(FormatWeakPointLog(Id, weakPoint.Id, exposed), this);
                }
            }

            if (!found)
            {
                Debug.LogWarning($"{LogTag} id={Id} has no weak point '{weakPointId}'", this);
            }

            return found;
        }

        // 段階の条件 (時間で進むものなど) と、EnemyAgent の初期化を待っていた当てはめを、Play 中は毎フレーム進める。
        // Edit Mode のテストでは直接呼ぶ。
        public void Tick(float deltaSeconds)
        {
            if (_state == null)
            {
                return;
            }

            if (_agentNeedsPhase)
            {
                ApplyToAgent(_state.Current);
            }

            Evaluate(deltaSeconds);
        }

        public static string FormatPhaseLog(string id, BossPhaseChange change, string reason)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} phase={2}->{3} from={4} to={5} reason={6} health={7:0.###} speed={8:0.###} exposed={9}",
                LogTag,
                id,
                change.PreviousIndex,
                change.Index,
                change.Previous.Id,
                change.Current.Id,
                reason,
                change.HealthFraction,
                change.Current.MoveSpeedMultiplier,
                change.Current.ExposedWeakPoints.Count > 0 ? string.Join(",", change.Current.ExposedWeakPoints) : "none");
        }

        public static string FormatWeakPointLog(string id, string weakPointId, bool exposed)
        {
            return $"{LogTag} id={id} weakpoint={weakPointId} exposed={exposed}";
        }

        public static string FormatHitLog(string id, HitZone? zone, int damage, int health, int maxHealth)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} hit zone={2} damage={3} health={4}/{5}",
                LogTag,
                id,
                zone.HasValue ? zone.Value.ToString() : "none",
                damage,
                health,
                maxHealth);
        }

        private void Awake()
        {
            if (!TryInitialize())
            {
                enabled = false;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_agent != null && _agent.Brain != null && _activeBehavior != null)
            {
                _agent.Brain.RemoveBehavior(_activeBehavior);
            }
        }

        private void Begin(BossPhases phases)
        {
            _weakPoints = GetComponentsInChildren<BossWeakPoint>(true);
            _agent = GetComponent<EnemyAgent>();
            Subscribe();

            _state = phases;
            _state.Reset(CurrentHealthFraction());
            ApplyPhase(_state.Current);
            Debug.Log(
                $"{LogTag} id={Id} phases={string.Join(",", PhaseIds(_state))} weakpoints={string.Join(",", WeakPointIds())} phase={_state.Current.Id}",
                this);
            Evaluate(0f);
        }

        private void Evaluate(float deltaSeconds)
        {
            if (_self.IsDead)
            {
                return;
            }

            BossPhaseChange? change;
            try
            {
                change = _state.Update(CurrentHealthFraction(), deltaSeconds);
            }
            catch (Exception e)
            {
                // 任意の条件が投げても、ボスの戦い (体力・射撃) は止めない。ログに出して段階はそのままにする。
                Debug.LogException(e, this);
                return;
            }

            if (change.HasValue)
            {
                OnChanged(change.Value, change.Value.Current.Trigger.Description);
            }
        }

        private void OnChanged(BossPhaseChange change, string reason)
        {
            ApplyPhase(change.Current);
            Debug.Log(FormatPhaseLog(Id, change, reason), this);
            PhaseChanged?.Invoke(this, change);
            CombatEvents.RaiseBossPhaseChanged(new BossPhaseChangedInfo(gameObject, _self, Id, change));
        }

        private void ApplyPhase(BossPhase phase)
        {
            foreach (var weakPoint in _weakPoints)
            {
                if (weakPoint == null)
                {
                    continue;
                }

                var exposed = phase.Exposes(weakPoint.Id);
                if (weakPoint.SetExposed(exposed))
                {
                    Debug.Log(FormatWeakPointLog(Id, weakPoint.Id, exposed), this);
                }
            }

            foreach (var id in phase.ExposedWeakPoints)
            {
                if (!HasWeakPoint(id))
                {
                    Debug.LogWarning($"{LogTag} id={Id} phase={phase.Id} exposes '{id}', but no BossWeakPoint has that id", this);
                }
            }

            ApplyToAgent(phase);
        }

        // EnemyAgent は自分の Awake で判断を作るので、こちらが先に動いたときは作られるのを待って当てはめる。
        private void ApplyToAgent(BossPhase phase)
        {
            _agentNeedsPhase = false;
            if (_agent == null)
            {
                return;
            }

            if (_agent.Brain == null)
            {
                _agentNeedsPhase = true;
                return;
            }

            if (_baseSpec == null)
            {
                _baseSpec = _agent.Brain.Spec;
            }

            var spec = phase.Spec ?? _baseSpec;
            if (!ReferenceEquals(_agent.Brain.Spec, spec))
            {
                _agent.ChangeSpec(spec);
            }

            _agent.MoveSpeedMultiplier = phase.MoveSpeedMultiplier;

            if (!ReferenceEquals(_activeBehavior, phase.Behavior))
            {
                if (_activeBehavior != null)
                {
                    _agent.Brain.RemoveBehavior(_activeBehavior);
                }

                _activeBehavior = phase.Behavior;
                if (_activeBehavior != null && !Contains(_agent.Brain.Behaviors, _activeBehavior))
                {
                    _agent.Brain.AddBehavior(_activeBehavior);
                }
            }
        }

        private void OnDamaged(Damageable damageable, DamageReport report)
        {
            Debug.Log(FormatHitLog(Id, report.Zone, report.Amount, damageable.Health.Current, damageable.Health.Max), this);
            if (_state != null)
            {
                Evaluate(0f);
            }
        }

        private void OnHealthReset(Health health)
        {
            if (_state == null)
            {
                return;
            }

            var change = _state.Reset(CurrentHealthFraction());
            if (change.HasValue)
            {
                OnChanged(change.Value, "reset");
                return;
            }

            // 段階が変わらなくても、行動の合図で出した弱点が復活後に残らないよう当て直す
            ApplyPhase(_state.Current);
        }

        private float CurrentHealthFraction()
        {
            var health = _self.Health;
            return BossPhases.HealthFraction(health.Current, health.Max);
        }

        private bool HasWeakPoint(string id)
        {
            foreach (var weakPoint in _weakPoints)
            {
                if (weakPoint != null && string.Equals(weakPoint.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<string> WeakPointIds()
        {
            foreach (var weakPoint in _weakPoints)
            {
                if (weakPoint != null)
                {
                    yield return weakPoint.Id;
                }
            }
        }

        private static IEnumerable<string> PhaseIds(BossPhases phases)
        {
            foreach (var phase in phases.Phases)
            {
                yield return phase.Id;
            }
        }

        private static bool Contains(IReadOnlyList<IEnemyBehavior> behaviors, IEnemyBehavior behavior)
        {
            foreach (var item in behaviors)
            {
                if (ReferenceEquals(item, behavior))
                {
                    return true;
                }
            }

            return false;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _self.Damaged += OnDamaged;
            _self.HealthReset += OnHealthReset;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _self == null)
            {
                return;
            }

            _self.Damaged -= OnDamaged;
            _self.HealthReset -= OnHealthReset;
            _subscribed = false;
        }
    }

    [Serializable]
    public sealed class BossPhaseEntry
    {
        [SerializeField]
        private string _id = string.Empty;

        [SerializeField, Range(0f, 1f), Tooltip("体力の割合がこれ以下になったら、この段階に入る。最初の段階では使わない")]
        private float _healthFraction = 1f;

        [SerializeField, Min(0.01f), Tooltip("元の歩きの速さに掛ける倍率")]
        private float _moveSpeedMultiplier = 1f;

        [SerializeField, Tooltip("この段階で出す弱点 (BossWeakPoint の Id)。ここに無い弱点は隠す")]
        private string[] _exposedWeakPoints = Array.Empty<string>();

        // Inspector で配列に足したときに、Unity がこれで作る。
        public BossPhaseEntry()
        {
        }

        public BossPhaseEntry(string id, float healthFraction, float moveSpeedMultiplier, IReadOnlyList<string> exposedWeakPoints)
        {
            _id = id ?? string.Empty;
            _healthFraction = healthFraction;
            _moveSpeedMultiplier = moveSpeedMultiplier;
            _exposedWeakPoints = exposedWeakPoints != null ? Copy(exposedWeakPoints) : Array.Empty<string>();
        }

        public string Id => _id;

        public float HealthFraction => _healthFraction;

        public float MoveSpeedMultiplier => _moveSpeedMultiplier;

        public IReadOnlyList<string> ExposedWeakPoints => _exposedWeakPoints ?? Array.Empty<string>();

        public BossPhase ToPhase(bool first)
        {
            return new BossPhase(
                _id,
                first ? null : BossPhaseTrigger.HealthAtOrBelow(_healthFraction),
                moveSpeedMultiplier: _moveSpeedMultiplier,
                exposedWeakPoints: ExposedWeakPoints);
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var copy = new string[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
