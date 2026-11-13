using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net
{
    /// <summary>
    /// チームデスマッチをネットワークで同期する。<see cref="TeamDeathmatchSession"/> (ルール本体) はサーバーだけが持ち、
    /// チームの点数・残り時間・状態は NetworkVariable、各プレイヤーのキル数・デス数は <see cref="Players"/> で配る。
    /// <see cref="NetBootstrap"/> がサーバー起動時に生成して Spawn する。
    /// </summary>
    /// <remarks>
    /// サーバーは接続順 (<see cref="NetworkManager.ConnectedClientsList"/> の順) にプレイヤーを試合に加え、人数の少ないチーム
    /// (同じなら点の低いチーム) に入れる。切断した人の成績は <see cref="ReconnectGraceSeconds"/> 秒残し、同じ鍵でつなぎ直せば引き継ぐ (.Roster.cs)。
    /// 点数・残り時間・状態は NetworkVariable、壊れた建物は <see cref="NetDestructibleStructure"/> の NetworkList なので、途中から入った人にも Spawn の時点で届く。
    /// ダメージはすべて <see cref="ApplyDamage"/> を通し、体力が尽きたら Core 側が ReportKill する。
    /// 味方に倒されたときは <see cref="MatchLog.TeamKill"/> で敵のキルと分けてログに出す。
    /// 出現・リスポーンの地点は Core の SpawnSelector が選び、ここでは <see cref="NetPlayer.ServerTeleport"/> を呼ぶだけ。
    /// <para>
    /// 射撃はサーバー権威のヒットスキャンで、ラグ補償をかける。毎フレームの終わりに全プレイヤーの位置を
    /// サーバーの tick で <see cref="LagCompensator"/> に記録し、射撃 (<see cref="ServerFire"/>) が届いたら
    /// 撃った側が見ていた時点まで巻き戻した位置で <see cref="HitboxRewinder"/> が Raycast する。
    /// ダメージ量は <see cref="DamageCalculator"/>、体力は Session の <see cref="Health"/> が決め、
    /// 体力は <see cref="NetPlayer"/> の NetworkVariable で配る。
    /// </para>
    /// <para>
    /// 武器の値はここに持たず、ロードアウトのアセット (<see cref="LoadoutDefinition"/>) から引く。試合の開始時に
    /// <see cref="LoadoutDefinition.ToRule"/> と <see cref="LoadoutDefinition.ToSpecs"/> で検査して <see cref="Fps.Core.ServerLoadout"/> に保持し、
    /// 未設定・不正なら試合を始めない。プレイヤーが武器の一覧 (<see cref="Catalog"/>) から選んだ並びは同じルールで検査し
    /// (<see cref="ServerChooseLoadout"/>)、通ればその人の次の出現から配る。選んでいない人には既定の並びを。どちらも出現・リスポーンのたびに
    /// 試合のルールと一緒に <see cref="NetPlayerWeapon.ServerEquip(System.Collections.Generic.IReadOnlyList{WeaponSpec}, float, GameRules)"/> で持たせる
    /// (予備弾の倍率と無限予備弾はそこで各武器に効く)。
    /// 持ち替えは <see cref="NetPlayerWeapon"/> がサーバーで判定し、射撃の判定 (ダメージ・射程・減衰・連射間隔) には
    /// 撃った人がその時に持っている武器 (<see cref="NetPlayerWeapon.CurrentSpec"/>) の値を使う。
    /// クライアントは撃った武器の Id だけを送り、ロードアウトに無い・今持っている武器と違う射撃は捨てる。
    /// </para>
    /// <para>
    /// 弾倉と予備弾はサーバーが各プレイヤーの <see cref="NetPlayerWeapon"/> で持つ。出現・リスポーンで武器ごと渡し直し、試合の開始で満タンに戻す。
    /// 射撃は当たり判定の前に 1 発消費し、弾が無ければ捨てる。
    /// </para>
    /// <para>
    /// ルール (味方撃ち・リスポーン方式・回復など) はアセット (<see cref="GameRulesDefinition"/>) から試合の開始時に読み、
    /// <see cref="TeamDeathmatchSpec"/> に渡してサーバーの判定に使う。クライアントには見た目と HUD に要る値だけを
    /// <see cref="SyncedRules"/> で、Wave の次のウェーブまでの秒数を <see cref="NextWaveSeconds"/> で、
    /// 各プレイヤーを最後に倒した相手を <see cref="Players"/> の <see cref="NetPlayerStats.Killer"/> で配る。
    /// 建物を壊せるか (<see cref="GameRules.Destruction"/>) は、試合の開始時にシーンの全ての <see cref="DestructibleStructure"/> に設定する。
    /// 壊れた部品の同期は <see cref="NetDestructibleStructure"/> が行う。
    /// </para>
    /// <para>
    /// 試合が終わると、サーバーは <see cref="RestartDelaySeconds"/> 秒待って同じ Session で次の試合を始める
    /// (<see cref="TeamDeathmatchSession.Restart"/>)。チーム分けはそのままで、点数・キル数・デス数・残り時間を戻し、
    /// 全員を体力満タンで出現させる。待っている間の残り秒数は <see cref="NextMatchSeconds"/> で配る。
    /// 終わっている間に接続してきたプレイヤーは、次の試合が始まってから加える。
    /// </para>
    /// <para>
    /// 射撃は .Fire.cs、出現とリスポーンと出現直後の無敵 (<see cref="SpawnProtections"/>) は .Spawn.cs、武器を配る処理は .Loadout.cs、参加と退出は .Roster.cs、
    /// ダウンと蘇生は .Downed.cs、射撃・ヒット・被弾・キルをクライアントへ配る処理 (<see cref="NetCombatFeed"/>) は .CombatFeed.cs、
    /// グレネードを投げる・爆発させる処理は .Explosion.cs に分けてある。
    /// </para>
    /// <para>
    /// Bot で人数を埋める処理は .Bots.cs、放置した人を切る処理は .Idle.cs、拠点で勝敗を決める処理は .Objective.cs が受け持つ。
    /// それぞれの差し込み口を、サーバーで試合を始めたとき・毎フレーム・参加・退出・キル・次の試合の開始・試合の終わり・Despawn から呼ぶ。
    /// 試合の終わりでは、勝ったチームを <c>ObjectiveDecideWinner</c> が差し替えてから同期とログに使う。
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed partial class NetTeamDeathmatch : NetworkBehaviour
    {
        /// <summary>試合が終わってから次の試合を始めるまでの秒数の既定値。</summary>
        public const float DefaultRestartDelaySeconds = 10f;

        // まだ一度も位置を記録していないことを表す tick
        private const int _noRecordedTick = -1;

        // 不正な射撃の警告を出す最短の間隔 (秒)。連打でログを埋めないため
        private const float _rejectWarningInterval = 1f;

        [SerializeField, Min(1), Tooltip("どちらかのチームがこの点数に届いたら終わる")]
        private int _scoreLimit = 10;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = 300f;

        [SerializeField, Min(0f), Tooltip("倒されてからリスポーンするまでの秒数")]
        private float _respawnSeconds = 3f;

        [SerializeField, Min(0f), Tooltip("試合が終わってから次の試合を始めるまでの秒数")]
        private float _restartDelaySeconds = DefaultRestartDelaySeconds;

        [SerializeField, Min(1), Tooltip("プレイヤーの最大体力")]
        private int _maxHealth = 100;

        [SerializeField, Min(0f), Tooltip("この距離 (m) 以内に敵がいる地点には、他に候補があればリスポーンしない")]
        private float _minEnemySpawnDistance = SpawnSelector.DefaultMinEnemyDistance;

        [SerializeField, Tooltip("全員に持たせるロードアウト (Assets/game/Weapons/DefaultLoadout.asset)。ダメージ・射程・減衰・連射間隔・弾倉・持ち替え秒数はここから引く")]
        private LoadoutDefinition _loadout;

        [SerializeField, Tooltip("プレイヤーが自分のロードアウトに選べる武器の一覧 (Assets/game/Weapons/WeaponCatalog.asset)。選んだ並びは Loadout のスロットのルールで検査する。未設定なら全員 Loadout の武器のまま")]
        private WeaponCatalogAsset _catalog;

        [SerializeField, Min(0f), Tooltip("射撃の始点とサーバー上の本人の位置のずれ (m) の上限。超えた射撃は捨てる")]
        private float _maxShotOriginOffset = ShotValidator.DefaultMaxOriginOffset;

        [SerializeField, Tooltip("試合のルール (Assets/game/Rules/TeamDeathmatch.asset)。味方撃ち・リスポーン方式・回復・やられ演出・予備弾はここから引く。未設定なら既定のルールで始める")]
        private GameRulesDefinition _rules;

        private readonly NetworkVariable<int> _alphaScore = new NetworkVariable<int>();
        private readonly NetworkVariable<int> _bravoScore = new NetworkVariable<int>();
        private readonly NetworkVariable<int> _remainingSeconds = new NetworkVariable<int>();
        private readonly NetworkVariable<int> _nextMatchSeconds = new NetworkVariable<int>();
        private readonly NetworkVariable<int> _nextWaveSeconds = new NetworkVariable<int>();
        private readonly NetworkVariable<NetMatchStatus> _status =
            new NetworkVariable<NetMatchStatus>(new NetMatchStatus(GameModeState.Waiting, null));
        private readonly NetworkVariable<NetMatchRules> _syncedRules = new NetworkVariable<NetMatchRules>(NetMatchRules.Default);

        // 最後に倒した相手 (サーバーだけ)。NetPlayerStats.Killer で配る
        private readonly LastKillerTable _lastKillers = new LastKillerTable();

        // NetworkList は Awake で作る (フィールド初期化子だとエディタで破棄されずに残ることがある)
        private NetworkList<NetPlayerStats> _players;

        private readonly Dictionary<int, NetPlayer> _playerById = new Dictionary<int, NetPlayer>();
        private readonly HashSet<ulong> _rejectedClients = new HashSet<ulong>();
        private TeamDeathmatchSession _session;

        // ラグ補償 (サーバーだけ)。試合の開始時に作る
        private readonly HitboxRewinder _rewinder = new HitboxRewinder();
        private readonly List<RewindTarget> _rewindTargets = new List<RewindTarget>();
        private LagCompensator _lagCompensator;
        private int _lastRecordedTick = _noRecordedTick;

        // 試合の開始時に _loadout から作る。全員に配る武器と各プレイヤーの連射の制限 (サーバーだけ)
        private ServerLoadout _serverLoadout;
        private float _activeMaxShotOriginOffset;
        private float _nextRejectWarningTime;

        private static NetTeamDeathmatch _current;

        /// <summary>このプロセスで Spawn されている試合。サーバーでもクライアントでも設定される。無ければ null。</summary>
        public static NetTeamDeathmatch Current
        {
            get => _current;
            private set
            {
                if (_current == value)
                {
                    return;
                }

                _current = value;
                CurrentChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// <see cref="Current"/> が変わったときに新しい値 (消えたら null) を渡して呼ばれる。
        /// プレイヤーと試合のどちらが先に Spawn されるかは決まっていないので、HUD はこれで試合を待つ。
        /// </summary>
        public static event Action<NetTeamDeathmatch> CurrentChanged;

        /// <summary><see cref="NetHud"/> が OnValueChanged を購読する同期値。</summary>
        internal NetworkVariable<int> AlphaScoreVariable => _alphaScore;
        internal NetworkVariable<int> BravoScoreVariable => _bravoScore;
        internal NetworkVariable<int> RemainingSecondsVariable => _remainingSeconds;
        internal NetworkVariable<int> NextMatchSecondsVariable => _nextMatchSeconds;
        internal NetworkVariable<NetMatchStatus> StatusVariable => _status;
        internal NetworkVariable<int> NextWaveSecondsVariable => _nextWaveSeconds;
        internal NetworkVariable<NetMatchRules> RulesVariable => _syncedRules;

        public int AlphaScore => _alphaScore.Value;
        public int BravoScore => _bravoScore.Value;

        /// <summary>残り時間 (秒、切り上げ)。</summary>
        public int RemainingSeconds => _remainingSeconds.Value;

        /// <summary>
        /// 試合が終わった後、次の試合が始まるまでの残り秒数 (切り上げ)。試合中と開始前は 0。
        /// HUD は <see cref="State"/> が Ended の間これを <see cref="HudFormat.NextMatch"/> で出す。
        /// </summary>
        public int NextMatchSeconds => _nextMatchSeconds.Value;

        /// <summary>
        /// <see cref="RespawnMode.Wave"/> の試合中、次のウェーブまでの残り秒数 (切り上げ)。Wave 以外と試合中以外は 0。
        /// HUD は倒れている間これを出す。
        /// </summary>
        public int NextWaveSeconds => _nextWaveSeconds.Value;

        /// <summary>
        /// 試合のルールのうち、クライアントが見た目と HUD に使う値。サーバーが試合を始めた時点の <see cref="Rules"/> の値で、
        /// それまでは <see cref="NetMatchRules.Default"/>。
        /// </summary>
        public NetMatchRules SyncedRules => _syncedRules.Value;

        public GameModeState State => _status.Value.State;
        public Team? Winner => _status.Value.Winner;

        /// <summary>各プレイヤーのチーム・キル数・デス数。参加順に並ぶ。</summary>
        public NetworkList<NetPlayerStats> Players => _players;

        /// <summary>サーバーで試合が動いているか。設定やシーンに誤りがあると false のまま。</summary>
        public bool IsRunningOnServer => IsServer && _session != null;

        /// <summary>サーバーで動いている試合のルール。試合が動いていなければ (クライアントを含む) null。</summary>
        internal GameRules ServerRules => IsRunningOnServer ? _session.Spec.Rules : null;

        /// <summary>サーバーで動いている試合の最大体力。試合が動いていなければ 0。</summary>
        internal int ServerMaxHealth => IsRunningOnServer ? _session.MaxHealth : 0;

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

        /// <summary>試合が終わってから次の試合を始めるまでの秒数。0 以上の有限値。試合の開始時 (Spawn 時) に読む。</summary>
        public float RestartDelaySeconds
        {
            get => _restartDelaySeconds;
            set => _restartDelaySeconds = value;
        }

        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        public float MinEnemySpawnDistance
        {
            get => _minEnemySpawnDistance;
            set => _minEnemySpawnDistance = value;
        }

        /// <summary>
        /// 全員に持たせるロードアウトのアセット。試合の開始時 (Spawn 時) に <see cref="TryCreateLoadout"/> で読む。
        /// 未設定・不正なら、サーバーはエラーを出して試合を始めない。
        /// プレハブでは <c>Fps.Editor.NetSceneBuilder</c> が配線し、テストでは Spawn 前にここへ渡す。
        /// </summary>
        public LoadoutDefinition Loadout
        {
            get => _loadout;
            set => _loadout = value;
        }

        /// <summary>
        /// プレイヤーが <see cref="NetPlayerWeapon.RequestLoadout"/> で選べる武器の一覧のアセット。試合の開始時 (Spawn 時) に
        /// <see cref="TryCreateLoadout(LoadoutDefinition, WeaponCatalogAsset, out ServerLoadout, out string)"/> で読む。
        /// 未設定なら警告を出し、選んだ並びはすべて断って全員に <see cref="Loadout"/> の武器を配る。不正ならエラーを出して試合を始めない。
        /// クライアントは、持ち主が持たされた武器の射撃モードと連射間隔をここからも引く。
        /// プレハブでは <c>Fps.Editor.NetSceneBuilder</c> が配線し、テストでは Spawn 前にここへ渡す。
        /// </summary>
        public WeaponCatalogAsset Catalog
        {
            get => _catalog;
            set => _catalog = value;
        }

        /// <summary>
        /// 試合のルールのアセット。試合の開始時 (Spawn 時) に <see cref="TryLoadRules"/> で読む。
        /// 未設定なら警告を出して <see cref="GameRules.Default"/> で、不正ならエラーを出して試合を始めない。
        /// プレハブでは <c>Fps.Editor.NetSceneBuilder</c> が配線し、テストでは Spawn 前にここへ渡す。
        /// </summary>
        public GameRulesDefinition Rules
        {
            get => _rules;
            set => _rules = value;
        }

        /// <summary>サーバーで試合が動いている間の、全員に配る武器と各プレイヤーの連射の制限。試合の開始前とクライアントでは null。</summary>
        internal ServerLoadout ServerLoadout => _serverLoadout;

        private void Awake()
        {
            _players = new NetworkList<NetPlayerStats>();
            _spawnProtections = new NetworkList<NetSpawnProtection>();
        }

        public override void OnNetworkSpawn()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[Match] another match is already spawned; replacing " + Current.name);
            }

            Current = this;
            ResolveCombatFeed();
            _alphaScore.OnValueChanged += HandleAlphaScoreChanged;
            _bravoScore.OnValueChanged += HandleBravoScoreChanged;
            _status.OnValueChanged += HandleStatusChanged;

            if (IsServer)
            {
                StartSession();
                return;
            }

            // 途中から入ったクライアントにも、Spawn の時点で今の点数・残り時間・状態が届いていることをログで確かめられるようにする
            Debug.Log(MatchLog.Synced(_alphaScore.Value, _bravoScore.Value, _remainingSeconds.Value, _status.Value.State));
        }

        public override void OnNetworkDespawn()
        {
            _alphaScore.OnValueChanged -= HandleAlphaScoreChanged;
            _bravoScore.OnValueChanged -= HandleBravoScoreChanged;
            _status.OnValueChanged -= HandleStatusChanged;

            if (_session != null)
            {
                // 差し込み口が試合の状態を読んで片付けられるよう、Session を手放す前に呼ぶ
                BotsServerStopped();
                IdleServerStopped();
                ObjectiveServerStopped();
                UnsubscribeSession(_session);
                _session = null;
            }

            foreach (var player in _playerById.Values)
            {
                if (player != null)
                {
                    player.ServerPlayerId = NetPlayer.NotJoined;
                }
            }

            _playerById.Clear();
            _rejectedClients.Clear();
            _waitingForKey.Clear();
            _lagCompensator = null;
            _lastRecordedTick = _noRecordedTick;
            _rewindTargets.Clear();
            _serverLoadout = null;
            _lastKillers.Clear();
            ClearDowned();
            ClearGrenades();
            _combatFeed = null;
            _killShot = null;

            if (Current == this)
            {
                Current = null;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _session == null)
            {
                return;
            }

            // 終わっている間に来た人は、次の試合が始まってから加える
            if (_session.State != GameModeState.Ended)
            {
                JoinNewPlayers();
            }

            // 蘇生は Session の Tick より前に進める (前の Tick から進めなかった累計を Tick が 0 に戻すため)。
            // 終わっていれば次の試合までの待ちが進み、待ち終わったら Session が Restart する
            ServerAdvanceRevives(Time.deltaTime);
            _session.Tick(Time.deltaTime);
            BotsServerTick(Time.deltaTime);
            IdleServerTick(Time.deltaTime);
            ObjectiveServerTick(Time.deltaTime);
            SyncDownedStates();
            SyncSpawnProtections();
            SyncRemainingSeconds();
            SyncNextMatchSeconds();
            SyncNextWaveSeconds();
        }

        // 入力 (RPC) とホストの移動 (Update) が反映された後の位置を記録する
        private void LateUpdate()
        {
            if (!IsSpawned || !IsServer || _session == null || _lagCompensator == null)
            {
                return;
            }

            RecordHitboxes();
        }

        /// <summary>
        /// サーバーで attacker が victim に amount のダメージを与える。命中判定 (B/H) はここを呼ぶ。
        /// 体力が尽きたら Core 側で ReportKill され、点数とリスポーンが進む。ダウンが On なら倒さずにダウンさせ、
        /// <see cref="DamageOutcome.Downed"/> を返す (体力が変わったことは <see cref="DamageOutcome.Damaged"/> と同じに扱う)。
        /// attacker が null のダメージ (落下など) は victim 自身が与えたものとし、倒れたら自滅として数える (ダウンさせずに倒す)。
        /// </summary>
        /// <param name="attacker">撃った人。撃った人がいなければ null。</param>
        /// <param name="victim">ダメージを受ける人。</param>
        /// <param name="amount">ダメージ。0 以上。</param>
        /// <exception cref="InvalidOperationException">サーバー以外で呼んだ。</exception>
        public DamageOutcome ApplyDamage(NetPlayer attacker, NetPlayer victim, int amount)
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("damage can only be applied on the server");
            }

            if (victim == null)
            {
                throw new ArgumentNullException(nameof(victim));
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            // Session は攻撃者と被害者が同じ Id のダメージを自滅として扱うので、攻撃者のいないダメージはそれに寄せる
            var source = attacker != null ? attacker : victim;
            if (_session == null || !IsJoined(source) || !IsJoined(victim))
            {
                return DamageOutcome.Ignored;
            }

            return ApplySessionDamage(source.ServerPlayerId, victim.ServerPlayerId, amount);
        }

        private void StartSession()
        {
            if (!TryLoadRules(_rules, out var rules, out var usedDefaultRules, out var rulesError))
            {
                Debug.LogError("[Match] invalid game rules on " + name + ": " + rulesError
                    + "; fix the asset or run Fps.Editor.GameRulesAssetBuilder.Build");
                return;
            }

            if (usedDefaultRules)
            {
                Debug.LogWarning("[Match] no game rules are assigned to " + name + "; using the default rules"
                    + "; run Fps.Editor.NetSceneBuilder.Build");
            }

            TeamDeathmatchSpec spec;
            SpawnSelector selector;
            try
            {
                spec = new TeamDeathmatchSpec(_scoreLimit, _timeLimitSeconds, _respawnSeconds, rules);
                selector = new SpawnSelector(_minEnemySpawnDistance);
                ValidateShotSettings();
                ValidateRestartDelay();
                ValidateReconnectGrace();
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError("[Match] invalid match settings on " + name + ": " + e.Message);
                return;
            }

            if (!TryCreateLoadout(_loadout, _catalog, out var loadout, out var loadoutError))
            {
                Debug.LogError("[Match] invalid loadout on " + name + ": " + loadoutError + "; run Fps.Editor.NetSceneBuilder.Build");
                return;
            }

            if (_catalog == null)
            {
                Debug.LogWarning("[Match] no weapon catalog is assigned to " + name + "; every player keeps the default loadout"
                    + "; run Fps.Editor.NetSceneBuilder.Build");
            }

            var spawnPoints = CollectSpawnPoints();
            try
            {
                _session = new TeamDeathmatchSession(
                    spec, _maxHealth, spawnPoints, selector, PositionOf, _restartDelaySeconds, _reconnectGraceSeconds);
            }
            catch (ArgumentException e)
            {
                Debug.LogError("[Match] cannot start the match (" + spawnPoints.Count + " spawn points): " + e.Message
                    + "; run Fps.Editor.NetSceneBuilder.Build");
                return;
            }

            var structures = ApplyDestructionRule(CollectStructures(), spec.Rules);

            _serverLoadout = loadout;
            _activeMaxShotOriginOffset = _maxShotOriginOffset;
            _lagCompensator = new LagCompensator();
            _lastRecordedTick = _noRecordedTick;

            _session.Started += HandleStarted;
            _session.Restarted += HandleRestarted;
            _session.ScoreChanged += HandleScoreChanged;
            _session.PlayerChanged += HandlePlayerChanged;
            _session.Killed += HandleKilled;
            _session.Spawned += HandleSpawned;
            _session.Ended += HandleEnded;
            _session.HeldExpired += HandleHeldExpired;
            SubscribeDowned(_session);

            _lastKillers.Clear();
            ClearDowned();
            PrepareGrenades(spec.Rules);

            _alphaScore.Value = 0;
            _bravoScore.Value = 0;
            _syncedRules.Value = NetMatchRules.From(spec.Rules);
            _status.Value = new NetMatchStatus(_session.State, null);
            SyncRemainingSeconds();
            SyncNextMatchSeconds();
            SyncNextWaveSeconds();

            Debug.Log("[Match] waiting for players scoreLimit=" + spec.ScoreLimit + " timeLimit=" + spec.TimeLimitSeconds
                + "s respawn=" + spec.RespawnSeconds + "s restartDelay=" + _restartDelaySeconds + "s reconnectGrace=" + _reconnectGraceSeconds
                + "s spawnPoints=" + spawnPoints.Count);
            Debug.Log(MatchLog.Rules(usedDefaultRules ? "default" : _rules.name, spec.Rules));
            Debug.Log(NetDownedLog.Settings(spec.Rules.Actions, ReviveReach.DefaultRange));
            Debug.Log("[Match] destructible structures=" + structures + " destruction=" + spec.Rules.Destruction);
            Debug.Log("[Net] lag compensation maxRewindTicks=" + _lagCompensator.MaxRewindTicks
                + " tickRate=" + NetworkPlayerMotor.TickRate);
            Debug.Log(MatchLog.Loadout(_loadout.name, _serverLoadout));
            for (var i = 0; i < _serverLoadout.Weapons.Count; i++)
            {
                Debug.Log(MatchLog.Weapon(i, _serverLoadout.Weapons[i]));
            }

            if (_serverLoadout.Catalog != null)
            {
                Debug.Log(MatchLog.LoadoutCatalog(_catalog.name, _serverLoadout.Catalog.Count));
            }

            BotsServerStarted();
            IdleServerStarted();
            ObjectiveServerStarted();
        }

        /// <summary>
        /// 試合で使うルールを asset から読む (<see cref="GameRulesDefinition.TryToRules"/>)。
        /// asset が未設定 (破棄されたアセットを含む) なら <see cref="GameRules.Default"/> を rules に入れ、usedDefault を true にして true を返す
        /// (警告を出すかは呼び出し側が決める)。値が不正なら rules を null、error にアセット名を含む理由を入れて false を返す。
        /// </summary>
        public static bool TryLoadRules(GameRulesDefinition asset, out GameRules rules, out bool usedDefault, out string error)
        {
            // 参照切れ (破棄されたアセット) も Unity の == null で拾う
            if (asset == null)
            {
                rules = GameRules.Default;
                usedDefault = true;
                error = null;
                return true;
            }

            usedDefault = false;
            return asset.TryToRules(out rules, out error);
        }

        /// <summary>
        /// structures の全ての <see cref="DestructibleStructure.DestructionEnabled"/> を、rules の <see cref="GameRules.Destruction"/> が
        /// <see cref="Destruction.On"/> かどうかにそろえる。null の要素 (破棄された建物) は飛ばす。切り替えた建物の数を返す。
        /// </summary>
        internal static int ApplyDestructionRule(IEnumerable<DestructibleStructure> structures, GameRules rules)
        {
            if (structures == null)
            {
                throw new ArgumentNullException(nameof(structures));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var enabled = rules.Destruction == Destruction.On;
            var count = 0;
            foreach (var structure in structures)
            {
                if (structure == null)
                {
                    continue;
                }

                structure.DestructionEnabled = enabled;
                count++;
            }

            return count;
        }

        /// <summary>
        /// 壊せる建物をシーンから集める。後から有効にされる建物にもルールを効かせるよう、非アクティブなものも含める。
        /// </summary>
        private static List<DestructibleStructure> CollectStructures()
        {
            var structures = new List<DestructibleStructure>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    structures.AddRange(root.GetComponentsInChildren<DestructibleStructure>(true));
                }
            }

            return structures;
        }

        /// <summary>Inspector で入る射撃の検査の値を確かめる。おかしければ <see cref="ArgumentOutOfRangeException"/>。</summary>
        private void ValidateShotSettings()
        {
            if (float.IsNaN(_maxShotOriginOffset) || float.IsInfinity(_maxShotOriginOffset) || _maxShotOriginOffset < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(_maxShotOriginOffset), _maxShotOriginOffset, "max shot origin offset must be finite and not negative");
            }
        }

        /// <summary>Inspector で入る次の試合までの秒数を確かめる。おかしければ <see cref="ArgumentOutOfRangeException"/>。</summary>
        private void ValidateRestartDelay()
        {
            if (!(_restartDelaySeconds >= 0f) || float.IsInfinity(_restartDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(RestartDelaySeconds), _restartDelaySeconds, "restart delay must not be negative and must be finite");
            }
        }

        /// <summary>Inspector で入る切断した人の成績を残す秒数を確かめる。おかしければ <see cref="ArgumentOutOfRangeException"/>。</summary>
        private void ValidateReconnectGrace()
        {
            if (!(_reconnectGraceSeconds >= 0f) || float.IsInfinity(_reconnectGraceSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ReconnectGraceSeconds), _reconnectGraceSeconds, "reconnect grace must not be negative and must be finite");
            }
        }

        private void UnsubscribeSession(TeamDeathmatchSession session)
        {
            session.Started -= HandleStarted;
            session.Restarted -= HandleRestarted;
            session.ScoreChanged -= HandleScoreChanged;
            session.PlayerChanged -= HandlePlayerChanged;
            session.Killed -= HandleKilled;
            session.Spawned -= HandleSpawned;
            session.Ended -= HandleEnded;
            session.HeldExpired -= HandleHeldExpired;
            UnsubscribeDowned(session);
        }

        private void SyncRemainingSeconds()
        {
            var seconds = (int)Math.Ceiling(_session.RemainingSeconds);
            if (_remainingSeconds.Value != seconds)
            {
                _remainingSeconds.Value = seconds;
            }
        }

        private void SyncNextMatchSeconds()
        {
            var seconds = (int)Math.Ceiling(_session.RestartRemainingSeconds);
            if (_nextMatchSeconds.Value != seconds)
            {
                _nextMatchSeconds.Value = seconds;
            }
        }

        private void SyncNextWaveSeconds()
        {
            var remaining = _session.SecondsUntilNextWave;
            var seconds = remaining.HasValue ? HudFormat.DisplayedSeconds(remaining.Value) : 0;
            if (_nextWaveSeconds.Value != seconds)
            {
                _nextWaveSeconds.Value = seconds;
            }
        }

        private void HandleStarted()
        {
            _status.Value = new NetMatchStatus(GameModeState.Playing, null);
            SyncNextWaveSeconds();

            // 待っている間にリロードを始めていた人や投げていた人も、満タンでそろえて始める
            foreach (var pair in _playerById)
            {
                var player = pair.Value;
                if (player == null)
                {
                    continue;
                }

                if (player.Weapon != null)
                {
                    player.Weapon.ServerRefill();
                }

                ServerRefillGrenades(pair.Key, player);
            }

            Debug.Log(MatchLog.Started(_session.PlayerCount));
        }

        private void HandleScoreChanged(int alpha, int bravo)
        {
            _alphaScore.Value = alpha;
            _bravoScore.Value = bravo;
            Debug.Log(MatchLog.Score(alpha, bravo));
        }

        private void HandlePlayerChanged(int playerId)
        {
            if (!_playerById.TryGetValue(playerId, out var player))
            {
                return;
            }

            var team = _session.GetTeam(playerId);
            player.ServerSetState(team, _session.GetHealth(playerId));

            // playerId はクライアント Id をそのまま使っている (JoinNewPlayers) ので、抜けた相手でもクライアント Id に戻せる
            var killer = _lastKillers.Get(playerId);
            var row = new NetPlayerStats(
                player.OwnerClientId, team, _session.GetKills(playerId), _session.GetDeaths(playerId),
                killer.HasValue ? (ulong)killer.Value : (ulong?)null);
            var index = FindRow(row.ClientId);
            if (index < 0)
            {
                _players.Add(row);
            }
            else if (!_players[index].Equals(row))
            {
                _players[index] = row;
            }
        }

        // Session は Killed の直後に倒された人の PlayerChanged を出すので、ここで記録すればその行に載る
        private void HandleKilled(int killerId, int victimId)
        {
            _lastKillers.Record(killerId, victimId);
            var byAlly = killerId != victimId && _session.GetTeam(killerId) == _session.GetTeam(victimId);
            Debug.Log(byAlly ? MatchLog.TeamKill(killerId, victimId) : MatchLog.Kill(killerId, victimId));
            SendKill(killerId, victimId, byAlly);
            BotsPlayerKilled(killerId, victimId);
            IdlePlayerKilled(killerId, victimId);
            ObjectivePlayerKilled(killerId, victimId);
        }

        private void HandleEnded(Team? winner)
        {
            ObjectiveDecideWinner(ref winner);
            _status.Value = new NetMatchStatus(GameModeState.Ended, winner);
            SyncRemainingSeconds();
            SyncNextMatchSeconds();
            SyncNextWaveSeconds();
            Debug.Log(MatchLog.Ended(winner));
            Debug.Log(MatchLog.NextMatch(_session.RestartRemainingSeconds));
            BotsMatchEnded(winner);
            IdleMatchEnded(winner);
            ObjectiveMatchEnded(winner);
        }

        // Session が点数・キル数・時間・体力を戻した直後。続けて点数・各プレイヤー・出現の知らせが来て、
        // 両チームがそろっていれば HandleStarted で Playing になる
        private void HandleRestarted()
        {
            _status.Value = new NetMatchStatus(GameModeState.Waiting, null);
            SyncRemainingSeconds();
            SyncNextMatchSeconds();
            SyncNextWaveSeconds();
            _serverLoadout?.ResetBudgets();

            // 続く PlayerChanged で各行が倒した相手なしに戻る。続く DownedChanged でダウンしていた人の状態が戻る
            _lastKillers.Clear();
            _reviveHolds.Clear();
            Debug.Log(MatchLog.Restart());
            BotsMatchRestarted();
            IdleMatchRestarted();
            ObjectiveMatchRestarted();
        }

        // クライアントでも同じ書式でログを出し、サーバーのログと突き合わせられるようにする。
        // サーバー (ホストを含む) は Session のイベントで出しているので二重にしない。
        private void HandleAlphaScoreChanged(int previous, int current)
        {
            if (!IsServer)
            {
                Debug.Log(MatchLog.Score(current, _bravoScore.Value));
            }
        }

        private void HandleBravoScoreChanged(int previous, int current)
        {
            if (!IsServer)
            {
                Debug.Log(MatchLog.Score(_alphaScore.Value, current));
            }
        }

        private void HandleStatusChanged(NetMatchStatus previous, NetMatchStatus current)
        {
            if (IsServer || previous.State == current.State)
            {
                return;
            }

            if (current.State == GameModeState.Ended)
            {
                Debug.Log(MatchLog.Ended(current.Winner));
                return;
            }

            // サーバーでは同じフレームに Waiting → Playing と変わるので、クライアントには Ended → Playing で届くことがある
            if (previous.State == GameModeState.Ended)
            {
                Debug.Log(MatchLog.Restart());
            }

            if (current.State == GameModeState.Playing)
            {
                Debug.Log(MatchLog.Tag + " started");
            }
        }
    }
}
