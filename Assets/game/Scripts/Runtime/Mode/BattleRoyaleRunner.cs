using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // ダウンしただけでは持ち物を落とさないのは、起こされたら持ったまま戦うため。
    public sealed class BattleRoyaleRunner : ModeRunnerBase
    {
        public const int DefaultSquadCount = BattleRoyaleSession.DefaultSquadCount;
        public const int DefaultSquadSize = BattleRoyaleSession.DefaultSquadSize;

        // 既定は制限なしにし、縮むエリアで決着させる。
        public const float DefaultTimeLimitSeconds = 0f;

        // 毎フレーム拾える物を探すと重いので、bot の用事を決め直すのは間をあける。
        public const float DefaultBotPlanIntervalSeconds = 0.5f;

        public const string InteractActionPath = InputActionPaths.Interact;

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

        public BattleRoyaleSession BattleRoyale => _battleRoyale;

        public override bool HasSquads => true;

        public ShrinkingZone RunningZone => _runningZone;

        public SideMember LocalReviveTarget => _localReviveTarget.HasValue ? MemberOf(_localReviveTarget.Value) : null;

        public BattleRoyaleDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public BattleRoyaleDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.BattleRoyale);

        public float EffectiveBotPlanIntervalSeconds
        {
            get
            {
                var definition = EffectiveDefinition;
                return definition != null ? definition.BotPlanIntervalSeconds : _botPlanIntervalSeconds;
            }
        }

        public float EffectiveBotSearchRadius
        {
            get
            {
                var definition = EffectiveDefinition;
                return definition != null ? definition.BotSearchRadius : _botSearchRadius;
            }
        }

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

        public ShrinkingZone Zone
        {
            get => _zone;
            set => _zone = value;
        }

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

        // このモードはダウンが前提なので、actions のダウンが Off でも Revive にして使う。
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

        public BattleRoyaleSession CreateConfiguredBattleRoyale(ActionRules actions)
        {
            var definition = EffectiveDefinition;
            return definition != null
                ? definition.CreateBattleRoyale(actions)
                : CreateBattleRoyale(_squadCount, _squadSize, _timeLimitSeconds, actions);
        }

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

        private static void PrepareDrops(SideMember member)
        {
            var drop = member.GetComponent<DropOnDeath>();
            if (drop == null)
            {
                drop = member.gameObject.AddComponent<DropOnDeath>();
            }

            drop.DecideDrops = (context, drops) => drops.Clear();
        }

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

        // OnEnable / OnDisable を書くと基底の同名のメッセージ (再開の入力の出し入れ) が呼ばれなくなるので、
        // 始めたときに有効にし、ModeRunnerBase.Stop (破棄時) で戻す。
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
