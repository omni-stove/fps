using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// エクスキューション (<see cref="ExecutionSession"/>) をシーンで進める。2 チームのラウンド制で、ラウンド中は戻らない。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>参加者の体力が 0 になったら倒さずにダウンさせ (<see cref="Damageable.IsDowned"/>)、操作を止める。
    /// とどめを刺されるか出血で倒れるまで、ラウンドからは外れない。</item>
    /// <item>プレイヤーは Interact (キーボードは E、ゲームパッドは Y) を押し続けると、<see cref="InteractRange"/> 以内でいちばん近いダウン中の人に
    /// 敵ならとどめを刺し (<see cref="ExecuteSeconds"/> 秒)、味方なら起こす (ルールの <see cref="ActionRules.ReviveSeconds"/> 秒)。
    /// 起き上がった人は最大体力の <see cref="ActionRules.DownedHealthFraction"/> でその場に立つ。</item>
    /// <item>ラウンドの待ちに入るたびに、全員を自陣営の出現点に 1 人ずつ並べ直して体力を満たし、待ちの間は操作を止める。</item>
    /// <item>HUD の得点の欄に両チームのラウンドの勝ち数、その下にラウンドの番号・待ちの残り・両チームの残り人数・自分のダウンや長押しの進みを出す。
    /// 結果は先取数に届いたときだけ出し、そこで R (ゲームパッドは X) で読み直せる。</item>
    /// <item>ダウン・ルールはシーンの <see cref="GameBootstrap"/> のルールから読む。ダウンを使わないルールでも、このモードはダウンを使う。</item>
    /// </list>
    /// </remarks>
    public sealed class ExecutionRunner : ModeRunnerBase
    {
        public const int DefaultWinsToWin = ExecutionSession.DefaultWinsToWin;
        public const float DefaultPreparationSeconds = ExecutionSession.DefaultPreparationSeconds;
        public const float DefaultRoundTimeLimitSeconds = ExecutionSession.DefaultRoundTimeLimitSeconds;
        public const float DefaultExecuteSeconds = ExecutionSession.DefaultExecuteSeconds;

        /// <summary>
        /// ダウン中の人にインタラクトできる距離 (m) の既定値。足元どうしで測る。ダウン中の体は倒れて横に伸びるので、
        /// 立った人どうしで測るネット対戦の蘇生 (<see cref="ReviveReach.DefaultRange"/>) より少し長くする。
        /// </summary>
        public const float DefaultInteractRange = 2f;

        /// <summary>Assets/InputSystem_Actions.inputactions の Interact アクション。</summary>
        public const string InteractActionPath = InputActionPaths.Interact;

        [SerializeField, Min(1), Tooltip("試合に勝つのに要るラウンドの勝ち数")]
        private int _winsToWin = DefaultWinsToWin;

        [SerializeField, Min(0f), Tooltip("各ラウンドの開始前の待ち (秒)")]
        private float _preparationSeconds = DefaultPreparationSeconds;

        [SerializeField, Min(1f), Tooltip("1 ラウンドの制限時間 (秒)。切れたらダウンしていない人の多いチームの勝ち")]
        private float _roundTimeLimitSeconds = DefaultRoundTimeLimitSeconds;

        [SerializeField, Min(0.1f), Tooltip("とどめを刺すのに Interact を押し続ける秒数")]
        private float _executeSeconds = DefaultExecuteSeconds;

        [SerializeField, Min(0.1f), Tooltip("ダウン中の人にインタラクトできる距離 (m)。足元どうしで測る")]
        private float _interactRange = DefaultInteractRange;

        private ExecutionSession _execution;
        private bool _subscribed;
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private int? _localInteractTarget;

        /// <summary>進めているエクスキューション。開始前は null。</summary>
        public ExecutionSession Execution => _execution;

        /// <summary>この画面で操作しているプレイヤーがいまインタラクトを押している相手。押していなければ null。</summary>
        public SideMember LocalInteractTarget => _localInteractTarget.HasValue ? MemberOf(_localInteractTarget.Value) : null;

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

        public float InteractRange
        {
            get => _interactRange;
            set => _interactRange = value;
        }

        /// <summary>
        /// エクスキューションを作る。actions のダウンが Off なら Revive にして使う (このモードはダウンが前提)。値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
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

        /// <summary>
        /// execution をこの Runner で始める (<see cref="ModeRunnerBase.Begin"/>)。Edit Mode のテストと、シーンを使わずに始めるときに使う。
        /// execution は始める前で参加者がいないこと。
        /// </summary>
        public void Begin(
            ExecutionSession execution,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null,
            IModeHud hudView = null)
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
                Begin(execution.Mode, members, spawnPoints, localPlayer, hudView);
            }
            catch
            {
                Unsubscribe();
                _execution = null;
                throw;
            }
        }

        /// <summary>
        /// actor が、<see cref="InteractRange"/> 以内でいちばん近いダウン中の人へのインタラクトを deltaSeconds だけ押した。
        /// 誰もいなければ <see cref="ExecutionStep.Rejected"/>。Play 中はプレイヤーが Interact を押している間、Tick の前に呼ばれる。
        /// </summary>
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
            _execution = CreateExecution(_winsToWin, _preparationSeconds, _roundTimeLimitSeconds, ResolveActions(), _executeSeconds);
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

        protected override IReadOnlyList<ModeHudScoreLine> HudScores(Side? ownSide)
        {
            return ExecutionHudText.RoundWins(_execution, ownSide);
        }

        protected override string HudResult()
        {
            return ExecutionHudText.Result(_execution);
        }

        protected override void AppendHudLines(List<string> lines)
        {
            var local = LocalPlayer;
            lines.AddRange(ExecutionHudText.Lines(_execution, local != null ? IdOf(local) : (int?)null, _localInteractTarget));
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
            var nearest = -1;
            var nearestDistance = float.PositiveInfinity;
            foreach (var targetId in _execution.GetDownedIds())
            {
                var target = MemberOf(targetId);
                if (target == null || target == actor)
                {
                    continue;
                }

                var to = target.GroundPosition.ToNumerics();
                if (!ReviveReach.IsWithin(from, to, _interactRange))
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
        }

        private void OnRoundEnded(Side? winner)
        {
            Debug.Log(ExecutionLog.RoundEnded(_execution.Round, winner), this);
        }

        private void OnMatchEnded(Side winner)
        {
            Debug.Log(ModeLog.Ended(winner), this);
        }

        /// <summary>
        /// 全員を自陣営の出現点に 1 人ずつ (ID の順に、出現点はシーンの並びで) 並べ直し、体力を満たしてダウンを消し、待ちの間は操作を止める。
        /// 人数が出現点より多ければ先頭から重ねて使い、自陣営の出現点が無ければその場で体力だけ戻す。
        /// </summary>
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
