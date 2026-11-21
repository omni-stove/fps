using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Shooter.Runtime
{
    public sealed class ExecutionRunner : ModeRunnerBase
    {
        public const int DefaultWinsToWin = ExecutionSession.DefaultWinsToWin;
        public const float DefaultPreparationSeconds = ExecutionSession.DefaultPreparationSeconds;
        public const float DefaultRoundTimeLimitSeconds = ExecutionSession.DefaultRoundTimeLimitSeconds;
        public const float DefaultExecuteSeconds = ExecutionSession.DefaultExecuteSeconds;

        public const float DefaultExecuteRange = ExecutionSpec.DefaultExecuteRange;

        public const string InteractActionPath = InputActionPaths.Interact;

        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の勝ち数・待ち・制限時間・とどめの秒数と距離の代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private ExecutionDefinition _definition;

        [SerializeField, Min(1), Tooltip("試合に勝つのに要るラウンドの勝ち数")]
        private int _winsToWin = DefaultWinsToWin;

        [SerializeField, Min(0f), Tooltip("各ラウンドの開始前の待ち (秒)")]
        private float _preparationSeconds = DefaultPreparationSeconds;

        [SerializeField, Min(1f), Tooltip("1 ラウンドの制限時間 (秒)。切れたらダウンしていない人の多いチームの勝ち")]
        private float _roundTimeLimitSeconds = DefaultRoundTimeLimitSeconds;

        [SerializeField, Min(0.1f), Tooltip("とどめを刺すのに Interact を押し続ける秒数")]
        private float _executeSeconds = DefaultExecuteSeconds;

        // 蘇生とも兼ねていた欄の名前。シーンに入っていた値は、とどめの距離として引き継ぐ。
        [SerializeField, FormerlySerializedAs("_interactRange"), Min(0.1f), Tooltip("ダウン中の敵にとどめを刺せる距離 (m)。足元どうしで測る。味方を起こせる距離はルールの reviveRange")]
        private float _executeRange = DefaultExecuteRange;

        [SerializeField, Tooltip("各ラウンドが始まった直後にダウンさせる参加者。ダウン・蘇生・とどめをすぐ試せるシーンに使う。空なら誰もダウンさせない")]
        private List<SideMember> _downedAtRoundStart = new List<SideMember>();

        private ExecutionSession _execution;
        private bool _subscribed;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private int? _localInteractTarget;

        public ExecutionSession Execution => _execution;

        public SideMember LocalInteractTarget => _localInteractTarget.HasValue ? MemberOf(_localInteractTarget.Value) : null;

        public ExecutionDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public ExecutionDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.Execution);

        public float EffectiveExecuteRange
        {
            get
            {
                var definition = EffectiveDefinition;
                return definition != null ? definition.ExecuteRange : _executeRange;
            }
        }

        public int WinsToWin
        {
            get => _winsToWin;
            set => _winsToWin = value;
        }

        public float PreparationSeconds
        {
            get => _preparationSeconds;
            set => _preparationSeconds = value;
        }

        public float RoundTimeLimitSeconds
        {
            get => _roundTimeLimitSeconds;
            set => _roundTimeLimitSeconds = value;
        }

        public float ExecuteSeconds
        {
            get => _executeSeconds;
            set => _executeSeconds = value;
        }

        public float ExecuteRange
        {
            get => _executeRange;
            set => _executeRange = value;
        }

        public List<SideMember> DownedAtRoundStart
        {
            get => _downedAtRoundStart ??= new List<SideMember>();
            set => _downedAtRoundStart = value ?? new List<SideMember>();
        }

        // このモードはダウンが前提なので、actions のダウンが Off でも Revive にして使う。
        public static ExecutionSession CreateExecution(
            int winsToWin, float preparationSeconds, float roundTimeLimitSeconds, ActionRules actions, float executeSeconds)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            var withDowned = actions.Downed == Downed.Revive ? actions : actions.WithDowned(Downed.Revive);
            return new ExecutionSession(winsToWin, preparationSeconds, roundTimeLimitSeconds, withDowned, executeSeconds);
        }

        public ExecutionSession CreateConfiguredExecution(ActionRules actions)
        {
            var definition = EffectiveDefinition;
            return definition != null
                ? definition.CreateExecution(actions)
                : CreateExecution(_winsToWin, _preparationSeconds, _roundTimeLimitSeconds, actions, _executeSeconds);
        }

        public void Begin(
            ExecutionSession execution,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (execution == null)
            {
                throw new ArgumentNullException(nameof(execution));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{GetType().Name} '{name}' has already started a mode");
            }

            _execution = execution;
            try
            {
                Begin(execution.Mode, members, spawnPoints, localPlayer);
            }
            catch
            {
                Unsubscribe();
                _execution = null;
                throw;
            }
        }

        public ExecutionStep HoldInteract(SideMember actor, float deltaSeconds)
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

            var targetId = NearestDowned(actor);
            if (targetId < 0)
            {
                return ExecutionStep.Rejected;
            }

            var step = _execution.Interact(actorId, targetId, deltaSeconds);
            if (actor == LocalPlayer && (step == ExecutionStep.Reviving || step == ExecutionStep.Executing))
            {
                _localInteractTarget = targetId;
            }

            return step;
        }

        protected override ModeSession CreateSession()
        {
            _execution = CreateConfiguredExecution(ResolveActions());
            return _execution.Mode;
        }

        protected override void ValidateSession(ModeSession session)
        {
            if (_execution == null || _execution.Mode != session)
            {
                throw new ArgumentException(
                    $"{GetType().Name} runs an ExecutionSession; start it with Begin(ExecutionSession, ...) instead of a bare ModeSession",
                    nameof(session));
            }
        }

        protected override void StartSession(ModeSession session)
        {
            Subscribe();
            AcquireInteractAction();
            _execution.Start();
        }

        protected override void AdvanceSession(float deltaSeconds)
        {
            _execution.Tick(deltaSeconds);
        }

        protected override void OnBeforeTick(float deltaSeconds)
        {
            _localInteractTarget = null;
            var local = LocalPlayer;
            if (local == null || _interactAction == null || !_interactAction.IsPressed() || !_execution.IsLive)
            {
                return;
            }

            HoldInteract(local, deltaSeconds);
        }

        protected override bool InterceptDeath(SideMember killer, SideMember victim)
        {
            var victimId = IdOf(victim);
            var killerId = IdOf(killer);
            var hit = _execution.ReportLethal(victimId, killerId >= 0 ? killerId : (int?)null);
            if (hit == ExecutionHit.Downed)
            {
                victim.Damageable.SetDowned(true);
            }
            else if (hit == ExecutionHit.Killed)
            {
                Debug.Log(killerId >= 0 ? ModeLog.Kill(killerId, victimId) : ModeLog.Death(victimId), victim);
            }

            // ダウンも倒れたこともここで報告し終えたので、ModeRunnerBase の報告はさせない。
            return true;
        }

        protected override bool OffersInteract
        {
            get
            {
                var local = LocalPlayer;
                if (_execution == null || !_execution.IsLive || local == null)
                {
                    return false;
                }

                var localId = IdOf(local);
                return localId >= 0 && !_execution.Mode.IsDown(localId) && !_execution.IsDowned(localId) && NearestDowned(local) >= 0;
            }
        }

        protected override object HudDetail
        {
            get
            {
                if (_execution == null)
                {
                    return null;
                }

                var local = LocalPlayer;
                return new ExecutionStatus(_execution, local != null ? IdOf(local) : (int?)null, _localInteractTarget);
            }
        }

        protected override string SessionEndedLog(Side? winner)
        {
            // ModeSession が終わるのはラウンドの終わり。区切りは RoundEnded と Ended で出す。
            return null;
        }

        protected override void OnStopping()
        {
            Unsubscribe();
            ReleaseInteractAction();
            _localInteractTarget = null;
            _execution = null;
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
                Debug.LogWarning($"{LogTag} ルールのダウンが {rules.Actions.Downed} ですが、エクスキューションはダウンを使うので Revive として扱います。", this);
            }

            return rules.Actions;
        }

        private int NearestDowned(SideMember actor)
        {
            var from = actor.GroundPosition.ToNumerics();
            var actorSide = _execution.Mode.GetSide(IdOf(actor));
            var nearest = -1;
            var nearestDistance = float.PositiveInfinity;
            var executeRange = EffectiveExecuteRange;
            foreach (var targetId in _execution.GetDownedIds())
            {
                var target = MemberOf(targetId);
                if (target == null || target == actor)
                {
                    continue;
                }

                var to = target.GroundPosition.ToNumerics();
                var range = _execution.Mode.GetSide(targetId) == actorSide ? _execution.Actions.ReviveRange : executeRange;
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
            if (_subscribed || _execution == null)
            {
                return;
            }

            _execution.PlayerDowned += OnPlayerDowned;
            _execution.PlayerRevived += OnPlayerRevived;
            _execution.PlayerExecuted += OnPlayerExecuted;
            _execution.PlayerBledOut += OnPlayerBledOut;
            _execution.Round.PreparationStarted += OnPreparationStarted;
            _execution.Round.RoundStarted += OnRoundStarted;
            _execution.Round.RoundEnded += OnRoundEnded;
            _execution.Round.Ended += OnMatchEnded;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _execution == null)
            {
                _subscribed = false;
                return;
            }

            _execution.PlayerDowned -= OnPlayerDowned;
            _execution.PlayerRevived -= OnPlayerRevived;
            _execution.PlayerExecuted -= OnPlayerExecuted;
            _execution.PlayerBledOut -= OnPlayerBledOut;
            _execution.Round.PreparationStarted -= OnPreparationStarted;
            _execution.Round.RoundStarted -= OnRoundStarted;
            _execution.Round.RoundEnded -= OnRoundEnded;
            _execution.Round.Ended -= OnMatchEnded;
            _subscribed = false;
        }

        private void OnPlayerDowned(int playerId, int? downerId)
        {
            Debug.Log(ExecutionLog.Downed(playerId, downerId), MemberOf(playerId));
        }

        private void OnPlayerRevived(int reviverId, int playerId)
        {
            var member = MemberOf(playerId);
            Debug.Log(ExecutionLog.Revived(reviverId, playerId), member);
            if (member == null)
            {
                return;
            }

            member.Damageable.SetDowned(false);
            member.Revive(_execution.RevivedHealth(member.Damageable.MaxHealth));
            member.SetActing(true);
        }

        private void OnPlayerExecuted(int executorId, int victimId)
        {
            var victim = MemberOf(victimId);
            Debug.Log(ExecutionLog.Executed(executorId, victimId), victim);
            if (victim != null)
            {
                victim.Damageable.SetDowned(false);
            }
        }

        private void OnPlayerBledOut(int playerId, int? creditId)
        {
            var member = MemberOf(playerId);
            Debug.Log(ExecutionLog.BledOut(playerId, creditId), member);
            if (member != null)
            {
                member.Damageable.SetDowned(false);
            }
        }

        private void OnPreparationStarted(int roundNumber)
        {
            Debug.Log(ExecutionLog.Preparing(roundNumber), this);
            LineUp();
        }

        private void OnRoundStarted(int roundNumber)
        {
            Debug.Log(ExecutionLog.Live(roundNumber), this);
            foreach (var member in Members)
            {
                if (member != null)
                {
                    member.SetActing(true);
                }
            }

            DownOpeningMembers();
        }

        // 倒れたときと同じ道 (Died → InterceptDeath) を通し、体を止めることもダウンの記録も一か所にまとめる。
        private void DownOpeningMembers()
        {
            foreach (var member in DownedAtRoundStart)
            {
                if (member == null || member.Damageable == null || IdOf(member) < 0)
                {
                    continue;
                }

                member.Damageable.Kill();
            }
        }

        private void OnRoundEnded(Side? winner)
        {
            Debug.Log(ExecutionLog.RoundEnded(_execution.Round, winner), this);
        }

        private void OnMatchEnded(Side winner)
        {
            Debug.Log(ModeLog.Ended(winner), this);
        }

        private void LineUp()
        {
            var nextIndex = new Dictionary<Side, int>();
            foreach (var member in Members)
            {
                if (member == null)
                {
                    continue;
                }

                var side = member.Side;
                var points = new List<SideSpawnPoint>();
                foreach (var point in SpawnPoints)
                {
                    if (point != null && point.Side == side)
                    {
                        points.Add(point);
                    }
                }

                member.Damageable.SetDowned(false);
                if (points.Count == 0)
                {
                    Respawn(member);
                }
                else
                {
                    nextIndex.TryGetValue(side, out var index);
                    var point = points[index % points.Count];
                    nextIndex[side] = index + 1;
                    RespawnAt(member, point.Position, point.Rotation);
                }

                member.SetActing(false);
            }
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
                    Debug.LogError($"{LogTag} Input Actions に '{InteractActionPath}' がないため、とどめと蘇生ができません。", this);
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
