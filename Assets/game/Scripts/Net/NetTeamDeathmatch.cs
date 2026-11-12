using System;
using System.Collections.Generic;
using Fps.Core;
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
    /// サーバーは接続順 (<see cref="NetworkManager.ConnectedClientsList"/> の順) にプレイヤーを試合に加える。
    /// ダメージはすべて <see cref="ApplyDamage"/> を通し、体力が尽きたら Core 側が ReportKill する。
    /// 出現・リスポーンの地点は Core の SpawnSelector が選び、ここでは <see cref="NetPlayer.ServerTeleport"/> を呼ぶだけ。
    /// <para>
    /// 射撃はサーバー権威のヒットスキャンで、ラグ補償をかける。毎フレームの終わりに全プレイヤーの位置を
    /// サーバーの tick で <see cref="LagCompensator"/> に記録し、射撃 (<see cref="ServerFire"/>) が届いたら
    /// 撃った側が見ていた時点まで巻き戻した位置で <see cref="HitboxRewinder"/> が Raycast する。
    /// ダメージ量は <see cref="DamageCalculator"/>、体力は Session の <see cref="Health"/> が決め、
    /// 体力は <see cref="NetPlayer"/> の NetworkVariable で配る。
    /// </para>
    /// <para>
    /// 試合が終わると、サーバーは <see cref="RestartDelaySeconds"/> 秒待って同じ Session で次の試合を始める
    /// (<see cref="TeamDeathmatchSession.Restart"/>)。チーム分けはそのままで、点数・キル数・デス数・残り時間を戻し、
    /// 全員を体力満タンで出現させる。待っている間の残り秒数は <see cref="NextMatchSeconds"/> で配る。
    /// 終わっている間に接続してきたプレイヤーは、次の試合が始まってから加える。
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetTeamDeathmatch : NetworkBehaviour
    {
        /// <summary>武器の既定値。オフラインの WeaponController と同じ値にそろえる。</summary>
        public const int DefaultWeaponDamage = 25;
        public const float DefaultWeaponRange = 100f;
        public const float DefaultFireInterval = 0.1f;
        public const float DefaultFalloffStart = 20f;
        public const float DefaultFalloffEnd = 60f;
        public const float DefaultFalloffMinMultiplier = 0.5f;

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

        [SerializeField, Min(0), Tooltip("ヒットスキャン 1 発の基本ダメージ。部位倍率と距離減衰はこれに掛ける")]
        private int _weaponDamage = DefaultWeaponDamage;

        [SerializeField, Min(0.01f), Tooltip("ヒットスキャンの射程 (m)")]
        private float _weaponRange = DefaultWeaponRange;

        [SerializeField, Min(0f), Tooltip("サーバーが受け付ける射撃の間隔 (秒)。これより速い連射は捨てる")]
        private float _fireInterval = DefaultFireInterval;

        [SerializeField, Min(0f), Tooltip("この距離 (m) までは減衰しない")]
        private float _falloffStart = DefaultFalloffStart;

        [SerializeField, Min(0f), Tooltip("この距離 (m) 以上で最小倍率になる")]
        private float _falloffEnd = DefaultFalloffEnd;

        [SerializeField, UnityEngine.Range(0f, 1f), Tooltip("最大まで減衰したときの倍率")]
        private float _falloffMinMultiplier = DefaultFalloffMinMultiplier;

        [SerializeField, Min(0f), Tooltip("射撃の始点とサーバー上の本人の位置のずれ (m) の上限。超えた射撃は捨てる")]
        private float _maxShotOriginOffset = ShotValidator.DefaultMaxOriginOffset;

        private readonly NetworkVariable<int> _alphaScore = new NetworkVariable<int>();
        private readonly NetworkVariable<int> _bravoScore = new NetworkVariable<int>();
        private readonly NetworkVariable<int> _remainingSeconds = new NetworkVariable<int>();
        private readonly NetworkVariable<int> _nextMatchSeconds = new NetworkVariable<int>();
        private readonly NetworkVariable<NetMatchStatus> _status =
            new NetworkVariable<NetMatchStatus>(new NetMatchStatus(GameModeState.Waiting, null));

        // NetworkList は Awake で作る (フィールド初期化子だとエディタで破棄されずに残ることがある)
        private NetworkList<NetPlayerStats> _players;

        private readonly Dictionary<int, NetPlayer> _playerById = new Dictionary<int, NetPlayer>();
        private readonly HashSet<ulong> _rejectedClients = new HashSet<ulong>();
        private TeamDeathmatchSession _session;

        // ラグ補償 (サーバーだけ)。試合の開始時に作る
        private readonly HitboxRewinder _rewinder = new HitboxRewinder();
        private readonly List<RewindTarget> _rewindTargets = new List<RewindTarget>();
        private readonly Dictionary<int, ShotBudget> _shotBudgets = new Dictionary<int, ShotBudget>();
        private LagCompensator _lagCompensator;
        private int _lastRecordedTick = _noRecordedTick;
        private DamageFalloff _falloff;
        private int _activeWeaponDamage;
        private float _activeWeaponRange;
        private float _activeFireInterval;
        private float _activeMaxShotOriginOffset;
        private float _nextRejectWarningTime;

        /// <summary>このプロセスで Spawn されている試合。サーバーでもクライアントでも設定される。無ければ null。</summary>
        public static NetTeamDeathmatch Current { get; private set; }

        public int AlphaScore => _alphaScore.Value;
        public int BravoScore => _bravoScore.Value;

        /// <summary>残り時間 (秒、切り上げ)。</summary>
        public int RemainingSeconds => _remainingSeconds.Value;

        /// <summary>
        /// 試合が終わった後、次の試合が始まるまでの残り秒数 (切り上げ)。試合中と開始前は 0。
        /// HUD は <see cref="State"/> が Ended の間これを <see cref="HudFormat.NextMatch"/> で出す。
        /// </summary>
        public int NextMatchSeconds => _nextMatchSeconds.Value;

        public GameModeState State => _status.Value.State;
        public Team? Winner => _status.Value.Winner;

        /// <summary>各プレイヤーのチーム・キル数・デス数。参加順に並ぶ。</summary>
        public NetworkList<NetPlayerStats> Players => _players;

        /// <summary>サーバーで試合が動いているか。設定やシーンに誤りがあると false のまま。</summary>
        public bool IsRunningOnServer => IsServer && _session != null;

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

        /// <summary>ヒットスキャン 1 発の基本ダメージ。0 以上。試合の開始時に読む。</summary>
        public int WeaponDamage
        {
            get => _weaponDamage;
            set => _weaponDamage = value;
        }

        /// <summary>ヒットスキャンの射程 (m)。0 より大きい有限値。試合の開始時に読む。</summary>
        public float WeaponRange
        {
            get => _weaponRange;
            set => _weaponRange = value;
        }

        /// <summary>サーバーが受け付ける射撃の間隔 (秒)。0 以上。試合の開始時に読む。</summary>
        public float FireInterval
        {
            get => _fireInterval;
            set => _fireInterval = value;
        }

        private void Awake()
        {
            _players = new NetworkList<NetPlayerStats>();
        }

        public override void OnNetworkSpawn()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[Match] another match is already spawned; replacing " + Current.name);
            }

            Current = this;
            _alphaScore.OnValueChanged += HandleAlphaScoreChanged;
            _bravoScore.OnValueChanged += HandleBravoScoreChanged;
            _status.OnValueChanged += HandleStatusChanged;

            if (IsServer)
            {
                StartSession();
            }
        }

        public override void OnNetworkDespawn()
        {
            _alphaScore.OnValueChanged -= HandleAlphaScoreChanged;
            _bravoScore.OnValueChanged -= HandleBravoScoreChanged;
            _status.OnValueChanged -= HandleStatusChanged;

            if (_session != null)
            {
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
            _lagCompensator = null;
            _lastRecordedTick = _noRecordedTick;
            _shotBudgets.Clear();
            _rewindTargets.Clear();

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

            // 終わっていれば次の試合までの待ちが進み、待ち終わったら Session が Restart する
            _session.Tick(Time.deltaTime);
            SyncRemainingSeconds();
            SyncNextMatchSeconds();
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
        /// 体力が尽きたら Core 側で ReportKill され、点数とリスポーンが進む。
        /// </summary>
        /// <exception cref="InvalidOperationException">サーバー以外で呼んだ。</exception>
        public DamageOutcome ApplyDamage(NetPlayer attacker, NetPlayer victim, int amount)
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("damage can only be applied on the server");
            }

            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (victim == null)
            {
                throw new ArgumentNullException(nameof(victim));
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            if (_session == null || !IsJoined(attacker) || !IsJoined(victim))
            {
                return DamageOutcome.Ignored;
            }

            return _session.ApplyDamage(attacker.ServerPlayerId, victim.ServerPlayerId, amount);
        }

        /// <summary>
        /// サーバーで shooter の射撃 1 発を処理する。連射の間隔と始点を確かめてから、shooter が見ていた時点 time まで
        /// 他のプレイヤーを巻き戻して Raycast し、当たればダメージを与えて <c>[Net] hit attacker=.. victim=.. rewindTicks=..</c> を出す。
        /// 巻き戻せるのは <see cref="LagCompensator.MaxRewindTicks"/> tick 前まで。
        /// </summary>
        /// <param name="shooter">撃った人。試合に参加していて生きていなければ何もしない。</param>
        /// <param name="origin">クライアントが送ってきた始点。</param>
        /// <param name="direction">クライアントが送ってきた向き。</param>
        /// <param name="time">クライアントが見ていた時点。</param>
        internal void ServerFire(NetPlayer shooter, Vector3 origin, Vector3 direction, ShotTime time)
        {
            if (!IsServer || _session == null || _lagCompensator == null || shooter == null || !IsJoined(shooter))
            {
                return;
            }

            var shooterId = shooter.ServerPlayerId;
            if (_session.State != GameModeState.Playing || !_session.IsAlive(shooterId))
            {
                return;
            }

            if (!_shotBudgets.TryGetValue(shooterId, out var budget))
            {
                budget = new ShotBudget(_activeFireInterval);
                _shotBudgets.Add(shooterId, budget);
            }

            if (!budget.TryConsume(NetworkManager.ServerTime.Time))
            {
                WarnRejected(shooterId, ShotRejection.FireRateExceeded);
                return;
            }

            var rejection = ShotValidator.Check(
                NetMath.ToNumerics(origin),
                NetMath.ToNumerics(direction),
                time,
                NetMath.ToNumerics(shooter.transform.position),
                _activeMaxShotOriginOffset);
            if (rejection != ShotRejection.None)
            {
                WarnRejected(shooterId, rejection);
                return;
            }

            if (IsOriginBlocked(shooter, origin))
            {
                WarnRejected(shooterId, ShotRejection.OriginBlocked);
                return;
            }

            if (_lastRecordedTick == _noRecordedTick)
            {
                // 試合が始まった直後のフレームで、まだ巻き戻す履歴が無い
                return;
            }

            var currentTick = _lastRecordedTick;
            var rewindTick = _lagCompensator.ClampTick(time.ToTick(), currentTick);
            var rewound = _lagCompensator.Rewind(rewindTick);

            _rewindTargets.Clear();
            foreach (var pair in _playerById)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                var hittable = pair.Key != shooterId && _session.HasPlayer(pair.Key) && _session.IsAlive(pair.Key);
                _rewindTargets.Add(new RewindTarget(pair.Key, pair.Value.transform, hittable));
            }

            RewindHit hit;
            bool any;
            try
            {
                any = _rewinder.Raycast(_rewindTargets, rewound, origin, direction, _activeWeaponRange, shooter.transform, out hit);
            }
            finally
            {
                _rewindTargets.Clear();
            }

            if (!any || !hit.HitTarget || !_playerById.TryGetValue(hit.TargetId, out var victim) || victim == null)
            {
                return;
            }

            var zone = ZoneOf(victim, hit);
            var damage = DamageCalculator.Calculate(_activeWeaponDamage, zone, hit.RaycastHit.distance, _falloff);
            var outcome = ApplyDamage(shooter, victim, damage);
            var health = _session.HasPlayer(hit.TargetId) ? _session.GetHealth(hit.TargetId) : 0;
            Debug.Log(NetHitLog.Hit(shooterId, hit.TargetId, currentTick - rewindTick, zone, damage, health, outcome));
        }

        // サーバーの知る本人の目の位置から始点までの間に、プレイヤー以外のコライダー (壁など) があれば true。
        private static bool IsOriginBlocked(NetPlayer shooter, Vector3 origin)
        {
            var eye = shooter.transform.position + (Vector3.up * NetPlayer.EyeHeight);
            var toOrigin = origin - eye;
            var distance = toOrigin.magnitude;
            if (distance <= 0f)
            {
                return false;
            }

            var hits = Physics.RaycastAll(eye, toOrigin / distance, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<NetPlayer>() == null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>プレイヤーの NetworkObject が消えるとき (切断を含む) にサーバーで呼ぶ。</summary>
        internal void ServerLeave(NetPlayer player)
        {
            if (!IsServer || player == null || !IsJoined(player))
            {
                return;
            }

            var playerId = player.ServerPlayerId;
            player.ServerPlayerId = NetPlayer.NotJoined;
            _playerById.Remove(playerId);
            _lagCompensator?.Remove(playerId);
            _shotBudgets.Remove(playerId);
            if (_session != null && _session.HasPlayer(playerId))
            {
                _session.Leave(playerId);
            }

            Debug.Log(MatchLog.Left(playerId));

            // 停止中は配る先がもう無いので、同期する一覧には触らない
            if (NetworkManager != null && NetworkManager.ShutdownInProgress)
            {
                return;
            }

            var index = FindRow(player.OwnerClientId);
            if (index >= 0)
            {
                _players.RemoveAt(index);
            }
        }

        private void StartSession()
        {
            TeamDeathmatchSpec spec;
            SpawnSelector selector;
            DamageFalloff falloff;
            try
            {
                spec = new TeamDeathmatchSpec(_scoreLimit, _timeLimitSeconds, _respawnSeconds);
                selector = new SpawnSelector(_minEnemySpawnDistance);
                falloff = new DamageFalloff(_falloffStart, _falloffEnd, _falloffMinMultiplier);
                ValidateWeaponSettings();
                ValidateRestartDelay();
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError("[Match] invalid match settings on " + name + ": " + e.Message);
                return;
            }

            var spawnPoints = CollectSpawnPoints();
            try
            {
                _session = new TeamDeathmatchSession(spec, _maxHealth, spawnPoints, selector, PositionOf, _restartDelaySeconds);
            }
            catch (ArgumentException e)
            {
                Debug.LogError("[Match] cannot start the match (" + spawnPoints.Count + " spawn points): " + e.Message
                    + "; run Fps.Editor.NetSceneBuilder.Build");
                return;
            }

            _falloff = falloff;
            _activeWeaponDamage = _weaponDamage;
            _activeWeaponRange = _weaponRange;
            _activeFireInterval = _fireInterval;
            _activeMaxShotOriginOffset = _maxShotOriginOffset;
            _lagCompensator = new LagCompensator();
            _lastRecordedTick = _noRecordedTick;
            _shotBudgets.Clear();

            _session.Started += HandleStarted;
            _session.Restarted += HandleRestarted;
            _session.ScoreChanged += HandleScoreChanged;
            _session.PlayerChanged += HandlePlayerChanged;
            _session.Killed += HandleKilled;
            _session.Spawned += HandleSpawned;
            _session.Ended += HandleEnded;

            _alphaScore.Value = 0;
            _bravoScore.Value = 0;
            _status.Value = new NetMatchStatus(_session.State, null);
            SyncRemainingSeconds();
            SyncNextMatchSeconds();

            Debug.Log("[Match] waiting for players scoreLimit=" + spec.ScoreLimit + " timeLimit=" + spec.TimeLimitSeconds
                + "s respawn=" + spec.RespawnSeconds + "s restartDelay=" + _restartDelaySeconds + "s spawnPoints=" + spawnPoints.Count);
            Debug.Log("[Net] lag compensation maxRewindTicks=" + _lagCompensator.MaxRewindTicks
                + " tickRate=" + NetworkPlayerMotor.TickRate + " damage=" + _activeWeaponDamage + " range=" + _activeWeaponRange
                + " fireInterval=" + _activeFireInterval);
        }

        /// <summary>Inspector で入る武器の値を確かめる。おかしければ <see cref="ArgumentOutOfRangeException"/>。</summary>
        private void ValidateWeaponSettings()
        {
            if (_weaponDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(WeaponDamage), _weaponDamage, "weapon damage must not be negative");
            }

            if (!(_weaponRange > 0f) || float.IsInfinity(_weaponRange))
            {
                throw new ArgumentOutOfRangeException(nameof(WeaponRange), _weaponRange, "weapon range must be positive and finite");
            }

            if (float.IsNaN(_maxShotOriginOffset) || float.IsInfinity(_maxShotOriginOffset) || _maxShotOriginOffset < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(_maxShotOriginOffset), _maxShotOriginOffset, "max shot origin offset must be finite and not negative");
            }

            // 間隔の検査は ShotBudget に任せる (不正なら ArgumentOutOfRangeException)
            _ = new ShotBudget(_fireInterval);
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

        private void UnsubscribeSession(TeamDeathmatchSession session)
        {
            session.Started -= HandleStarted;
            session.Restarted -= HandleRestarted;
            session.ScoreChanged -= HandleScoreChanged;
            session.PlayerChanged -= HandlePlayerChanged;
            session.Killed -= HandleKilled;
            session.Spawned -= HandleSpawned;
            session.Ended -= HandleEnded;
        }

        /// <summary>まだ試合に入っていない接続済みプレイヤーを、接続順に加える。</summary>
        private void JoinNewPlayers()
        {
            var clients = NetworkManager.ConnectedClientsList;
            for (var i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                var playerObject = client.PlayerObject;
                if (playerObject == null || !playerObject.IsSpawned || _rejectedClients.Contains(client.ClientId))
                {
                    continue;
                }

                var player = playerObject.GetComponent<NetPlayer>();
                if (player == null)
                {
                    _rejectedClients.Add(client.ClientId);
                    Debug.LogError("[Match] player object of client " + client.ClientId + " has no NetPlayer; run Fps.Editor.NetSceneBuilder.Build");
                    continue;
                }

                if (IsJoined(player))
                {
                    continue;
                }

                if (client.ClientId > int.MaxValue)
                {
                    _rejectedClients.Add(client.ClientId);
                    Debug.LogError("[Match] client id " + client.ClientId + " does not fit a player id");
                    continue;
                }

                var playerId = (int)client.ClientId;
                if (_session.HasPlayer(playerId))
                {
                    // 同じクライアントの前のプレイヤーオブジェクトが抜け損ねている。二重に数えないよう加えない
                    _rejectedClients.Add(client.ClientId);
                    Debug.LogError("[Match] client " + client.ClientId + " is already in the match with another player object");
                    continue;
                }

                player.ServerPlayerId = playerId;
                _playerById[playerId] = player;
                Debug.Log(MatchLog.Joined(playerId, _session.NextTeam));
                _session.Join(playerId);
            }
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

        /// <summary>参加中の全プレイヤーの今の位置を、サーバーの今の tick で記録する。</summary>
        private void RecordHitboxes()
        {
            var time = NetworkManager.ServerTime.Time;
            if (double.IsNaN(time) || time < 0d)
            {
                return;
            }

            var tick = FireTiming.TickAt(time, NetworkPlayerMotor.TickRate);
            if (tick < _lastRecordedTick)
            {
                // サーバーの時刻は戻らないはずだが、履歴は古い tick を受け付けないので念のため飛ばす
                return;
            }

            foreach (var pair in _playerById)
            {
                if (pair.Value != null)
                {
                    _lagCompensator.Record(tick, pair.Key, NetMath.ToNumerics(pair.Value.transform.position));
                }
            }

            _lastRecordedTick = tick;
        }

        /// <summary>
        /// 当たった高さから部位を決める。プレイヤーの当たりは CharacterController のカプセル 1 つなので、
        /// 巻き戻した位置を基準にカプセルの中の高さの割合で <see cref="CapsuleHitZone"/> に決めさせる。
        /// </summary>
        private static HitZone ZoneOf(NetPlayer victim, RewindHit hit)
        {
            var controller = victim.GetComponent<CharacterController>();
            if (controller == null)
            {
                return HitZone.Body;
            }

            var scale = victim.transform.lossyScale.y;
            var height = controller.height * scale;
            if (!(height > 0f) || float.IsInfinity(height))
            {
                return HitZone.Body;
            }

            var bottom = hit.TargetPosition.y + (controller.center.y * scale) - (height / 2f);
            return CapsuleHitZone.Classify(hit.RaycastHit.point.y, bottom, height);
        }

        private void WarnRejected(int shooterId, ShotRejection reason)
        {
            if (Time.unscaledTime < _nextRejectWarningTime)
            {
                return;
            }

            _nextRejectWarningTime = Time.unscaledTime + _rejectWarningInterval;
            Debug.LogWarning(NetHitLog.Rejected(shooterId, reason), this);
        }

        private System.Numerics.Vector3 PositionOf(int playerId)
        {
            var p = _playerById[playerId].transform.position;
            return new System.Numerics.Vector3(p.x, p.y, p.z);
        }

        private bool IsJoined(NetPlayer player)
        {
            return player.ServerPlayerId != NetPlayer.NotJoined
                && _playerById.TryGetValue(player.ServerPlayerId, out var joined)
                && joined == player;
        }

        private int FindRow(ulong clientId)
        {
            for (var i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 出現地点の目印 (<see cref="NetSpawnPoint"/>) をシーンから集める。並び順は SpawnSelector の同点時の決め手になるので、
        /// 読み込まれたシーンの階層順で固定する。
        /// </summary>
        private static List<SpawnPoint> CollectSpawnPoints()
        {
            var points = new List<SpawnPoint>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var marker in root.GetComponentsInChildren<NetSpawnPoint>())
                    {
                        points.Add(marker.ToSpawnPoint());
                    }
                }
            }

            return points;
        }

        private void HandleStarted()
        {
            _status.Value = new NetMatchStatus(GameModeState.Playing, null);
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

            var row = new NetPlayerStats(player.OwnerClientId, team, _session.GetKills(playerId), _session.GetDeaths(playerId));
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

        private void HandleKilled(int killerId, int victimId)
        {
            Debug.Log(MatchLog.Kill(killerId, victimId));
        }

        private void HandleSpawned(int playerId, SpawnPoint point)
        {
            if (!_playerById.TryGetValue(playerId, out var player))
            {
                return;
            }

            // テレポートの前後を補間して途中の位置に当たらないよう、履歴を捨てて出現地点から記録し直す。
            // 出現より前の時点まで巻き戻した射撃には、その時点にいなかったものとして当たらない
            _lagCompensator?.Remove(playerId);

            var p = point.Position;
            player.ServerTeleport(new Vector3(p.X, p.Y, p.Z));
            Debug.Log(MatchLog.Spawn(playerId, point));
        }

        private void HandleEnded(Team? winner)
        {
            _status.Value = new NetMatchStatus(GameModeState.Ended, winner);
            SyncRemainingSeconds();
            SyncNextMatchSeconds();
            Debug.Log(MatchLog.Ended(winner));
            Debug.Log(MatchLog.NextMatch(_session.RestartRemainingSeconds));
        }

        // Session が点数・キル数・時間・体力を戻した直後。続けて点数・各プレイヤー・出現の知らせが来て、
        // 両チームがそろっていれば HandleStarted で Playing になる
        private void HandleRestarted()
        {
            _status.Value = new NetMatchStatus(GameModeState.Waiting, null);
            SyncRemainingSeconds();
            SyncNextMatchSeconds();
            _shotBudgets.Clear();
            Debug.Log(MatchLog.Restart());
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
