using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // RespawnReady を受けてもすぐには戻さず、次の Tick の終わりに戻すのは、ダメージの処理の途中で生き返らせないため。
    [DisallowMultipleComponent]
    public abstract class ModeRunnerBase : MonoBehaviour
    {
        public const string LogTag = ModeLog.Tag;

        public const int LocalPlayerId = 0;

        // 終了後は全員の WeaponController を止めるので、再開の入力をリロードと同じにしても取り合わない。
        public const string RestartActionPath = InputActionPaths.Reload;

        [SerializeField, Min(0), Tooltip("GameBootstrap が作るプレイヤーに SideMember が無いとき、付ける陣営の ID")]
        private int _playerSideId;

        [SerializeField, Tooltip("GameBootstrap が並べる標的ダミーを隠すか (モードの参加者ではない)")]
        private bool _hideTargetDummies = true;

        private readonly List<SideMember> _members = new List<SideMember>();
        private readonly Dictionary<Damageable, int> _ids = new Dictionary<Damageable, int>();
        private readonly List<SideSpawnPoint> _spawnPoints = new List<SideSpawnPoint>();
        private readonly Queue<int> _pendingRespawns = new Queue<int>();
        private readonly HashSet<Side> _warnedMissingSpawn = new HashSet<Side>();
        private ModeSession _session;
        private SideMember _localPlayer;
        private ModeStatus _publishedStatus;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _restartAction;
        private bool _enabledRestartAction;
        private bool _matchOver;
        private Side? _matchWinner;

        public ModeSession Session => _session;

        public bool IsStarted => _session != null;

        public bool IsMatchOver => _matchOver;

        protected virtual bool EndsMatchWithSession => true;

        public virtual bool HasSquads => false;

        protected IReadOnlyList<SideSpawnPoint> SpawnPoints => _spawnPoints;

        public IReadOnlyList<SideMember> Members => _members;

        public SideMember LocalPlayer => _localPlayer;

        public ModeStatus Status => _publishedStatus;

        public InputAction RestartAction => _restartAction;

        public Side PlayerSide
        {
            get => new Side(Math.Max(0, _playerSideId));
            set => _playerSideId = value.Id;
        }

        public bool HidesTargetDummies
        {
            get => _hideTargetDummies;
            set => _hideTargetDummies = value;
        }

        protected abstract ModeSession CreateSession();

        protected virtual void OnBegan()
        {
        }

        protected virtual void OnKill(SideMember killer, SideMember victim)
        {
        }

        protected virtual void OnSpawned(SideMember member)
        {
        }

        protected virtual void OnEnded(Side? winner)
        {
        }

        protected virtual void OnBeforeTick(float deltaSeconds)
        {
        }

        protected virtual object HudDetail => null;

        protected virtual void ValidateSession(ModeSession session)
        {
        }

        protected virtual void StartSession(ModeSession session)
        {
            session.Start();
        }

        protected virtual void AdvanceSession(float deltaSeconds)
        {
            TickSession(_session, deltaSeconds);
        }

        protected virtual void TickSession(ModeSession session, float deltaSeconds)
        {
            session.Tick(deltaSeconds);
        }

        protected virtual bool InterceptDeath(SideMember killer, SideMember victim)
        {
            return false;
        }

        protected virtual string SessionEndedLog(Side? winner)
        {
            return ModeLog.Ended(winner);
        }

        protected virtual void OnStopping()
        {
            OnStop();
        }

        // Unity のメッセージ (OnDisable など) を派生クラスで定義すると土台のものが呼ばれなくなるので、派生の購読の解除はこちらか OnStopping で行う。
        protected virtual void OnStop()
        {
        }

        public int IdOf(SideMember member)
        {
            if (member == null || member.Damageable == null)
            {
                return -1;
            }

            return _ids.TryGetValue(member.Damageable, out var id) ? id : -1;
        }

        public SideMember MemberOf(int playerId)
        {
            return playerId >= 0 && playerId < _members.Count ? _members[playerId] : null;
        }

        private void Start()
        {
            if (!IsStarted && !TryBegin())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            // 終了後に無効→有効にされたら、再開の入力をもう一度受け付ける。
            if (IsStarted && _matchOver)
            {
                AcquireRestartAction();
            }
        }

        private void OnDisable()
        {
            ReleaseRestartAction();
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void Update()
        {
            if (!IsStarted)
            {
                return;
            }

            Tick(Time.deltaTime);

            if (_session != null && _matchOver && _restartAction != null && _restartAction.WasPressedThisFrame())
            {
                Restart();
            }
        }

        public bool TryBegin()
        {
            if (IsStarted)
            {
                return true;
            }

            var bootstrap = FindAnyObjectByType<GameBootstrap>();
            var world = bootstrap != null ? bootstrap.World : null;
            if (bootstrap != null && world == null)
            {
                Debug.LogError($"{LogTag} GameBootstrap '{bootstrap.name}' がプレイヤーを生成していません。上のエラーを確認してください。", this);
                return false;
            }

            SideMember localPlayer = null;
            if (world != null && world.Player != null)
            {
                localPlayer = world.Player.GetComponent<SideMember>();
                if (localPlayer == null)
                {
                    localPlayer = world.Player.AddComponent<SideMember>();
                    localPlayer.Side = PlayerSide;
                }
            }

            // 再開できなくてもモード自体は進められるので、解決に失敗してもエラーを出して続ける。
            _restartAction = ResolveRestartAction();

            ModeSession session;
            try
            {
                session = CreateSession();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogError($"{LogTag} モードを組み立てられません: {e.Message}", this);
                return false;
            }

            if (session == null)
            {
                Debug.LogError($"{LogTag} {GetType().Name}.CreateSession が null を返しました。", this);
                return false;
            }

            try
            {
                Begin(session, SideMember.FindAll(), SideSpawnPoint.FindAll(), localPlayer);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} モードを始められません: {e.Message}", this);
                return false;
            }

            // 標的ダミーは SideMember を持たず得点にもならないのに、プレイヤーの正面 (出現点の前) に立って射線を塞ぐ。
            if (_hideTargetDummies && world != null && world.TargetRoot != null)
            {
                world.TargetRoot.SetActive(false);
            }

            return true;
        }

        public void Begin(
            ModeSession session,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            if (spawnPoints == null)
            {
                throw new ArgumentNullException(nameof(spawnPoints));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{GetType().Name} '{name}' has already started a mode");
            }

            if (session.State != GameModeState.Waiting)
            {
                throw new ArgumentException($"session must be Waiting, but is {session.State}", nameof(session));
            }

            if (session.PlayerCount != 0)
            {
                throw new ArgumentException("session must have no players; Begin adds the scene's SideMembers", nameof(session));
            }

            ValidateSession(session);

            var ordered = OrderMembers(members, localPlayer);
            if (ordered.Count == 0)
            {
                throw new ArgumentException("there is no SideMember to join the mode", nameof(members));
            }

            for (var id = 0; id < ordered.Count; id++)
            {
                var member = ordered[id];
                try
                {
                    session.AddPlayer(id, member.Side);
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    throw new ArgumentException($"SideMember '{member.name}' ({member.Side}) cannot join the mode: {e.Message}", nameof(members), e);
                }
            }

            _session = session;
            _localPlayer = localPlayer != null && ordered.Contains(localPlayer) ? localPlayer : null;
            _members.AddRange(ordered);
            for (var id = 0; id < _members.Count; id++)
            {
                _ids.Add(_members[id].Damageable, id);
                _members[id].Damageable.Died += OnMemberDied;
                Debug.Log(ModeLog.Joined(id, _members[id].name, _members[id].Side), _members[id]);
            }

            foreach (var point in spawnPoints)
            {
                if (point != null)
                {
                    _spawnPoints.Add(point);
                }
            }

            WarnSidesWithoutSpawn();

            _session.StateChanged += OnStateChanged;
            _session.RespawnReady += OnRespawnReady;
            _session.Ended += OnSessionEnded;
            StartSession(_session);
            RefreshHud();
            OnBegan();
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException($"{GetType().Name} '{name}' has not started a mode");
            }

            OnBeforeTick(deltaSeconds);
            AdvanceSession(deltaSeconds);
            RespawnPending();
            RefreshHud();
        }

        public void Respawn(SideMember member)
        {
            RequireMember(member);
            var side = member.Side;
            var usable = new List<SideSpawnPoint>(_spawnPoints.Count);
            var spawns = new List<SideSpawn>(_spawnPoints.Count);
            foreach (var point in _spawnPoints)
            {
                if (point != null)
                {
                    usable.Add(point);
                    spawns.Add(new SideSpawn(point.Side, point.Position.ToNumerics()));
                }
            }

            var enemies = new List<System.Numerics.Vector3>();
            foreach (var other in _members)
            {
                if (other != null && other != member && other.Side != side && other.IsAlive)
                {
                    enemies.Add(other.GroundPosition.ToNumerics());
                }
            }

            var index = SideSpawnSelector.Select(spawns, side, enemies);
            Vector3 position;
            Quaternion rotation;
            if (index >= 0)
            {
                position = usable[index].Position;
                rotation = usable[index].Rotation;
            }
            else
            {
                if (_warnedMissingSpawn.Add(side))
                {
                    Debug.LogWarning($"{LogTag} 陣営 {side.Id} の SideSpawnPoint がシーンにありません。倒れた場所で戻します。", this);
                }

                position = member.GroundPosition;
                rotation = member.transform.rotation;
            }

            RespawnAt(member, position, rotation);
        }

        public void RespawnAt(SideMember member, SideSpawnPoint point)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            RespawnAt(member, point.Position, point.Rotation);
        }

        public void RespawnAt(SideMember member, Vector3 position, Quaternion rotation)
        {
            var id = RequireMember(member);
            member.Respawn(position, rotation);
            member.SetActing(_session == null || (!_matchOver && _session.State != GameModeState.Ended));
            Debug.Log(ModeLog.Spawn(id, member.Side, position.ToNumerics()), member);
            OnSpawned(member);
        }

        [ContextMenu("Restart")]
        public void Restart()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException($"{GetType().Name}.Restart can only reload the scene in Play mode");
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex < 0)
            {
                Debug.LogError($"{LogTag} シーン '{scene.path}' が Build Settings にないため再読み込みできません。", this);
                return;
            }

            Debug.Log(ModeLog.Restart(), this);
            SceneManager.LoadScene(scene.buildIndex);
        }

        public void Stop()
        {
            OnStopping();
            ReleaseRestartAction();

            foreach (var damageable in _ids.Keys)
            {
                if (damageable != null)
                {
                    damageable.Died -= OnMemberDied;
                }
            }

            if (_session != null)
            {
                _session.StateChanged -= OnStateChanged;
                _session.RespawnReady -= OnRespawnReady;
                _session.Ended -= OnSessionEnded;
            }

            // 別の Runner が後から出した状態は消さない。
            if (_publishedStatus != null)
            {
                GameSignals.ModeStatus.ClearIfCurrent(_publishedStatus);
            }

            _ids.Clear();
            _members.Clear();
            _spawnPoints.Clear();
            _pendingRespawns.Clear();
            _warnedMissingSpawn.Clear();
            _session = null;
            _localPlayer = null;
            _publishedStatus = null;
            _matchOver = false;
            _matchWinner = null;
        }

        protected void EndMatch(Side? winner)
        {
            if (_session == null || _matchOver)
            {
                return;
            }

            _matchOver = true;
            _matchWinner = winner;
            _pendingRespawns.Clear();
            foreach (var member in _members)
            {
                if (member != null)
                {
                    member.SetActing(false);
                }
            }

            AcquireRestartAction();
            Debug.Log(ModeLog.Ended(winner), this);
            RefreshHud();
            OnEnded(winner);
            RaiseMatchFinished(winner);
        }

        // 操作しているプレイヤーがいない (bot だけの) 試合には「自分」の勝ち負けが無いので出さない。
        private void RaiseMatchFinished(Side? winner)
        {
            if (_session == null || _localPlayer == null || !_session.HasPlayer(LocalPlayerId))
            {
                return;
            }

            var ownSide = _session.GetSide(LocalPlayerId);
            var outcome = !winner.HasValue ? MatchOutcome.Draw : winner.Value == ownSide ? MatchOutcome.Won : MatchOutcome.Lost;
            CombatEvents.RaiseMatchFinished(new MatchResult(
                outcome,
                _session.GetKills(LocalPlayerId),
                _session.GetDeaths(LocalPlayerId),
                ScoreOf(_session.GetScore(ownSide))));
        }

        // 得点は小数を含みうるが、表示と同じく切り捨てて渡す。
        private static int ScoreOf(double score)
        {
            return score > 0d ? (int)Math.Min(int.MaxValue, Math.Floor(score)) : 0;
        }

        protected void LogScore()
        {
            if (_session != null)
            {
                Debug.Log(ModeLog.Score(_session), this);
            }
        }

        protected void RefreshHud()
        {
            if (_session == null)
            {
                return;
            }

            float? respawnSeconds = null;
            if (_localPlayer != null && _session.TracksDowns)
            {
                respawnSeconds = _session.GetRespawnRemaining(LocalPlayerId);
            }

            var ownSide = _localPlayer != null ? _localPlayer.Side : (Side?)null;
            // ModeStatus は出した後も UI が持つので、得点の並びは使い回さずに毎回作る。
            var sideScores = new SideScore[_session.Sides.Count];
            for (var i = 0; i < sideScores.Length; i++)
            {
                var side = _session.Sides[i];
                sideScores[i] = new SideScore(side, _session.GetScore(side));
            }

            var remaining = float.IsInfinity(_session.TimeLimitSeconds) ? (float?)null : Math.Max(0f, _session.RemainingSeconds);
            _publishedStatus = new ModeStatus(
                remaining,
                sideScores,
                ownSide,
                respawnSeconds.HasValue ? Math.Max(0f, respawnSeconds.Value) : (float?)null,
                _matchOver,
                _matchOver ? _matchWinner : null,
                HudDetail);
            GameSignals.ModeStatus.Publish(_publishedStatus);
        }

        private int RequireMember(SideMember member)
        {
            var id = IdOf(member);
            if (id < 0)
            {
                throw new ArgumentException($"'{(member != null ? member.name : "null")}' is not a member of this mode", nameof(member));
            }

            return id;
        }

        private static List<SideMember> OrderMembers(IReadOnlyList<SideMember> members, SideMember localPlayer)
        {
            var seen = new HashSet<SideMember>();
            var others = new List<(SideMember Member, int Index)>();
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || member == localPlayer || !seen.Add(member))
                {
                    continue;
                }

                if (member.Damageable == null)
                {
                    throw new ArgumentException($"SideMember '{member.name}' has no Damageable", nameof(members));
                }

                others.Add((member, i));
            }

            // FindObjectsByType の順は決まっていないので、ログの ID が Play ごとに変わらないよう陣営と名前で並べる。
            others.Sort((a, b) =>
            {
                var bySide = a.Member.Side.CompareTo(b.Member.Side);
                if (bySide != 0)
                {
                    return bySide;
                }

                var byName = string.CompareOrdinal(a.Member.name, b.Member.name);
                return byName != 0 ? byName : a.Index.CompareTo(b.Index);
            });

            var ordered = new List<SideMember>(others.Count + 1);
            if (localPlayer != null)
            {
                if (localPlayer.Damageable == null)
                {
                    throw new ArgumentException($"SideMember '{localPlayer.name}' has no Damageable", nameof(localPlayer));
                }

                ordered.Add(localPlayer);
            }

            foreach (var entry in others)
            {
                ordered.Add(entry.Member);
            }

            return ordered;
        }

        private void WarnSidesWithoutSpawn()
        {
            if (!_session.TracksDowns)
            {
                return;
            }

            foreach (var member in _members)
            {
                var side = member.Side;
                var found = false;
                foreach (var point in _spawnPoints)
                {
                    if (point.Side == side)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found && _warnedMissingSpawn.Add(side))
                {
                    Debug.LogWarning($"{LogTag} 陣営 {side.Id} の SideSpawnPoint がシーンにありません。倒れた場所で戻します。", this);
                }
            }
        }

        private void OnMemberDied(Damageable damageable)
        {
            if (_session == null || !_ids.TryGetValue(damageable, out var victimId))
            {
                return;
            }

            var victim = _members[victimId];
            if (_session.State != GameModeState.Playing || _session.IsDown(victimId))
            {
                return;
            }

            victim.SetActing(false);

            var killer = ResolveKiller();
            var killerId = IdOf(killer);
            if (InterceptDeath(killerId >= 0 && killerId != victimId ? killer : null, victim))
            {
                return;
            }

            if (killerId >= 0 && killerId != victimId)
            {
                Debug.Log(
                    killer.Side == victim.Side ? ModeLog.TeamKill(killerId, victimId) : ModeLog.Kill(killerId, victimId),
                    victim);
                _session.ReportKill(killerId, victimId);
            }
            else
            {
                killer = null;
                Debug.Log(ModeLog.Death(victimId), victim);
                _session.ReportDeath(victimId);
            }

            // このキルで終わったときは、終わったときの状態のログに得点が載っている。
            if (_session.State == GameModeState.Playing)
            {
                LogScore();
            }

            OnKill(killer, victim);
        }

        private static SideMember ResolveKiller()
        {
            var attribution = CombatEvents.CurrentAttribution;
            return attribution.HasValue ? SideMember.Of(CombatEvents.OfflineObject(attribution.Value.KillerId)) : null;
        }

        private void OnRespawnReady(int playerId)
        {
            _pendingRespawns.Enqueue(playerId);
        }

        private void RespawnPending()
        {
            while (_pendingRespawns.Count > 0)
            {
                var playerId = _pendingRespawns.Dequeue();
                if (_session == null || _session.State != GameModeState.Playing)
                {
                    _pendingRespawns.Clear();
                    return;
                }

                var member = MemberOf(playerId);
                if (member != null)
                {
                    Respawn(member);
                }
            }
        }

        private void OnStateChanged(GameModeState state)
        {
            Debug.Log(ModeLog.State(_session), this);
        }

        private void OnSessionEnded(Side? winner)
        {
            var log = SessionEndedLog(winner);
            // ラウンドの終わりに区切りのログを自分で出す派生 (log が null) は、ModeSession の終わりを試合の終わりにしない。
            if (EndsMatchWithSession && log != null)
            {
                EndMatch(winner);
                return;
            }

            if (log != null)
            {
                Debug.Log(log, this);
            }

            RefreshHud();
            OnEnded(winner);
        }

        private InputAction ResolveRestartAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    $"{LogTag} プロジェクト全体の Input Actions が設定されていないため、R で再開できません (Project Settings > Input System Package)。",
                    this);
                return null;
            }

            var action = actions.FindAction(RestartActionPath);
            if (action == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' に '{RestartActionPath}' がないため、R で再開できません。", this);
            }

            return action;
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
        private void AcquireRestartAction()
        {
            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (_restartAction == null || !Application.isPlaying || !isActiveAndEnabled || _restartAction.enabled)
            {
                return;
            }

            _restartAction.Enable();
            _enabledRestartAction = true;
        }

        private void ReleaseRestartAction()
        {
            if (_enabledRestartAction && _restartAction != null)
            {
                _restartAction.Disable();
            }

            _enabledRestartAction = false;
        }
    }
}
