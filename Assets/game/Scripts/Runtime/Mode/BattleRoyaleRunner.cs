using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// バトルロイヤル (<see cref="BattleRoyaleSession"/>) をシーンで進める。分隊 (<see cref="Squads"/>) で戦い、倒れた人は戻らず、最後に残った分隊が勝つ。
    /// 分隊の数と人数・制限時間・bot の用事を決め直す間隔と探す半径・パラシュートで降ろす高さは、
    /// <see cref="Definition"/>、入口 (<see cref="GameContent.BattleRoyale"/>)、Inspector の値の順で読む。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>始めたとき、<see cref="StartsUnarmed"/> なら全員の武器を手放させ (<see cref="WeaponController.Disarm"/>・<see cref="EnemyAgent.Disarm"/>)、
    /// シーンの拾える物 (<see cref="PickupSpawnPoint"/> など) から拾って戦わせる。</item>
    /// <item>始めたとき、<see cref="Zone"/> (無ければシーンの <see cref="ShrinkingZone"/>) を参加者だけを相手に始め、終わったら止める。エリアは自分の Update で進む。</item>
    /// <item>参加者の体力が 0 になったら、分隊にほかに立っている人がいればダウンさせ (<see cref="Damageable.IsDowned"/>)、操作を止める。
    /// プレイヤーは Interact (キーボードは E、ゲームパッドは Y) を押し続けると ルールの <see cref="ActionRules.ReviveRange"/> 以内のダウンした分隊の味方を起こし、
    /// bot は範囲にダウンした分隊の味方がいれば起こす。</item>
    /// <item>倒れて戻らなくなった人 (分隊が全滅した・出血した) は、持っていた武器と <see cref="DropOnDeath.Drops"/> を <see cref="DropOnDeath"/> で足元に落とす。
    /// ダウンしただけでは落とさない (起こされたら持ったまま戦うので)。DropOnDeath が無い参加者には始めたときに付ける。</item>
    /// <item>bot には <see cref="EffectiveBotPlanIntervalSeconds"/> ごとに <see cref="BattleRoyaleBotPlanner"/> で用事 (エリアの中へ移る・味方を起こす・拾える物を取る) を決めて渡す
    /// (<see cref="EnemyAgent.SetErrand"/>)。用事が変わるたびに <c>[Mode] errand</c> のログを出す。</item>
    /// <item>HUD に出す分隊ごとの残り人数と順位・エリアの様子・自分が起こしている相手・武器を持っているかは <see cref="BattleRoyaleStatus"/> にして渡す。</item>
    /// <item>分隊が脱落するたびに <c>[Mode] eliminated</c>、終わったら <c>[Mode] ended</c> と全分隊の順位 <c>[Mode] placements</c> のログ (<see cref="BattleRoyaleLog"/>) を出す。</item>
    /// <item>ダウン・ルールはシーンの <see cref="GameBootstrap"/> のルールから読む。ダウンを使わないルールでも、このモードはダウンを使う。</item>
    /// <item><see cref="StartFrom"/> が <see cref="BattleRoyaleStart.Parachute"/> なら、始めたときにプレイヤーを上空に出してパラシュートで降ろす (<c>[Mode] drop</c>)。既定は地面から。</item>
    /// </list>
    /// </remarks>
    public sealed class BattleRoyaleRunner : ModeRunnerBase
    {
        public const int DefaultSquadCount = BattleRoyaleSession.DefaultSquadCount;
        public const int DefaultSquadSize = BattleRoyaleSession.DefaultSquadSize;

        /// <summary>制限時間の既定値。0 は制限なし (縮むエリアで決着させる)。</summary>
        public const float DefaultTimeLimitSeconds = 0f;

        /// <summary>bot の用事を決め直す既定の間隔 (秒)。毎フレーム拾える物を探すと重いので間をあける。</summary>
        public const float DefaultBotPlanIntervalSeconds = 0.5f;

        /// <summary>Assets/InputSystem_Actions.inputactions の Interact アクション。</summary>
        public const string InteractActionPath = InputActionPaths.Interact;

        /// <summary><see cref="StartFrom"/> が <see cref="BattleRoyaleStart.Parachute"/> のとき、出現点からどれだけ上に出すか (m) の既定値。</summary>
        public const float DefaultDropHeight = 60f;

        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の分隊の数と人数・制限時間・bot の間隔と半径・降ろす高さの代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private BattleRoyaleDefinition _definition;

        [SerializeField, Min(2), Tooltip("分隊の数。参加者の陣営の ID はこれより小さいこと")]
        private int _squadCount = DefaultSquadCount;

        [SerializeField, Min(1), Tooltip("1 隊の人数の上限")]
        private int _squadSize = DefaultSquadSize;

        [SerializeField, Min(0f), Tooltip("制限時間 (秒)。0 なら制限なし (縮むエリアで決着させる)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Tooltip("始めたときに全員の武器を手放させ、拾える物から拾って戦わせるか")]
        private bool _startsUnarmed = true;

        [SerializeField, Tooltip("縮むエリア。空ならシーンから探す")]
        private ShrinkingZone _zone;

        [SerializeField, Min(0.05f), Tooltip("bot の用事 (エリアの中へ移る・拾える物を取る) を決め直す間隔 (秒)")]
        private float _botPlanIntervalSeconds = DefaultBotPlanIntervalSeconds;

        [SerializeField, Min(0f), Tooltip("bot がダウンした味方と拾える物を探す半径 (m)")]
        private float _botSearchRadius = BattleRoyaleBotPlanner.DefaultSearchRadius;

        [SerializeField, Tooltip("始めたときにプレイヤーを地面から始めるか、上空からパラシュートで降ろすか")]
        private BattleRoyaleStart _start = BattleRoyaleStart.Ground;

        [SerializeField, Min(0f), Tooltip("パラシュートで降ろすとき、出現点からどれだけ上に出すか (m)")]
        private float _dropHeight = DefaultDropHeight;

        private readonly List<Damageable> _zoneTargets = new List<Damageable>();
        private readonly Dictionary<int, BotErrandKind> _loggedErrands = new Dictionary<int, BotErrandKind>();
        private readonly List<BotPickupOption> _pickupOptions = new List<BotPickupOption>();
        private readonly List<System.Numerics.Vector3> _downedSquadmates = new List<System.Numerics.Vector3>();
        private BattleRoyaleSession _battleRoyale;
        private ShrinkingZone _runningZone;
        private bool _subscribed;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private int? _localReviveTarget;
        private float _untilBotPlan;

        /// <summary>進めているバトルロイヤル。開始前は null。</summary>
        public BattleRoyaleSession BattleRoyale => _battleRoyale;

        public override bool HasSquads => true;

        /// <summary>動かしている縮むエリア。無ければ null。</summary>
        public ShrinkingZone RunningZone => _runningZone;

        /// <summary>この画面で操作しているプレイヤーがいま起こしている相手。起こしていなければ null。</summary>
        public SideMember LocalReviveTarget => _localReviveTarget.HasValue ? MemberOf(_localReviveTarget.Value) : null;

        /// <summary>調整値のアセット。null なら入口 (<see cref="GameContent"/>) のもの、それも無ければ Inspector の値で始める。</summary>
        public BattleRoyaleDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        /// <summary>配線した <see cref="Definition"/>、空なら入口 (<see cref="GameContent"/>) のもの。どちらも無ければ null。</summary>
        public BattleRoyaleDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.BattleRoyale);

        /// <summary>いま bot の用事を決め直す間隔 (秒)。<see cref="EffectiveDefinition"/> があればその値、無ければ Inspector の <see cref="BotPlanIntervalSeconds"/>。</summary>
        public float EffectiveBotPlanIntervalSeconds
        {
            get
            {
                var definition = EffectiveDefinition;
                return definition != null ? definition.BotPlanIntervalSeconds : _botPlanIntervalSeconds;
            }
        }

        /// <summary>いま bot がダウンした味方と拾える物を探す半径 (m)。<see cref="EffectiveDefinition"/> があればその値、無ければ Inspector の <see cref="BotSearchRadius"/>。</summary>
        public float EffectiveBotSearchRadius
        {
            get
            {
                var definition = EffectiveDefinition;
                return definition != null ? definition.BotSearchRadius : _botSearchRadius;
            }
        }

        /// <summary>いまパラシュートで降ろす高さ (m)。<see cref="EffectiveDefinition"/> があればその値、無ければ Inspector の <see cref="DropHeight"/>。</summary>
        public float EffectiveDropHeight
        {
            get
            {
                var definition = EffectiveDefinition;
                return definition != null ? definition.DropHeight : _dropHeight;
            }
        }

        public int SquadCount
        {
            get => _squadCount;
            set => _squadCount = value;
        }

        public int SquadSize
        {
            get => _squadSize;
            set => _squadSize = value;
        }

        /// <summary>制限時間 (秒)。0 なら制限なし。</summary>
        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public bool StartsUnarmed
        {
            get => _startsUnarmed;
            set => _startsUnarmed = value;
        }

        /// <summary>縮むエリア。null なら始めるときにシーンから探す。</summary>
        public ShrinkingZone Zone
        {
            get => _zone;
            set => _zone = value;
        }

        /// <summary>Inspector の間隔。<see cref="EffectiveDefinition"/> があるときは使わない。</summary>
        public float BotPlanIntervalSeconds
        {
            get => _botPlanIntervalSeconds;
            set => _botPlanIntervalSeconds = value;
        }

        public float BotSearchRadius
        {
            get => _botSearchRadius;
            set => _botSearchRadius = value;
        }

        /// <summary>
        /// 始めたときにプレイヤーをどこから始めさせるか。既定は <see cref="BattleRoyaleStart.Ground"/> (出現点の地面)。
        /// <see cref="BattleRoyaleStart.Parachute"/> なら、<see cref="ParachuteMovement"/> を持つ参加者 (bot は持たない) を
        /// 出現点の <see cref="EffectiveDropHeight"/> 上に出してパラシュートを開かせる。ルールでパラシュートを開けなければその参加者は地面から始める。
        /// </summary>
        public BattleRoyaleStart StartFrom
        {
            get => _start;
            set
            {
                if (!Enum.IsDefined(typeof(BattleRoyaleStart), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined BattleRoyaleStart");
                }

                _start = value;
            }
        }

        /// <summary><see cref="StartFrom"/> が <see cref="BattleRoyaleStart.Parachute"/> のとき、出現点からどれだけ上に出すか (m)。</summary>
        public float DropHeight
        {
            get => _dropHeight;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value must be finite and not negative");
                }

                _dropHeight = value;
            }
        }

        /// <summary>
        /// バトルロイヤルを作る。timeLimitSeconds が 0 なら制限なし。actions のダウンが Off なら Revive にして使う (このモードはダウンが前提)。
        /// 値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public static BattleRoyaleSession CreateBattleRoyale(int squadCount, int squadSize, float timeLimitSeconds, ActionRules actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            if (!(timeLimitSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "timeLimitSeconds must not be negative");
            }

            var withDowned = actions.Downed == Downed.Revive ? actions : actions.WithDowned(Downed.Revive);
            var limit = timeLimitSeconds > 0f ? timeLimitSeconds : float.PositiveInfinity;
            return new BattleRoyaleSession(squadCount, squadSize, limit, withDowned);
        }

        /// <summary>
        /// <see cref="EffectiveDefinition"/> があればその値で、無ければ Inspector の値で、actions を使うバトルロイヤルを組む。
        /// アセットの値が不正なら <see cref="InvalidOperationException"/>、Inspector の値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public BattleRoyaleSession CreateConfiguredBattleRoyale(ActionRules actions)
        {
            var definition = EffectiveDefinition;
            return definition != null
                ? definition.CreateBattleRoyale(actions)
                : CreateBattleRoyale(_squadCount, _squadSize, _timeLimitSeconds, actions);
        }

        /// <summary>
        /// battleRoyale をこの Runner で始める (<see cref="ModeRunnerBase.Begin"/>)。Edit Mode のテストと、シーンを使わずに始めるときに使う。
        /// battleRoyale は始める前で参加者がいないこと。
        /// </summary>
        public void Begin(
            BattleRoyaleSession battleRoyale,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (battleRoyale == null)
            {
                throw new ArgumentNullException(nameof(battleRoyale));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{GetType().Name} '{name}' has already started a mode");
            }

            _battleRoyale = battleRoyale;
            try
            {
                Begin(battleRoyale.Mode, members, spawnPoints, localPlayer);
            }
            catch
            {
                Unsubscribe();
                _battleRoyale = null;
                throw;
            }
        }

        /// <summary>
        /// actor が、ルールの <see cref="ActionRules.ReviveRange"/> (足元どうしで測る) 以内でいちばん近いダウンした分隊の味方を deltaSeconds だけ起こす。誰もいなければ <see cref="ReviveStep.Rejected"/>。
        /// Play 中はプレイヤーが Interact を押している間と、範囲に味方のいる bot について、Tick の前に呼ばれる。
        /// </summary>
        public ReviveStep HoldRevive(SideMember actor, float deltaSeconds)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException($"{GetType().Name} '{name}' has not started a mode");
            }

            var actorId = IdOf(actor);
            if (actorId < 0)
            {
                throw new ArgumentException($"'{(actor != null ? actor.name : "null")}' is not a member of this mode", nameof(actor));
            }

            var targetId = NearestDownedSquadmate(actor, _battleRoyale.Actions.ReviveRange);
            if (targetId < 0)
            {
                return ReviveStep.Rejected;
            }

            var step = _battleRoyale.Revive(actorId, targetId, deltaSeconds);
            if (actor == LocalPlayer && step == ReviveStep.Progressed)
            {
                _localReviveTarget = targetId;
            }

            return step;
        }

        /// <summary>
        /// bot の用事を今すぐ決め直して渡す。Play 中は <see cref="EffectiveBotPlanIntervalSeconds"/> ごとに呼ばれる。
        /// NavMesh の無い Edit Mode のテストでも、渡した用事 (<see cref="EnemyAgent.Errand"/>) は確かめられる。
        /// </summary>
        public void PlanBots()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException($"{GetType().Name} '{name}' has not started a mode");
            }

            var zone = _runningZone != null && _runningZone.IsRunning ? _runningZone.State : (ZoneState?)null;
            var pickups = Pickup.FindActive();
            var searchRadius = EffectiveBotSearchRadius;
            foreach (var member in Members)
            {
                var agent = member != null ? member.GetComponent<EnemyAgent>() : null;
                if (agent == null || !agent.IsInitialized)
                {
                    continue;
                }

                var id = IdOf(member);
                if (!_battleRoyale.IsLive || !_battleRoyale.IsStanding(id))
                {
                    agent.ClearErrand();
                    _loggedErrands.Remove(id);
                    continue;
                }

                CollectOptions(member, agent, pickups);
                CollectDownedSquadmates(member);
                var errand = BattleRoyaleBotPlanner.Plan(
                    member.GroundPosition.ToNumerics(), zone, !agent.IsUnarmed, _pickupOptions, _downedSquadmates, searchRadius);

                if (errand.HasErrand)
                {
                    agent.SetErrand(errand.Destination.ToUnity(), errand.OverridesCombat);
                }
                else
                {
                    agent.ClearErrand();
                }

                if (!_loggedErrands.TryGetValue(id, out var logged) || logged != errand.Kind)
                {
                    _loggedErrands[id] = errand.Kind;
                    Debug.Log(BattleRoyaleLog.Errand(id, errand.Kind, errand.Destination), member);
                }
            }
        }

        protected override ModeSession CreateSession()
        {
            _battleRoyale = CreateConfiguredBattleRoyale(ResolveActions());
            return _battleRoyale.Mode;
        }

        protected override void ValidateSession(ModeSession session)
        {
            if (_battleRoyale == null || _battleRoyale.Mode != session)
            {
                throw new ArgumentException(
                    $"{GetType().Name} runs a BattleRoyaleSession; start it with Begin(BattleRoyaleSession, ...) instead of a bare ModeSession",
                    nameof(session));
            }
        }

        protected override void StartSession(ModeSession session)
        {
            Subscribe();
            AcquireInteractAction();
            _battleRoyale.Start();
            if (_battleRoyale.EntrantCount < 2)
            {
                Debug.LogWarning($"{LogTag} 人のいる分隊が {_battleRoyale.EntrantCount} つしかないため、最後の分隊が決まりません。", this);
            }
        }

        protected override void OnBegan()
        {
            _zoneTargets.Clear();
            foreach (var member in Members)
            {
                if (member == null)
                {
                    continue;
                }

                _zoneTargets.Add(member.Damageable);
                PrepareDrops(member);
                if (_startsUnarmed)
                {
                    Disarm(member);
                }

                if (_start == BattleRoyaleStart.Parachute)
                {
                    DropByParachute(member);
                }
            }

            StartZone();
            _untilBotPlan = 0f;

            // Begin は OnBegan の前に HUD を書くので、エリアと武器なしの案内を出すためにもう一度書く。
            RefreshHud();
        }

        protected override void AdvanceSession(float deltaSeconds)
        {
            _battleRoyale.Tick(deltaSeconds);
        }

        protected override void OnBeforeTick(float deltaSeconds)
        {
            _localReviveTarget = null;
            if (!_battleRoyale.IsLive)
            {
                return;
            }

            var local = LocalPlayer;
            if (local != null && _interactAction != null && _interactAction.IsPressed())
            {
                HoldRevive(local, deltaSeconds);
            }

            foreach (var member in Members)
            {
                if (member != null && member != local && member.GetComponent<EnemyAgent>() != null && _battleRoyale.IsStanding(IdOf(member)))
                {
                    HoldRevive(member, deltaSeconds);
                }
            }

            _untilBotPlan -= deltaSeconds;
            if (_untilBotPlan <= 0f)
            {
                _untilBotPlan = Mathf.Max(0.05f, EffectiveBotPlanIntervalSeconds);
                PlanBots();
            }
        }

        protected override bool InterceptDeath(SideMember killer, SideMember victim)
        {
            var victimId = IdOf(victim);
            var killerId = IdOf(killer);
            var hit = _battleRoyale.ReportLethal(victimId, killerId >= 0 ? killerId : (int?)null);
            if (hit == BattleRoyaleHit.Downed)
            {
                victim.Damageable.SetDowned(true);
            }
            else if (hit == BattleRoyaleHit.Killed)
            {
                Debug.Log(killerId >= 0 ? ModeLog.Kill(killerId, victimId) : ModeLog.Death(victimId), victim);
                DropBelongings(victim);
            }

            // ダウンも倒れたこともここで報告し終えたので、ModeRunnerBase の報告はさせない。
            return true;
        }

        protected override object HudDetail
        {
            get
            {
                if (_battleRoyale == null)
                {
                    return null;
                }

                var local = LocalPlayer;
                var localId = IdOf(local);
                var controller = local != null ? local.GetComponentInChildren<WeaponController>(true) : null;
                var unarmed = controller != null && controller.IsInitialized && controller.IsUnarmed;
                var zone = _runningZone != null && _runningZone.IsRunning ? _runningZone.State : (ZoneState?)null;
                return new BattleRoyaleStatus(_battleRoyale, localId >= 0 ? localId : (int?)null, _localReviveTarget, zone, unarmed);
            }
        }

        // 順位を付け終えてから終えたいので、ModeSession の終わりではなく BattleRoyaleSession.Ended で EndMatch を呼ぶ。
        protected override bool EndsMatchWithSession => false;

        protected override string SessionEndedLog(Side? winner)
        {
            return null;
        }

        protected override void OnStopping()
        {
            Unsubscribe();
            ReleaseInteractAction();
            StopZone();
            ClearErrands();
            foreach (var member in Members)
            {
                var drop = member != null ? member.GetComponent<DropOnDeath>() : null;
                if (drop != null)
                {
                    drop.DecideDrops = null;
                }
            }

            _localReviveTarget = null;
            _loggedErrands.Clear();
            _zoneTargets.Clear();
            _battleRoyale = null;
        }

        private ActionRules ResolveActions()
        {
            var bootstrap = FindAnyObjectByType<GameBootstrap>();
            var rules = bootstrap != null && bootstrap.World != null ? bootstrap.World.Rules : null;
            if (rules == null)
            {
                return ActionRules.Default;
            }

            if (rules.Actions.Downed != Downed.Revive)
            {
                Debug.LogWarning($"{LogTag} ルールのダウンが {rules.Actions.Downed} ですが、バトルロイヤルはダウンを使うので Revive として扱います。", this);
            }

            return rules.Actions;
        }

        private void StartZone()
        {
            var zone = _zone != null ? _zone : FindAnyObjectByType<ShrinkingZone>();
            if (zone == null)
            {
                Debug.LogWarning($"{LogTag} ShrinkingZone がシーンにないため、エリアは縮みません。", this);
                return;
            }

            zone.SetTargets(_zoneTargets);
            if (zone.Begin())
            {
                _runningZone = zone;
            }
        }

        private void StopZone()
        {
            if (_runningZone != null)
            {
                _runningZone.Stop();
                _runningZone.SetTargets(null);
            }

            _runningZone = null;
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
        }

        /// <summary>
        /// member に <see cref="ParachuteMovement"/> があれば、出現した位置の <see cref="DropHeight"/> 上でパラシュートを開かせ、結果を <c>[Mode] drop</c> のログに出す。
        /// 開けなければ (ルールが Off など) 地面に残して警告を出す。無い参加者 (NavMesh で動く bot) は地面から始める。
        /// </summary>
        private void DropByParachute(SideMember member)
        {
            var parachute = member.GetComponent<ParachuteMovement>();
            if (parachute == null || !parachute.IsInitialized)
            {
                return;
            }

            var height = EffectiveDropHeight;
            var verdict = parachute.TryOpenAt(member.transform.position + (Vector3.up * height));
            var line = BattleRoyaleLog.Drop(IdOf(member), height, verdict);
            if (verdict == ParachuteOpenVerdict.Open)
            {
                Debug.Log(line, member);
            }
            else
            {
                Debug.LogWarning($"{line} (パラシュートを開けないため、地面から始めます)", member);
            }
        }

        private void Disarm(SideMember member)
        {
            var controller = member.GetComponentInChildren<WeaponController>(true);
            if (controller != null && controller.IsInitialized)
            {
                controller.Disarm();
            }

            var agent = member.GetComponent<EnemyAgent>();
            if (agent != null)
            {
                agent.Disarm();
            }
        }

        /// <summary>
        /// member に DropOnDeath を付け (無ければ)、倒れたとき (ダウンも含む) には落とさないようにする。
        /// 戻らなくなったときに <see cref="DropBelongings"/> が持ち物を落とす。
        /// </summary>
        private static void PrepareDrops(SideMember member)
        {
            var drop = member.GetComponent<DropOnDeath>();
            if (drop == null)
            {
                drop = member.gameObject.AddComponent<DropOnDeath>();
            }

            drop.DecideDrops = (context, drops) => drops.Clear();
        }

        /// <summary>member が持っていた武器 (今の予備弾を持っていたときに足す弾にして) と、DropOnDeath の Drops を足元に落とし、武器を手放させる。</summary>
        private void DropBelongings(SideMember member)
        {
            if (member == null)
            {
                return;
            }

            var drop = member.GetComponent<DropOnDeath>();
            if (drop == null)
            {
                return;
            }

            var contents = new List<PickupContents>();
            var controller = member.GetComponentInChildren<WeaponController>(true);
            if (controller != null && controller.IsInitialized && !controller.IsUnarmed)
            {
                var held = controller.HeldDefinitions;
                var items = controller.Loadout.Items;
                for (var i = 0; i < held.Count && i < items.Count; i++)
                {
                    contents.Add(PickupContents.ForWeapon(held[i], AmmoToDrop(items[i])).WithAttachments(controller.AttachmentsOf(items[i].Spec.Id)));
                }
            }

            var agent = member.GetComponent<EnemyAgent>();
            if (agent != null && agent.Weapon != null && agent.WeaponDefinition != null)
            {
                contents.Add(PickupContents.ForWeapon(agent.WeaponDefinition, AmmoToDrop(agent.Weapon)).WithAttachments(agent.HeldAttachments));
            }

            foreach (var extra in drop.Drops)
            {
                if (extra != null)
                {
                    contents.Add(extra.Clone());
                }
            }

            drop.Drop(contents, member.GroundPosition);
            Disarm(member);
        }

        private static int AmmoToDrop(Weapon weapon)
        {
            return weapon.HasInfiniteReserve ? weapon.Spec.ReserveAmmo : weapon.ReserveAmmo;
        }

        private void CollectOptions(SideMember member, EnemyAgent agent, IReadOnlyList<Pickup> pickups)
        {
            _pickupOptions.Clear();
            var collector = Pickup.Describe(member.Damageable);
            foreach (var pickup in pickups)
            {
                // Interact の物は bot が触れても拾えないので、行き先にすると着いたまま動かなくなる。
                if (pickup.CollectMode != PickupCollectMode.Touch)
                {
                    continue;
                }

                PickupOutcome outcome;
                try
                {
                    outcome = PickupJudge.Judge(pickup.Spec, collector);
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    // 中身の不正は Pickup 自身が拾われるときにエラーを出すので、ここでは候補から外すだけにする。
                    continue;
                }

                // 武器を持っている bot は 1 本しか持たないので、別の武器を取りに行くと持ち替えを繰り返してしまう。
                if (!outcome.IsCollected || (outcome.AddsWeapon && !agent.IsUnarmed))
                {
                    continue;
                }

                _pickupOptions.Add(new BotPickupOption(pickup.transform.position.ToNumerics(), outcome.AddsWeapon));
            }
        }

        private void CollectDownedSquadmates(SideMember member)
        {
            _downedSquadmates.Clear();
            foreach (var id in _battleRoyale.GetDownedIds())
            {
                var other = MemberOf(id);
                if (other != null && other != member && other.Side == member.Side)
                {
                    _downedSquadmates.Add(other.GroundPosition.ToNumerics());
                }
            }
        }

        private int NearestDownedSquadmate(SideMember actor, float range)
        {
            var from = actor.GroundPosition.ToNumerics();
            var nearest = -1;
            var nearestDistance = float.PositiveInfinity;
            foreach (var targetId in _battleRoyale.GetDownedIds())
            {
                var target = MemberOf(targetId);
                if (target == null || target == actor || target.Side != actor.Side)
                {
                    continue;
                }

                var to = target.GroundPosition.ToNumerics();
                if (!ReviveReach.IsWithin(from, to, range))
                {
                    continue;
                }

                var distance = System.Numerics.Vector3.DistanceSquared(from, to);
                if (distance < nearestDistance)
                {
                    nearest = targetId;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void Subscribe()
        {
            if (_subscribed || _battleRoyale == null)
            {
                return;
            }

            _battleRoyale.PlayerDowned += OnPlayerDowned;
            _battleRoyale.PlayerRevived += OnPlayerRevived;
            _battleRoyale.PlayerBledOut += OnPlayerBledOut;
            _battleRoyale.PlayerFinished += OnPlayerFinished;
            _battleRoyale.SquadEliminated += OnSquadEliminated;
            _battleRoyale.Ended += OnBattleRoyaleEnded;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _battleRoyale == null)
            {
                _subscribed = false;
                return;
            }

            _battleRoyale.PlayerDowned -= OnPlayerDowned;
            _battleRoyale.PlayerRevived -= OnPlayerRevived;
            _battleRoyale.PlayerBledOut -= OnPlayerBledOut;
            _battleRoyale.PlayerFinished -= OnPlayerFinished;
            _battleRoyale.SquadEliminated -= OnSquadEliminated;
            _battleRoyale.Ended -= OnBattleRoyaleEnded;
            _subscribed = false;
        }

        private void OnPlayerDowned(int playerId, int? downerId)
        {
            Debug.Log(BattleRoyaleLog.Downed(playerId, downerId), MemberOf(playerId));
        }

        private void OnPlayerRevived(int reviverId, int playerId)
        {
            var member = MemberOf(playerId);
            Debug.Log(BattleRoyaleLog.Revived(reviverId, playerId), member);
            if (member == null)
            {
                return;
            }

            member.Damageable.SetDowned(false);
            member.Revive(_battleRoyale.RevivedHealth(member.Damageable.MaxHealth));
            member.SetActing(true);
        }

        private void OnPlayerBledOut(int playerId, int? creditId)
        {
            var member = MemberOf(playerId);
            Debug.Log(BattleRoyaleLog.BledOut(playerId, creditId), member);
            Fall(member);
        }

        private void OnPlayerFinished(int playerId, int? creditId)
        {
            var member = MemberOf(playerId);
            Debug.Log(BattleRoyaleLog.SquadWiped(playerId, creditId), member);
            Fall(member);
        }

        private void Fall(SideMember member)
        {
            if (member == null)
            {
                return;
            }

            member.Damageable.SetDowned(false);
            DropBelongings(member);
        }

        private void OnSquadEliminated(Side squad, int place)
        {
            Debug.Log(BattleRoyaleLog.Eliminated(squad, place, _battleRoyale.SquadsRemaining), this);
        }

        private void OnBattleRoyaleEnded(Side? winner)
        {
            StopZone();
            ClearErrands();
            EndMatch(winner);
            Debug.Log(BattleRoyaleLog.Placements(_battleRoyale), this);
        }

        /// <summary>
        /// Interact を有効にする。OnEnable / OnDisable を書くと基底の同名のメッセージ (再開の入力の出し入れ) が呼ばれなくなるので、
        /// 始めたときに有効にし、<see cref="ModeRunnerBase.Stop"/> (破棄時) で戻す。
        /// </summary>
        private void AcquireInteractAction()
        {
            // Edit Mode では破棄で無効に戻せないことがあるので、Play 中だけ有効にする。
            if (!Application.isPlaying)
            {
                return;
            }

            if (_interactAction == null)
            {
                var actions = InputSystem.actions;
                _interactAction = actions != null ? actions.FindAction(InteractActionPath) : null;
                if (_interactAction == null)
                {
                    Debug.LogError($"{LogTag} Input Actions に '{InteractActionPath}' がないため、味方を起こせません。", this);
                    return;
                }
            }

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteractAction = true;
            }
        }

        private void ReleaseInteractAction()
        {
            if (_enabledInteractAction && _interactAction != null)
            {
                _interactAction.Disable();
            }

            _enabledInteractAction = false;
        }
    }
}
