using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class CaptureTheFlagRunner : ModeRunnerBase
    {
        public const float DefaultBotPlanIntervalSeconds = 0.5f;

        public const float ReachHeight = CaptureTheFlag.ReachHeight;

        // 間隔の設定が小さすぎても、毎フレームより細かくは決め直さないための下限。
        private const float _minimumPlanInterval = 0.05f;

        [Header("Rules")]
        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の Rules と Bots の値の代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private CaptureTheFlagDefinition _definition;

        [SerializeField, Min(1), Tooltip("どちらかのチームがこの得点に届いたら試合が終わる")]
        private int _scoreLimit = CaptureTheFlagSpec.DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = CaptureTheFlagSpec.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = CaptureTheFlagSpec.DefaultRespawnSeconds;

        [SerializeField, Min(0.1f), Tooltip("旗に触れたことにする距離 (m、水平)。敵の旗を持つのも、落ちた自陣の旗を台に戻すのもこの距離")]
        private float _pickupRadius = CaptureTheFlagSpec.DefaultPickupRadius;

        [SerializeField, Min(0.1f), Tooltip("敵の旗を運ぶ人が自陣の台に着いたことにする距離 (m、水平)")]
        private float _captureRadius = CaptureTheFlagSpec.DefaultCaptureRadius;

        [SerializeField, Min(0.1f), Tooltip("落ちた旗が誰にも触れられずに台へ戻るまでの秒数")]
        private float _returnSeconds = CaptureTheFlagSpec.DefaultReturnSeconds;

        [SerializeField, Min(1), Tooltip("敵の旗を 1 回自陣の台へ運んだときに入る点")]
        private int _pointsPerCapture = CaptureTheFlagSpec.DefaultPointsPerCapture;

        [Header("Bots")]
        [SerializeField, Min(0.05f), Tooltip("bot の用事 (旗を取る・運ぶ・取り返す・守る) を決め直す間隔 (秒)")]
        private float _botPlanIntervalSeconds = DefaultBotPlanIntervalSeconds;

        [SerializeField, Range(0f, 1f), Tooltip("陣営の bot のうち攻めの役にする割合。残りは自陣の台を守る")]
        private float _botAttackShare = CaptureTheFlagBotPlanner.DefaultAttackShare;

        [Header("Objects")]
        [SerializeField, Tooltip("旗の台。Alpha と Bravo に 1 つずつ")]
        private FlagBase[] _bases = Array.Empty<FlagBase>();

        [SerializeField, Tooltip("旗の見た目。陣営ごとに 1 つまで。無い陣営の旗は見えないが、試合は進む")]
        private FlagObject[] _flagObjects = Array.Empty<FlagObject>();

        private readonly Dictionary<Side, FlagBase> _baseOf = new Dictionary<Side, FlagBase>();
        private readonly Dictionary<Side, FlagObject> _flagObjectOf = new Dictionary<Side, FlagObject>();
        private readonly Dictionary<Side, Vector3> _dropPoints = new Dictionary<Side, Vector3>();
        private readonly Dictionary<int, BotErrandKind> _loggedErrands = new Dictionary<int, BotErrandKind>();
        private readonly List<SideMember> _teamBots = new List<SideMember>();
        private CaptureTheFlag _game;
        private float _untilBotPlan;

        public CaptureTheFlag Game => _game;

        public CaptureTheFlagDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public CaptureTheFlagDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.CaptureTheFlag);

        public int ScoreLimit
        {
            get => _scoreLimit;
            set => _scoreLimit = value;
        }

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public float PickupRadius
        {
            get => _pickupRadius;
            set => _pickupRadius = value;
        }

        public float CaptureRadius
        {
            get => _captureRadius;
            set => _captureRadius = value;
        }

        public float ReturnSeconds
        {
            get => _returnSeconds;
            set => _returnSeconds = value;
        }

        public int PointsPerCapture
        {
            get => _pointsPerCapture;
            set => _pointsPerCapture = value;
        }

        public float BotPlanIntervalSeconds
        {
            get => _botPlanIntervalSeconds;
            set => _botPlanIntervalSeconds = value;
        }

        public float BotAttackShare
        {
            get => _botAttackShare;
            set => _botAttackShare = value;
        }

        public float EffectiveBotPlanIntervalSeconds
        {
            get
            {
                var definition = EffectiveDefinition;
                return definition != null ? definition.BotPlanIntervalSeconds : _botPlanIntervalSeconds;
            }
        }

        public float EffectiveBotAttackShare
        {
            get
            {
                var definition = EffectiveDefinition;
                return definition != null ? definition.BotAttackShare : _botAttackShare;
            }
        }

        public IReadOnlyList<FlagBase> Bases => _bases ?? Array.Empty<FlagBase>();

        public IReadOnlyList<FlagObject> FlagObjects => _flagObjects ?? Array.Empty<FlagObject>();

        protected override object HudDetail => _game != null ? new CaptureTheFlagStatus(_game, LocalPlayer != null ? LocalPlayerId : (int?)null) : null;

        public void SetBases(IReadOnlyList<FlagBase> bases)
        {
            _bases = Copy(bases);
        }

        public void SetFlagObjects(IReadOnlyList<FlagObject> flags)
        {
            _flagObjects = Copy(flags);
        }

        public CaptureTheFlagSpec CreateSpec()
        {
            var definition = EffectiveDefinition;
            if (definition != null)
            {
                return definition.ToSpec();
            }

            RequireBotTuning(_botPlanIntervalSeconds, _botAttackShare);
            return new CaptureTheFlagSpec(
                _scoreLimit, _timeLimitSeconds, _respawnSeconds, _pickupRadius, _captureRadius, _returnSeconds, _pointsPerCapture);
        }

        public static void RequireBotTuning(float planIntervalSeconds, float attackShare)
        {
            if (!(planIntervalSeconds > 0f) || float.IsInfinity(planIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(planIntervalSeconds), planIntervalSeconds, "botPlanIntervalSeconds must be positive and finite");
            }

            if (!(attackShare >= 0f && attackShare <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(attackShare), attackShare, "botAttackShare must be between 0 and 1");
            }
        }

        public static IReadOnlyDictionary<Side, FlagBase> ResolveBases(IReadOnlyList<FlagBase> bases)
        {
            var resolved = new Dictionary<Side, FlagBase>();
            var count = bases != null ? bases.Count : 0;
            for (var i = 0; i < count; i++)
            {
                var flagBase = bases[i];
                if (flagBase == null)
                {
                    throw new InvalidOperationException($"FlagBase #{i} is missing");
                }

                var side = flagBase.Side;
                if (!side.IsTeam)
                {
                    throw new InvalidOperationException($"FlagBase '{flagBase.name}' belongs to side {side.Id}; only sides 0 (Alpha) and 1 (Bravo) have flags");
                }

                if (resolved.TryGetValue(side, out var other))
                {
                    throw new InvalidOperationException($"FlagBase '{flagBase.name}' and '{other.name}' both belong to side {side.Id}");
                }

                resolved.Add(side, flagBase);
            }

            foreach (var side in CaptureTheFlag.FlagSides)
            {
                if (!resolved.ContainsKey(side))
                {
                    throw new InvalidOperationException($"no FlagBase for side {side.Id} is wired to the CaptureTheFlagRunner");
                }
            }

            return resolved;
        }

        public static bool IsWithin(Vector3 ground, Vector3 point, float radius)
        {
            return CaptureTheFlag.IsWithin(ground.ToNumerics(), point.ToNumerics(), radius);
        }

        public FlagBase BaseOf(Side side)
        {
            return _baseOf.TryGetValue(side, out var flagBase) ? flagBase : null;
        }

        public FlagObject FlagObjectOf(Side side)
        {
            return _flagObjectOf.TryGetValue(side, out var flag) ? flag : null;
        }

        public Vector3? FlagPosition(Side side)
        {
            if (_game == null || !_baseOf.TryGetValue(side, out var flagBase))
            {
                return null;
            }

            switch (_game.GetFlagState(side))
            {
                case FlagState.Dropped:
                    return _dropPoints.TryGetValue(side, out var dropped) ? dropped : flagBase.Position;
                case FlagState.Carried:
                    var carrierId = _game.GetCarrierId(side);
                    var carrier = carrierId.HasValue ? MemberOf(carrierId.Value) : null;
                    return carrier != null ? carrier.GroundPosition : flagBase.Position;
                default:
                    return flagBase.Position;
            }
        }

        public void Begin(
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException($"{nameof(CaptureTheFlagRunner)} '{name}' has already started a mode");
            }

            var session = CreateSession();
            try
            {
                base.Begin(session, members, spawnPoints, localPlayer);
            }
            catch
            {
                Detach();
                throw;
            }
        }

        public void UpdateFlagContacts()
        {
            if (_game == null || IsMatchOver)
            {
                return;
            }

            var spec = _game.Spec;
            for (var id = 0; id < Members.Count; id++)
            {
                // 前の人が旗を運び込んで試合が終わったら、残りの人はもう触れない。
                if (_game == null || _game.Session.State != GameModeState.Playing)
                {
                    return;
                }

                var session = _game.Session;
                var member = Members[id];
                if (member == null || !session.HasPlayer(id) || !member.IsAlive || session.IsDown(id))
                {
                    continue;
                }

                var ground = member.GroundPosition;
                foreach (var side in CaptureTheFlag.FlagSides)
                {
                    if (_game.GetFlagState(side) == FlagState.Carried)
                    {
                        continue;
                    }

                    var at = FlagPosition(side);
                    if (at.HasValue && IsWithin(ground, at.Value, spec.PickupRadius))
                    {
                        _game.Touch(side, id);
                    }
                }

                if (_game.IsCarrying(id) && _baseOf.TryGetValue(member.Side, out var ownBase) && IsWithin(ground, ownBase.Position, spec.CaptureRadius))
                {
                    _game.ReachBase(id);
                }
            }
        }

        public void PlanBots()
        {
            if (_game == null)
            {
                return;
            }

            var live = !IsMatchOver && _game.Session.State == GameModeState.Playing;
            var attackShare = EffectiveBotAttackShare;
            foreach (var side in CaptureTheFlag.FlagSides)
            {
                CollectTeamBots(side);
                for (var index = 0; index < _teamBots.Count; index++)
                {
                    var member = _teamBots[index];
                    var agent = member.GetComponent<EnemyAgent>();
                    var id = IdOf(member);
                    if (!live || !member.IsAlive || _game.Session.IsDown(id))
                    {
                        agent.ClearErrand();
                        LogErrand(id, member, BotErrand.None);
                        continue;
                    }

                    var enemy = CaptureTheFlag.Opponent(side);
                    var ownBase = _baseOf[side].Position;
                    var errand = CaptureTheFlagBotPlanner.Plan(
                        _game.IsCarrying(id),
                        CaptureTheFlagBotPlanner.IsAttacker(index, _teamBots.Count, attackShare),
                        ownBase.ToNumerics(),
                        _game.GetFlagState(side),
                        (FlagPosition(side) ?? ownBase).ToNumerics(),
                        (FlagPosition(enemy) ?? _baseOf[enemy].Position).ToNumerics());

                    if (errand.HasErrand)
                    {
                        agent.SetErrand(errand.Destination.ToUnity(), errand.OverridesCombat);
                    }
                    else
                    {
                        agent.ClearErrand();
                    }

                    LogErrand(id, member, errand);
                }
            }

            _teamBots.Clear();
        }

        protected override ModeSession CreateSession()
        {
            Detach();

            var bases = ResolveBases(_bases);
            var flags = ResolveFlagObjects(_flagObjects);
            var game = new CaptureTheFlag(CreateSpec());

            // 色の既定はオフラインの Alpha から見た色なので、陣営を変えたプレイヤーには自分の陣営から見た色にする
            var viewer = PlayerSide.TryGetTeam(out var playerTeam) ? playerTeam : (Team?)null;
            foreach (var pair in bases)
            {
                _baseOf.Add(pair.Key, pair.Value);
                pair.Value.Radius = game.Spec.CaptureRadius;
                pair.Value.ViewerTeam = viewer;
            }

            foreach (var pair in flags)
            {
                _flagObjectOf.Add(pair.Key, pair.Value);
                pair.Value.ViewerTeam = viewer;
            }

            foreach (var side in CaptureTheFlag.FlagSides)
            {
                if (!_flagObjectOf.ContainsKey(side))
                {
                    Debug.LogWarning($"{LogTag} 陣営 {side.Id} の FlagObject が配線されていないため、その旗は見えません。", this);
                }
            }

            _game = game;
            _game.FlagTaken += OnFlagTaken;
            _game.FlagDropped += OnFlagDropped;
            _game.FlagReturned += OnFlagReturned;
            _game.FlagCaptured += OnFlagCaptured;
            return _game.Session;
        }

        protected override void ValidateSession(ModeSession session)
        {
            if (_game == null || session != _game.Session)
            {
                throw new ArgumentException("CaptureTheFlagRunner can only start the session made by its own CreateSession", nameof(session));
            }
        }

        protected override void OnBegan()
        {
            ShowFlags();
            Debug.Log(CaptureTheFlagLog.Flags(_game), this);
            _untilBotPlan = Mathf.Max(_minimumPlanInterval, EffectiveBotPlanIntervalSeconds);
            PlanBots();
        }

        protected override void OnBeforeTick(float deltaSeconds)
        {
            UpdateFlagContacts();
            if (_game == null)
            {
                return;
            }

            _untilBotPlan -= deltaSeconds;
            if (_untilBotPlan <= 0f)
            {
                _untilBotPlan = Mathf.Max(_minimumPlanInterval, EffectiveBotPlanIntervalSeconds);
                PlanBots();
            }
        }

        protected override void OnKill(SideMember killer, SideMember victim)
        {
            if (_game != null)
            {
                _game.DropCarriedFlag(IdOf(victim));
            }
        }

        protected override void OnEnded(Side? winner)
        {
            if (_game == null)
            {
                return;
            }

            Debug.Log(CaptureTheFlagLog.Flags(_game), this);
            PlanBots();
        }

        protected override void OnStopping()
        {
            ClearErrands();
            Detach();
        }

        private void OnFlagTaken(Side flag, int playerId)
        {
            Debug.Log(CaptureTheFlagLog.Taken(flag, playerId), LogContext(playerId));
            _dropPoints.Remove(flag);
            AfterFlagMoved(flag);
        }

        private void OnFlagDropped(Side flag, int playerId)
        {
            Debug.Log(CaptureTheFlagLog.Dropped(flag, playerId), LogContext(playerId));
            var carrier = MemberOf(playerId);
            _dropPoints[flag] = carrier != null ? carrier.GroundPosition : _baseOf[flag].Position;
            AfterFlagMoved(flag);
        }

        private void OnFlagReturned(Side flag, int? playerId)
        {
            Debug.Log(CaptureTheFlagLog.Returned(flag, playerId), playerId.HasValue ? LogContext(playerId.Value) : this);
            _dropPoints.Remove(flag);
            AfterFlagMoved(flag);
        }

        private void OnFlagCaptured(Side flag, int playerId)
        {
            var session = _game.Session;
            var side = session.GetSide(playerId);
            Debug.Log(CaptureTheFlagLog.Captured(flag, playerId, side, session.GetScore(side)), LogContext(playerId));
            _dropPoints.Remove(flag);

            // この点で終わったときは、終わったときの状態のログに得点が載っている。
            if (session.State == GameModeState.Playing)
            {
                LogScore();
            }

            AfterFlagMoved(flag);
            RefreshHud();
        }

        private void AfterFlagMoved(Side flag)
        {
            ShowFlag(flag);
            Debug.Log(CaptureTheFlagLog.Flags(_game), this);
            PlanBots();
        }

        private void ShowFlags()
        {
            foreach (var side in CaptureTheFlag.FlagSides)
            {
                ShowFlag(side);
            }
        }

        private void ShowFlag(Side side)
        {
            var flag = FlagObjectOf(side);
            if (flag == null || _game == null)
            {
                return;
            }

            switch (_game.GetFlagState(side))
            {
                case FlagState.Carried:
                    var carrierId = _game.GetCarrierId(side);
                    var carrier = carrierId.HasValue ? MemberOf(carrierId.Value) : null;
                    flag.ShowCarried(carrier != null ? carrier.transform : null);
                    break;
                case FlagState.Dropped:
                    flag.ShowDropped(FlagPosition(side) ?? _baseOf[side].Position);
                    break;
                default:
                    flag.ShowAtBase(_baseOf[side].Position);
                    break;
            }
        }

        private void CollectTeamBots(Side side)
        {
            _teamBots.Clear();
            for (var id = 0; id < Members.Count; id++)
            {
                var member = Members[id];
                if (member == null || member.Side != side)
                {
                    continue;
                }

                // Awake で初期化に失敗した bot は動かせない (自分で止まっている) ので、役も割り振らない。
                var agent = member.GetComponent<EnemyAgent>();
                if (agent != null && agent.IsInitialized)
                {
                    _teamBots.Add(member);
                }
            }
        }

        private void LogErrand(int id, SideMember member, BotErrand errand)
        {
            if (_loggedErrands.TryGetValue(id, out var logged) ? logged == errand.Kind : errand.Kind == BotErrandKind.None)
            {
                return;
            }

            _loggedErrands[id] = errand.Kind;
            Debug.Log(CaptureTheFlagLog.Errand(id, errand.Kind, errand.Destination), member);
        }

        private UnityEngine.Object LogContext(int playerId)
        {
            var member = MemberOf(playerId);
            return member != null ? (UnityEngine.Object)member : this;
        }

        private void ClearErrands()
        {
            foreach (var member in Members)
            {
                var agent = member != null ? member.GetComponent<EnemyAgent>() : null;
                if (agent != null)
                {
                    agent.ClearErrand();
                }
            }

            _loggedErrands.Clear();
        }

        private void Detach()
        {
            if (_game != null)
            {
                _game.FlagTaken -= OnFlagTaken;
                _game.FlagDropped -= OnFlagDropped;
                _game.FlagReturned -= OnFlagReturned;
                _game.FlagCaptured -= OnFlagCaptured;
            }

            _game = null;
            _baseOf.Clear();
            _flagObjectOf.Clear();
            _dropPoints.Clear();
            _loggedErrands.Clear();
            _teamBots.Clear();
            _untilBotPlan = 0f;
        }

        private static Dictionary<Side, FlagObject> ResolveFlagObjects(IReadOnlyList<FlagObject> flags)
        {
            var resolved = new Dictionary<Side, FlagObject>();
            var count = flags != null ? flags.Count : 0;
            for (var i = 0; i < count; i++)
            {
                var flag = flags[i];
                if (flag == null)
                {
                    continue;
                }

                var side = flag.Side;
                if (!side.IsTeam)
                {
                    throw new InvalidOperationException($"FlagObject '{flag.name}' belongs to side {side.Id}; only sides 0 (Alpha) and 1 (Bravo) have flags");
                }

                if (resolved.TryGetValue(side, out var other))
                {
                    throw new InvalidOperationException($"FlagObject '{flag.name}' and '{other.name}' both belong to side {side.Id}");
                }

                resolved.Add(side, flag);
            }

            return resolved;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source != null ? source.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
