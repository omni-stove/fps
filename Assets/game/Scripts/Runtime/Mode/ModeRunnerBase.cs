using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// オフラインのモードをシーンで進める Runner の土台。派生クラスは <see cref="CreateSession"/> で <see cref="ModeSession"/> を組み立てるだけでよい。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Start でシーンの <see cref="SideMember"/> を参加者にする。<see cref="GameBootstrap"/> があれば、そのプレイヤーに
    /// SideMember が無いときは <see cref="PlayerSide"/> で付ける。プレイヤーの ID は 0、残りは陣営の ID・名前の順に 1 から振る。</item>
    /// <item>参加者の <see cref="Damageable.Died"/> を、倒した者 (<see cref="CombatEvents.CurrentAttribution"/>) が参加者なら
    /// <see cref="ModeSession.ReportKill"/>、分からないか自分なら <see cref="ModeSession.ReportDeath"/> に渡し、倒れている間は操作と行動を止める。</item>
    /// <item><see cref="ModeSession.RespawnReady"/> を受けたら、次の <see cref="Tick"/> の終わりに自陣営の <see cref="SideSpawnPoint"/> のうち
    /// 敵からいちばん離れた点 (<see cref="SideSpawnSelector"/>) に戻し、体力を満たす。ダメージの処理の途中で生き返らせないよう、すぐには戻さない。</item>
    /// <item>Update で <see cref="Tick"/> し、状態が変わるたび・倒れるたび・終わったときに <c>[Mode]</c> のログ (<see cref="ModeLog"/>) を出す。
    /// HUD があれば <see cref="IModeHud"/> に残り時間・得点・自分のリスポーンまでの秒数・派生モードの行 (<see cref="AppendHudLines"/>)・結果を出す。</item>
    /// <item>終わったら全員の操作と行動を止め、<see cref="RestartActionPath"/> (キーボードの R、ゲームパッドの X) でシーンを読み直す。</item>
    /// <item><see cref="HidesTargetDummies"/> が true (既定) なら、始めたときに GameBootstrap の標的ダミーを隠す。</item>
    /// <item>ModeSession をラウンドとして繰り返す派生は、始め方 (<see cref="StartSession"/>)・進め方 (<see cref="AdvanceSession"/>)・
    /// 倒れ方 (<see cref="InterceptDeath"/>)・HUD (<see cref="HudScores(Side?)"/> など)・終わりのログ (<see cref="SessionEndedLog"/>) を置き換える。</item>
    /// <item>試合の終わりを自分で決める派生は <see cref="EndsMatchWithSession"/> を false にし、終わったときに <see cref="EndMatch"/> を呼ぶ。</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public abstract class ModeRunnerBase : MonoBehaviour
    {
        public const string LogTag = ModeLog.Tag;

        /// <summary><see cref="GameBootstrap"/> のプレイヤーの、このモードでの ID。</summary>
        public const int LocalPlayerId = 0;

        /// <summary>
        /// 再開に使うアクション。<see cref="MatchRunner.RestartActionPath"/> と同じ Reload。
        /// 終了後は全員の <see cref="WeaponController"/> を止めるので、リロードと取り合うことはない。
        /// </summary>
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
        private readonly List<string> _hudLines = new List<string>();
        private readonly List<LocalizedText> _hudLineTexts = new List<LocalizedText>();
        private ModeSession _session;
        private SideMember _localPlayer;
        private IModeHud _hudView;
        private InputAction _restartAction;
        private bool _enabledRestartAction;
        private bool _matchOver;

        /// <summary>進めているモード。開始前や開始に失敗したときは null。</summary>
        public ModeSession Session => _session;

        public bool IsStarted => _session != null;

        /// <summary>試合が終わったか (<see cref="EndMatch"/> の後)。終わった後は全員の操作と行動を止め、再開の入力を受け付ける。</summary>
        public bool IsMatchOver => _matchOver;

        /// <summary>
        /// <see cref="Session"/> が終わったら試合も終わりとして扱うなら true (既定)。ラウンド制のモードは false にし、
        /// 試合が終わったときに自分で <see cref="EndMatch"/> を呼ぶ。
        /// </summary>
        protected virtual bool EndsMatchWithSession => true;

        /// <summary>出現点。null は除いてある。</summary>
        protected IReadOnlyList<SideSpawnPoint> SpawnPoints => _spawnPoints;

        /// <summary>参加者。添字がモードでの ID。</summary>
        public IReadOnlyList<SideMember> Members => _members;

        /// <summary>この画面で操作しているプレイヤー。いなければ null。</summary>
        public SideMember LocalPlayer => _localPlayer;

        /// <summary>出している HUD。HUD が無ければ null。</summary>
        public IModeHud ModeHud => _hudView;

        /// <summary>終了後に押すと再開するアクション。解決できなければ null (モードは進むが再開できない)。</summary>
        public InputAction RestartAction => _restartAction;

        /// <summary><see cref="GameBootstrap"/> のプレイヤーに SideMember が無いとき、付ける陣営。</summary>
        public Side PlayerSide
        {
            get => new Side(Math.Max(0, _playerSideId));
            set => _playerSideId = value.Id;
        }

        /// <summary><see cref="TryBegin"/> で始めたとき、<see cref="GameBootstrap"/> が並べる標的ダミーを隠すか。既定は true。</summary>
        public bool HidesTargetDummies
        {
            get => _hideTargetDummies;
            set => _hideTargetDummies = value;
        }

        /// <summary>
        /// このモードの <see cref="ModeSession"/> を組み立てる。参加者は入れずに Waiting のまま返す (<see cref="Begin"/> が入れて始める)。
        /// 設定が不正なら <see cref="ArgumentException"/> か <see cref="InvalidOperationException"/> を投げる (理由をログに出して止まる)。
        /// </summary>
        protected abstract ModeSession CreateSession();

        /// <summary>
        /// <see cref="Begin"/> で参加者を入れてモードを始め、HUD を書いた直後に 1 度だけ呼ばれる。<see cref="Members"/> と <see cref="Session"/> は揃っている。
        /// 参加者に持たせる物 (武器など) をモードに合わせるならここで行う。
        /// </summary>
        protected virtual void OnBegan()
        {
        }

        /// <summary>
        /// 参加者が倒れ、モードに報告した直後に呼ばれる。killer は倒した参加者で、分からないときと自滅では null。
        /// 倒した武器は <see cref="CombatEvents.CurrentAttribution"/> でまだ読める。
        /// リスポーンの部品を持たないモードは、ここで <see cref="Respawn"/> を呼ぶなどして戻し方を決める。
        /// </summary>
        protected virtual void OnKill(SideMember killer, SideMember victim)
        {
        }

        /// <summary>参加者を出現点に戻した直後に呼ばれる。</summary>
        protected virtual void OnSpawned(SideMember member)
        {
        }

        /// <summary>モードが終わった直後に 1 度だけ呼ばれる。winner は勝った陣営で、引き分けなら null。</summary>
        protected virtual void OnEnded(Side? winner)
        {
        }

        /// <summary><see cref="ModeSession.Tick"/> の直前に呼ばれる。拠点の出入りなど、時間を進める前にモードへ伝えることがあればここで伝える。</summary>
        protected virtual void OnBeforeTick(float deltaSeconds)
        {
        }

        /// <summary>HUD の得点の下に足す行を lines に入れる。HUD を書き直すたびに空の lines で呼ばれる。</summary>
        protected virtual void AppendHudLines(List<string> lines)
        {
        }

        /// <summary>
        /// <see cref="Begin"/> が参加者を入れる前に、session をこの Runner で進められるか確かめる。進められなければ <see cref="ArgumentException"/> を投げる。
        /// 既定では何もしない。ModeSession を包む別の進め方 (ラウンド制など) を使う派生は、包んだものと session が合うかをここで確かめる。
        /// </summary>
        protected virtual void ValidateSession(ModeSession session)
        {
        }

        /// <summary><see cref="Begin"/> の最後に session を始める。既定は <see cref="ModeSession.Start"/>。ラウンド制のように外から始める派生が置き換える。</summary>
        protected virtual void StartSession(ModeSession session)
        {
            session.Start();
        }

        /// <summary><see cref="Tick"/> で時間を進める。既定は <see cref="TickSession"/>。ラウンド制のように外から進める派生が置き換える。</summary>
        protected virtual void AdvanceSession(float deltaSeconds)
        {
            TickSession(_session, deltaSeconds);
        }

        /// <summary><see cref="AdvanceSession"/> の既定が呼ぶ。session を渡される形で置き換えたい派生用。既定は <see cref="ModeSession.Tick"/>。</summary>
        protected virtual void TickSession(ModeSession session, float deltaSeconds)
        {
            session.Tick(deltaSeconds);
        }

        /// <summary>
        /// 参加者が倒れ、操作を止めた直後、モードに報告する前に呼ばれる。killer は倒した参加者で、分からないときと自滅では null。
        /// true を返すと既定の報告 (<see cref="ModeSession.ReportKill"/> などとログと <see cref="OnKill"/>) をしない。ダウンを扱うモードが使う。
        /// </summary>
        protected virtual bool InterceptDeath(SideMember killer, SideMember victim)
        {
            return false;
        }

        /// <summary>HUD の残り時間。既定は <see cref="ModeHudText.Time"/>。</summary>
        protected virtual string HudTime()
        {
            return HudTime(_session);
        }

        /// <summary>HUD の陣営ごとの得点の行。既定は <see cref="ModeHudText.Scores"/>。</summary>
        protected virtual IReadOnlyList<ModeHudScoreLine> HudScores(Side? ownSide)
        {
            return HudScores(_session, ownSide);
        }

        /// <summary>HUD の結果。既定は <see cref="ModeHudText.Result"/>。</summary>
        protected virtual string HudResult()
        {
            return HudResult(_session);
        }

        /// <summary><see cref="HudTime()"/> の既定が呼ぶ。session を渡される形で置き換えたい派生用。</summary>
        protected virtual string HudTime(ModeSession session) => ModeHudText.Time(session);

        /// <summary><see cref="HudScores(Side?)"/> の既定が呼ぶ。session を渡される形で置き換えたい派生用。</summary>
        protected virtual IReadOnlyList<ModeHudScoreLine> HudScores(ModeSession session, Side? ownSide) => ModeHudText.Scores(session, ownSide);

        /// <summary><see cref="HudResult()"/> の既定が呼ぶ。session を渡される形で置き換えたい派生用。</summary>
        protected virtual string HudResult(ModeSession session) => ModeHudText.Result(session);

        /// <summary>
        /// ModeSession が終わったときに出すログ。null なら出さない。既定は <see cref="ModeLog.Ended"/>。
        /// ModeSession の終わりが試合の終わりでない (ラウンドの終わり) 派生は、自分で区切りのログを出して null を返す。
        /// </summary>
        protected virtual string SessionEndedLog(Side? winner)
        {
            return ModeLog.Ended(winner);
        }

        /// <summary><see cref="Stop"/> の頭、購読を外す前に呼ばれる。派生が自分で購読したものはここで外す。</summary>
        protected virtual void OnStopping()
        {
            OnStop();
        }

        /// <summary>
        /// <see cref="OnStopping"/> の既定が呼ぶ。何度呼ばれてもよいように書く。
        /// Unity のメッセージ (OnDisable など) を派生クラスで定義すると土台のものが呼ばれなくなるので、購読の解除はこちらか <see cref="OnStopping"/> で行う。
        /// </summary>
        protected virtual void OnStop()
        {
        }

        /// <summary>参加者の ID。参加していなければ -1。</summary>
        public int IdOf(SideMember member)
        {
            if (member == null || member.Damageable == null)
            {
                return -1;
            }

            return _ids.TryGetValue(member.Damageable, out var id) ? id : -1;
        }

        /// <summary>ID の参加者。いなければ null。</summary>
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

        /// <summary>
        /// シーンの参加者・出現点・HUD を拾い、<see cref="CreateSession"/> のモードを始める。
        /// 既に始まっていれば何もせず true。始められなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
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

            var hud = world != null && GameUi.IsAlive(world.Hud) ? world.Hud : GameUi.FindHud();

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
                Begin(session, SideMember.FindAll(), SideSpawnPoint.FindAll(), localPlayer, GameUi.IsAlive(hud) ? hud.ModeHud : null);
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

        /// <summary>
        /// members をモードの参加者にし、死亡と出現を繋いで session を始める。session は Waiting で参加者がいないこと。
        /// localPlayer は ID 0 にし、HUD で自分の陣営として扱う (members に無ければ加える)。
        /// 引数が不正か、陣営がモードのチーム分けに入らなければ <see cref="ArgumentException"/> を投げ、何も購読しない (session は使えなくなる)。
        /// </summary>
        /// <param name="session">始めるモード。</param>
        /// <param name="members">参加者。null の要素と重複は飛ばす。<see cref="Damageable"/> を持つこと。</param>
        /// <param name="spawnPoints">出現点。null の要素は飛ばす。</param>
        /// <param name="localPlayer">この画面で操作しているプレイヤー。null 可。</param>
        /// <param name="hudView">得点を出す HUD。null 可。</param>
        public void Begin(
            ModeSession session,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null,
            IModeHud hudView = null)
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
            _hudView = hudView;
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

        /// <summary>
        /// 時間を deltaSeconds 進め、待っていたリスポーンを行い、HUD を書き直す。Play 中は Update から呼ぶ。Edit Mode のテストでは直接呼ぶ。
        /// </summary>
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

        /// <summary>
        /// member を自陣営の出現点のうち、生きている敵からいちばん離れた点に戻し、体力を満たして操作と行動を戻す (終了後は止めたまま)。
        /// 自陣営の出現点が無ければ倒れた場所で戻し、陣営ごとに 1 度だけ警告を出す。
        /// </summary>
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

        /// <summary>
        /// member を point に戻し、体力を満たして操作と行動を戻す (終了後は止めたまま)。
        /// ラウンドの頭で、参加者を 1 人ずつ別の出現点に並べるときに使う。
        /// </summary>
        public void RespawnAt(SideMember member, SideSpawnPoint point)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            RespawnAt(member, point.Position, point.Rotation);
        }

        /// <summary>
        /// member を position (足元) に戻し、体力を満たして操作と行動を戻す (終了後は止めたまま)。
        /// 出現点を自分で選ぶ派生 (ラウンドの始めに全員を並べ直すなど) が使う。
        /// </summary>
        public void RespawnAt(SideMember member, Vector3 position, Quaternion rotation)
        {
            var id = RequireMember(member);
            member.Respawn(position, rotation);
            member.SetActing(_session == null || (!_matchOver && _session.State != GameModeState.Ended));
            Debug.Log(ModeLog.Spawn(id, member.Side, position.ToNumerics()), member);
            OnSpawned(member);
        }

        /// <summary>今のシーンを読み込み直して最初からやり直す。Play 中にのみ呼べる。</summary>
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

        /// <summary>購読を解除してモードから離れる。破棄時にも呼ばれる。</summary>
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

            if (GameUi.IsAlive(_hudView))
            {
                _hudView.Hide();
            }

            _ids.Clear();
            _members.Clear();
            _spawnPoints.Clear();
            _pendingRespawns.Clear();
            _warnedMissingSpawn.Clear();
            _session = null;
            _localPlayer = null;
            _hudView = null;
            _matchOver = false;
        }

        /// <summary>
        /// 試合を終える。全員の操作と行動を止め、再開の入力を受け付け、<c>[Mode] ended</c> を出して HUD を書き直してから <see cref="OnEnded"/> を呼ぶ。
        /// <see cref="EndsMatchWithSession"/> が true なら <see cref="Session"/> が終わったときに呼ばれる。2 度目からは何もしない。
        /// </summary>
        protected void EndMatch(Side? winner)
        {
            if (_session == null || _matchOver)
            {
                return;
            }

            _matchOver = true;
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
        }

        /// <summary>今の得点を <c>[Mode] score</c> でログに出す。キルのたびに呼ぶ。時間で点が入るモードは派生クラスが区切りで呼ぶ。</summary>
        protected void LogScore()
        {
            if (_session != null)
            {
                Debug.Log(ModeLog.Score(_session), this);
            }
        }

        /// <summary>HUD を今の値で書き直す。<see cref="Tick"/> のたびに呼ばれる。</summary>
        protected void RefreshHud()
        {
            if (!GameUi.IsAlive(_hudView) || _session == null)
            {
                return;
            }

            _hudLines.Clear();
            if (_localPlayer != null)
            {
                var remaining = _session.TracksDowns ? _session.GetRespawnRemaining(LocalPlayerId) : null;
                if (remaining.HasValue)
                {
                    _hudLines.Add(HudFormat.Respawn(HudFormat.DisplayedSeconds(remaining.Value)));
                }
            }

            AppendHudLines(_hudLines);
            _hudLineTexts.Clear();
            foreach (var line in _hudLines)
            {
                _hudLineTexts.Add(LiteralOrEmpty(line));
            }

            var ownSide = _localPlayer != null ? _localPlayer.Side : (Side?)null;
            _hudView.Show(LiteralOrEmpty(HudTime()), HudScores(ownSide), _hudLineTexts, LiteralOrEmpty(HudResult()));
        }

        // 派生モードの文はまだ表のキーになっていないので、作った文字列をそのまま運ぶ。null は空の文として渡す
        private static LocalizedText LiteralOrEmpty(string text)
        {
            return text == null ? default : LocalizedText.Literal(text);
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

            // 倒れている間は動けず撃てない。操作を止めても体はその場に残る。
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

        /// <summary>プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。</summary>
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
