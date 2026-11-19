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

        [SerializeField, Tooltip("ルールの RespawnAnchor に Squad があるとき、倒された人を味方のどれだけ近くに、どんな味方の近くに戻すか")]
        private SquadSpawnSettings _squadSpawn = new SquadSpawnSettings();

        // 体の大きさは戻す人の CharacterController から測り、持たない体 (bot など) だけこの大きさとみなす。
        public const float FallbackBodyRadius = 0.35f;
        public const float FallbackBodyHeight = 1.8f;

        // 段差や坂に体の底が触れて塞がっているとみなさないよう、足元から少し浮かせて重なりを調べる。
        private const float BodyLift = 0.1f;

        // 味方の近くに戻す位置の真下に床があるかを、この高さの上からこの深さの下まで探す。
        private const float GroundProbeAbove = 1f;
        private const float GroundProbeBelow = 2f;

        private readonly List<SideMember> _members = new List<SideMember>();
        private readonly Dictionary<int, float> _lastDamagedAt = new Dictionary<int, float>();
        private readonly Dictionary<Damageable, int> _ids = new Dictionary<Damageable, int>();
        private readonly List<SideSpawnPoint> _spawnPoints = new List<SideSpawnPoint>();
        private readonly Queue<int> _pendingRespawns = new Queue<int>();
        private readonly HashSet<Side> _warnedMissingSpawn = new HashSet<Side>();
        private ModeSession _session;
        private OfflineAssists _assists;
        private AssistSpec _assistSpec;
        private SideMember _localPlayer;
        private ModeStatus _publishedStatus;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _restartAction;
        private bool _enabledRestartAction;
        private bool _matchOver;
        private Side? _matchWinner;
        private GameRules _rules;
        private GameRules _sceneRules;
        private SquadSpawnSelector _squadSpawnSelector;
        // 撃たれてからの秒数を Time.time でなく Tick の刻みで数えるのは、Edit Mode のテストでも同じに進めるため。
        private float _elapsedSeconds;

        public ModeSession Session => _session;

        // 入っていなければ GameBootstrap が組み立てたルール、それも無ければ GameRules.Default を読む。試すときにシーンを組まずに渡せるよう、上書きできるようにしておく。
        public GameRules Rules
        {
            get => _rules ?? _sceneRules ?? GameRules.Default;
            set => _rules = value;
        }

        public SquadSpawnSettings SquadSpawn => _squadSpawn ??= new SquadSpawnSettings();

        // 入っていなければ KitTuning.Assist を読む。試すときに Resources を通さず値を渡せるよう、上書きできるようにしておく。
        public AssistSpec AssistSpec
        {
            get => _assistSpec ?? KitTuning.Assist;
            set => _assistSpec = value;
        }

        public OfflineAssists Assists => _assists;

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

            _sceneRules = world != null ? world.Rules : null;

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

            SquadSpawnSelector squadSpawnSelector;
            try
            {
                squadSpawnSelector = new SquadSpawnSelector(SquadSpawn.ToSpec());
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"{GetType().Name} '{name}' has an invalid Squad Spawn: {e.Message}", nameof(SquadSpawn), e);
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
            _squadSpawnSelector = squadSpawnSelector;
            _elapsedSeconds = 0f;
            _localPlayer = localPlayer != null && ordered.Contains(localPlayer) ? localPlayer : null;
            _members.AddRange(ordered);
            _assists = new OfflineAssists(AssistSpec, (attackerId, victimId) => AreAllies(session, attackerId, victimId));
            for (var id = 0; id < _members.Count; id++)
            {
                _ids.Add(_members[id].Damageable, id);
                _members[id].Damageable.Died += OnMemberDied;
                _members[id].Damageable.Damaged += OnMemberDamaged;
                _assists.Watch(_members[id].Damageable, id);
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
            _elapsedSeconds += Math.Max(0f, deltaSeconds);
            _assists.Advance(deltaSeconds);
            AdvanceSession(deltaSeconds);
            RespawnPending();
            RefreshHud();
        }

        // ラウンドの始めの並べ直しや感染のように、倒されて戻るのでない呼び出しはビーコンを使わず拠点に戻す。
        public void Respawn(SideMember member)
        {
            RequireMember(member);
            BasePose(member, out var position, out var rotation);
            RespawnAt(member, position, rotation);
        }

        // 倒されて戻るときは、ルールの RespawnAnchor の順にビーコン・味方の近く・拠点を試し、使ったものを返す。
        public RespawnAnchor RespawnByRules(SideMember member)
        {
            var id = RequireMember(member);
            var pose = default(Pose);
            var anchor = RespawnAnchorOrder.Choose(
                Rules.RespawnAnchors,
                candidate => candidate == RespawnAnchor.Beacon ? TryBeaconPose(member, out pose) : TrySquadPose(member, id, out pose));

            if (anchor == RespawnAnchor.Base)
            {
                BasePose(member, out var position, out var rotation);
                pose = new Pose(position, rotation);
            }

            Place(member, id, pose.position, pose.rotation, ModeLog.Spawn(id, member.Side, pose.position.ToNumerics(), anchor));
            return anchor;
        }

        private bool TryBeaconPose(SideMember member, out Pose pose)
        {
            pose = default;
            return SpawnBeacon.TryFindFor(member.gameObject, out var beacon) && beacon.TryUse(member.gameObject, member.name, out pose);
        }

        private bool TrySquadPose(SideMember member, int id, out Pose pose)
        {
            pose = default;
            var side = member.Side;
            var mates = new List<SquadSpawnMate>(_members.Count);
            var enemies = new List<System.Numerics.Vector3>(_members.Count);
            for (var otherId = 0; otherId < _members.Count; otherId++)
            {
                var other = _members[otherId];
                if (other == null || otherId == id)
                {
                    continue;
                }

                // ダウン中 (起こされ待ち) の味方は体力が残っていても動けないので、そばに戻しても守り合えない。
                var alive = other.IsAlive && !(_session.HasPlayer(otherId) && _session.IsDown(otherId));
                var position = other.GroundPosition.ToNumerics();
                mates.Add(new SquadSpawnMate(other.Side, position, alive, SecondsSinceDamaged(otherId)));
                if (alive && other.Side != side)
                {
                    enemies.Add(position);
                }
            }

            var chosen = _squadSpawnSelector.Select(side, mates, enemies, p => TryStandAt(member, p.ToUnity(), out _));
            if (!chosen.HasValue || !TryStandAt(member, chosen.Value.ToUnity(), out var ground))
            {
                return false;
            }

            pose = new Pose(ground, FacingOfNearestMate(member, ground));
            return true;
        }

        // 床が無い所 (崖の外や空を飛ぶ機体の下) と、壁や置き物に体が重なる所には戻さない。体どうしの重なりは SquadSpawnSelector が見る。
        private bool TryStandAt(SideMember member, Vector3 position, out Vector3 ground)
        {
            ground = position;
            var from = position + (Vector3.up * GroundProbeAbove);
            if (!Physics.Raycast(from, Vector3.down, out var hit, GroundProbeAbove + GroundProbeBelow, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            ground = hit.point;
            var controller = member.GetComponent<CharacterController>();
            var radius = controller != null ? controller.radius : FallbackBodyRadius;
            var height = controller != null ? controller.height : FallbackBodyHeight;
            var bottom = ground + (Vector3.up * (radius + BodyLift));
            var top = ground + (Vector3.up * Mathf.Max(height - radius, radius + BodyLift));
            foreach (var overlap in Physics.OverlapCapsule(bottom, top, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var other = SideMember.Of(overlap.gameObject);
                // ダウン中の味方の体は場に残るので、体どうしの判定 (Selector) では避けられず、ここで重なりとして扱う。
                if (other == null || (other != member && IsDownedBody(other)))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsDownedBody(SideMember other)
        {
            var otherId = _members.IndexOf(other);
            return otherId >= 0 && _session.HasPlayer(otherId) && _session.IsDown(otherId);
        }

        private Quaternion FacingOfNearestMate(SideMember member, Vector3 position)
        {
            SideMember nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var other in _members)
            {
                if (other == null || other == member || other.Side != member.Side || !other.IsAlive)
                {
                    continue;
                }

                var distance = (other.GroundPosition - position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = other;
                    nearestDistance = distance;
                }
            }

            var forward = nearest != null ? Vector3.ProjectOnPlane(nearest.transform.forward, Vector3.up) : Vector3.zero;
            return forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : member.transform.rotation;
        }

        private float SecondsSinceDamaged(int playerId)
        {
            return _lastDamagedAt.TryGetValue(playerId, out var at) ? Math.Max(0f, _elapsedSeconds - at) : SquadSpawnMate.NeverDamaged;
        }

        private void BasePose(SideMember member, out Vector3 position, out Quaternion rotation)
        {
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
            Place(member, id, position, rotation, ModeLog.Spawn(id, member.Side, position.ToNumerics()));
        }

        private void Place(SideMember member, int id, Vector3 position, Quaternion rotation, string log)
        {
            _assists?.Clear(id);
            _lastDamagedAt.Remove(id);
            member.Respawn(position, rotation);
            member.SetActing(_session == null || (!_matchOver && _session.State != GameModeState.Ended));
            Debug.Log(log, member);
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
                    damageable.Damaged -= OnMemberDamaged;
                }
            }

            if (_session != null)
            {
                _session.StateChanged -= OnStateChanged;
                _session.RespawnReady -= OnRespawnReady;
                _session.Ended -= OnSessionEnded;
            }

            _assists?.Stop();
            _assists = null;

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
            _lastDamagedAt.Clear();
            _squadSpawnSelector = null;
            _sceneRules = null;
            _elapsedSeconds = 0f;
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
            foreach (var playerId in _session.GetPlayerIds())
            {
                Debug.Log(ModeLog.Stats(_session, playerId), this);
            }

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
                var assisters = _assists.TakeAssisters(victimId, killerId);
                Debug.Log(
                    killer.Side == victim.Side ? ModeLog.TeamKill(killerId, victimId) : ModeLog.Kill(killerId, victimId, assisters),
                    victim);
                _session.ReportKill(killerId, victimId, assisters);
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

        // 派生がモードから抜いた人のダメージは、アシストにならないよう味方どうしと同じに扱う。
        private static bool AreAllies(ModeSession session, int attackerId, int victimId)
        {
            return !session.HasPlayer(attackerId) || !session.HasPlayer(victimId) || session.GetSide(attackerId) == session.GetSide(victimId);
        }

        private static SideMember ResolveKiller()
        {
            var attribution = CombatEvents.CurrentAttribution;
            return attribution.HasValue ? SideMember.Of(CombatEvents.OfflineObject(attribution.Value.KillerId)) : null;
        }

        private void OnMemberDamaged(Damageable damageable, DamageReport report)
        {
            if (_session != null && _ids.TryGetValue(damageable, out var playerId))
            {
                _lastDamagedAt[playerId] = _elapsedSeconds;
            }
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
                    RespawnByRules(member);
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
