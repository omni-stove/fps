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
    /// サーバーは接続順 (<see cref="NetworkManager.ConnectedClientsList"/> の順) にプレイヤーを試合に加える。
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
    /// 未設定・不正なら試合を始めない。全員に同じロードアウトを、出現・リスポーンのたびに <see cref="NetPlayerWeapon.ServerEquip"/> で持たせる。
    /// 持ち替えは <see cref="NetPlayerWeapon"/> がサーバーで判定し、射撃の判定 (ダメージ・射程・減衰・連射間隔) には
    /// 撃った人がその時に持っている武器 (<see cref="NetPlayerWeapon.CurrentSpec"/>) の値を使う。
    /// クライアントは撃った武器の Id だけを送り、ロードアウトに無い・今持っている武器と違う射撃は捨てる。
    /// </para>
    /// <para>
    /// 弾倉と予備弾はサーバーが各プレイヤーの <see cref="NetPlayerWeapon"/> で持つ。出現・リスポーンで武器ごと渡し直し、試合の開始で満タンに戻す。
    /// 射撃は当たり判定の前に 1 発消費し、弾が無ければ捨てる。
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

        /// <summary>サーバーで試合が動いている間の、全員に配る武器と各プレイヤーの連射の制限。試合の開始前とクライアントでは null。</summary>
        internal ServerLoadout ServerLoadout => _serverLoadout;

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
            _rewindTargets.Clear();
            _serverLoadout = null;

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
        /// サーバーで shooter の射撃 1 発を処理する。撃った武器・連射の間隔・始点を確かめ、弾倉から 1 発消費してから、shooter が見ていた時点 time まで
        /// 他のプレイヤーを巻き戻して Raycast し、当たればダメージを与えて <c>[Net] hit attacker=.. victim=.. rewindTicks=..</c> を出す。
        /// 巻き戻せるのは <see cref="LagCompensator.MaxRewindTicks"/> tick 前まで。
        /// 射程・ダメージ・減衰は、サーバーが shooter に今持たせている武器 (<see cref="NetPlayerWeapon.CurrentSpec"/>) の値を使う (クライアントの値は使わない)。
        /// </summary>
        /// <param name="shooter">撃った人。試合に参加していて生きていなければ何もしない。</param>
        /// <param name="weaponId">クライアントが送ってきた、撃った武器の Id。ロードアウトに無い・今持っている武器と違えば捨てる。</param>
        /// <param name="origin">クライアントが送ってきた始点。</param>
        /// <param name="direction">クライアントが送ってきた向き。</param>
        /// <param name="time">クライアントが見ていた時点。</param>
        internal void ServerFire(NetPlayer shooter, string weaponId, Vector3 origin, Vector3 direction, ShotTime time)
        {
            if (!IsServer || _session == null || _lagCompensator == null || _serverLoadout == null || shooter == null || !IsJoined(shooter))
            {
                return;
            }

            var shooterId = shooter.ServerPlayerId;
            if (_session.State != GameModeState.Playing || !_session.IsAlive(shooterId))
            {
                return;
            }

            // 武器の Id を確かめ、今の武器の連射間隔 (ShotBudget) で制限する。ServerWeapon は弾倉とリロードだけを見る。
            // RPC の届く時刻は揺れるので、間隔どおりの射撃がまとめて届いても Burst 発までは受け入れ、速すぎる連打だけを捨てる
            var weaponCheck = CheckWeaponShot(_serverLoadout, shooterId, shooter.Weapon, weaponId, NetworkManager.ServerTime.Time, out var spec);
            if (weaponCheck != ShotRejection.None)
            {
                WarnRejected(shooterId, weaponCheck);
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

            // 不正な射撃では弾を減らさないよう、検査を通ってから 1 発消費する。弾倉が空ならここでリロードが始まる
            var weapon = shooter.Weapon;
            var ammo = weapon != null ? weapon.ServerTryFire() : ShotRejection.NoWeapon;
            if (ammo != ShotRejection.None)
            {
                WarnRejected(shooterId, ammo);
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
            var rewoundCrouched = _lagCompensator.RewindCrouched(rewindTick);

            _rewindTargets.Clear();
            foreach (var pair in _playerById)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                var hittable = pair.Key != shooterId && _session.HasPlayer(pair.Key) && _session.IsAlive(pair.Key);
                _rewindTargets.Add(new RewindTarget(pair.Key, pair.Value.transform, hittable, pair.Value.Stance));
            }

            RewindHit hit;
            bool any;
            try
            {
                any = _rewinder.Raycast(
                    _rewindTargets, rewound, rewoundCrouched, origin, direction, spec.Range, shooter.transform, out hit);
            }
            finally
            {
                _rewindTargets.Clear();
            }

            if (!any)
            {
                return;
            }

            if (!hit.HitTarget)
            {
                if (_session.Spec.Rules.Destruction == Destruction.On)
                {
                    StructureDamage.Apply(hit.RaycastHit.collider, hit.RaycastHit.point, spec.Damage, spec.ExplosionRadius);
                }

                return;
            }

            if (!_playerById.TryGetValue(hit.TargetId, out var victim) || victim == null)
            {
                return;
            }

            var zone = ZoneOf(victim, hit);
            var damage = DamageCalculator.Calculate(spec.Damage, zone, hit.RaycastHit.distance, spec.Falloff);
            var outcome = ApplyDamage(shooter, victim, damage);
            var health = _session.HasPlayer(hit.TargetId) ? _session.GetHealth(hit.TargetId) : 0;
            Debug.Log(NetHitLog.Hit(shooterId, hit.TargetId, currentTick - rewindTick, zone, damage, health, outcome, hit.TargetCrouched));
        }

        /// <summary>
        /// <see cref="ServerFire"/> の武器の検査。weapon が今持っている武器 (<see cref="NetPlayerWeapon.CurrentSpec"/>、持ち替え中は持ち替え先) で
        /// weaponId と連射の制限を確かめ、通ればその武器を spec に返す。持ち替えていれば、連射の制限はその武器の間隔で作り直す。
        /// weapon が null か装備前なら <see cref="ShotRejection.NoWeapon"/> (weaponId がロードアウトにあるとき)。
        /// </summary>
        internal static ShotRejection CheckWeaponShot(
            ServerLoadout loadout, int shooterId, NetPlayerWeapon weapon, string weaponId, double nowSeconds, out WeaponSpec spec)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            var held = weapon != null ? weapon.CurrentSpec : null;
            var rejection = loadout.TryFire(shooterId, held, weaponId, nowSeconds);
            spec = rejection == ShotRejection.None ? held : null;
            return rejection;
        }

        /// <summary>
        /// 出現・リスポーンで weapon に loadout の武器を全部満タンで渡し直し、先頭 (rifle) を持ち替えの待ち時間なしで構えさせる。
        /// 持ち替え中・リロード中だった状態と、倒れる前の残弾は捨てる。weapon が null なら何もしない。
        /// </summary>
        internal static void ArmForSpawn(ServerLoadout loadout, NetPlayerWeapon weapon)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            // ServerLoadout は開始時に ServerEquip と同じ条件で検査済みなので、ここでは投げない
            if (weapon != null)
            {
                weapon.ServerEquip(loadout.Weapons, loadout.SwitchSeconds);
            }
        }

        // サーバーの知る本人の目の位置 (今の姿勢の高さ) から始点までの間に、プレイヤー以外のコライダー (壁など) があれば true。
        private static bool IsOriginBlocked(NetPlayer shooter, Vector3 origin)
        {
            var eye = shooter.EyePosition();
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
            _serverLoadout?.Remove(playerId);
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
            try
            {
                spec = new TeamDeathmatchSpec(_scoreLimit, _timeLimitSeconds, _respawnSeconds);
                selector = new SpawnSelector(_minEnemySpawnDistance);
                ValidateShotSettings();
                ValidateRestartDelay();
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError("[Match] invalid match settings on " + name + ": " + e.Message);
                return;
            }

            if (!TryCreateLoadout(_loadout, out var loadout, out var loadoutError))
            {
                Debug.LogError("[Match] invalid loadout on " + name + ": " + loadoutError + "; run Fps.Editor.NetSceneBuilder.Build");
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

            _alphaScore.Value = 0;
            _bravoScore.Value = 0;
            _status.Value = new NetMatchStatus(_session.State, null);
            SyncRemainingSeconds();
            SyncNextMatchSeconds();

            Debug.Log("[Match] waiting for players scoreLimit=" + spec.ScoreLimit + " timeLimit=" + spec.TimeLimitSeconds
                + "s respawn=" + spec.RespawnSeconds + "s restartDelay=" + _restartDelaySeconds + "s spawnPoints=" + spawnPoints.Count
                + " friendlyFire=" + spec.Rules.FriendlyFire);
            Debug.Log("[Net] lag compensation maxRewindTicks=" + _lagCompensator.MaxRewindTicks
                + " tickRate=" + NetworkPlayerMotor.TickRate);
            Debug.Log(MatchLog.Loadout(_loadout.name, _serverLoadout));
            for (var i = 0; i < _serverLoadout.Weapons.Count; i++)
            {
                Debug.Log(MatchLog.Weapon(i, _serverLoadout.Weapons[i]));
            }
        }

        /// <summary>
        /// asset のルール (<see cref="LoadoutDefinition.ToRule"/>) と武器 (<see cref="LoadoutDefinition.ToSpecs"/>) を検査し、
        /// 試合で使う <see cref="Fps.Core.ServerLoadout"/> にする。asset が未設定・ルールか武器が不正・ルールに合わない・
        /// <see cref="NetPlayerWeapon.MaxWeaponCount"/> 本より多い・RPC で送れない長さの Id がある、のどれかなら false と理由を返す。
        /// </summary>
        public static bool TryCreateLoadout(LoadoutDefinition asset, out ServerLoadout loadout, out string error)
        {
            loadout = null;

            // 参照切れ (破棄されたアセット) も Unity の == null で拾う
            if (asset == null)
            {
                error = "no loadout is assigned";
                return false;
            }

            LoadoutRule rule;
            IReadOnlyList<WeaponSpec> specs;
            try
            {
                rule = asset.ToRule();
                specs = asset.ToSpecs();
            }
            catch (WeaponDefinitionException e)
            {
                error = e.Message;
                return false;
            }

            if (specs.Count > NetPlayerWeapon.MaxWeaponCount)
            {
                error = "loadout '" + asset.name + "' has " + specs.Count + " weapons but at most "
                    + NetPlayerWeapon.MaxWeaponCount + " can be synced";
                return false;
            }

            foreach (var weapon in specs)
            {
                if (!NetPlayerWeapon.FitsWeaponId(weapon.Id))
                {
                    error = "weapon id '" + weapon.Id + "' in loadout '" + asset.name + "' is longer than "
                        + NetPlayerWeapon.MaxWeaponIdBytes + " bytes and cannot be sent with a shot";
                    return false;
                }
            }

            try
            {
                // ToRule と ToSpecs の間にアセットが書き換わっても、ここで同じルールに合うことを確かめ直す
                loadout = new ServerLoadout(rule, specs, asset.SwitchSeconds);
            }
            catch (ArgumentException e)
            {
                error = "loadout '" + asset.name + "' is invalid: " + e.Message;
                return false;
            }

            error = null;
            return true;
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

                if (player.Weapon == null)
                {
                    // 弾数はサーバーが NetPlayerWeapon で持つので、無いと撃てない。古いプレハブのまま起動したとき
                    _rejectedClients.Add(client.ClientId);
                    Debug.LogError("[Match] player object of client " + client.ClientId + " has no NetPlayerWeapon; run Fps.Editor.NetSceneBuilder.Build");
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

                // 武器は Join の中の出現 (HandleSpawned) で持たせる
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

        /// <summary>参加中の全プレイヤーの今の位置としゃがみを、サーバーの今の tick で記録する。</summary>
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
                    _lagCompensator.Record(tick, pair.Key, NetMath.ToNumerics(pair.Value.transform.position), pair.Value.IsCrouched);
                }
            }

            _lastRecordedTick = tick;
        }

        /// <summary>
        /// 当たった高さから部位を決める。プレイヤーの当たりは CharacterController のカプセル 1 つなので、
        /// 巻き戻した位置と巻き戻した時点の姿勢 (<see cref="RewindHit.TargetCrouched"/>) のカプセルの中の高さの割合で
        /// <see cref="CapsuleHitZone"/> に決めさせる。姿勢を持たないプレイヤーは今の CharacterController の高さで決める。
        /// </summary>
        private static HitZone ZoneOf(NetPlayer victim, RewindHit hit)
        {
            var scale = victim.transform.lossyScale.y;
            var stance = victim.Stance;
            if (stance != null)
            {
                return ZoneOf(stance.Geometry.Shape(hit.TargetCrouched), scale, hit);
            }

            var controller = victim.GetComponent<CharacterController>();
            if (controller == null)
            {
                return HitZone.Body;
            }

            return ZoneOf(new StanceShape(controller.height, controller.center.y, 0f), scale, hit);
        }

        /// <summary>
        /// 当たった高さから部位を決める。shape は当たったときのカプセルの寸法 (本体のローカル)、scaleY は本体の縦の拡大率。
        /// カプセルの底は巻き戻した位置 (<see cref="RewindHit.TargetPosition"/>) から測る。高さが 0 以下か有限でなければ胴体。
        /// </summary>
        internal static HitZone ZoneOf(StanceShape shape, float scaleY, RewindHit hit)
        {
            var height = shape.Height * scaleY;
            if (!(height > 0f) || float.IsInfinity(height))
            {
                return HitZone.Body;
            }

            var bottom = hit.TargetPosition.y + (shape.CenterY * scaleY) - (height / 2f);
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

            // 待っている間にリロードを始めていた人も、満タンでそろえて始める
            foreach (var player in _playerById.Values)
            {
                if (player != null && player.Weapon != null)
                {
                    player.Weapon.ServerRefill();
                }
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
            var byAlly = killerId != victimId && _session.GetTeam(killerId) == _session.GetTeam(victimId);
            Debug.Log(byAlly ? MatchLog.TeamKill(killerId, victimId) : MatchLog.Kill(killerId, victimId));
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

            ArmForSpawn(_serverLoadout, player.Weapon);

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
            _serverLoadout?.ResetBudgets();
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
