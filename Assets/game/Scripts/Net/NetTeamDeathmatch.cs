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
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetTeamDeathmatch : NetworkBehaviour
    {
        [SerializeField, Min(1), Tooltip("どちらかのチームがこの点数に届いたら終わる")]
        private int _scoreLimit = 10;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = 300f;

        [SerializeField, Min(0f), Tooltip("倒されてからリスポーンするまでの秒数")]
        private float _respawnSeconds = 3f;

        [SerializeField, Min(1), Tooltip("プレイヤーの最大体力")]
        private int _maxHealth = 100;

        [SerializeField, Min(0f), Tooltip("この距離 (m) 以内に敵がいる地点には、他に候補があればリスポーンしない")]
        private float _minEnemySpawnDistance = SpawnSelector.DefaultMinEnemyDistance;

        [SerializeField, Min(0), Tooltip("動作確認用の攻撃 (Attack 入力) 1 回のダメージ")]
        private int _debugAttackDamage = 50;

        private readonly NetworkVariable<int> _alphaScore = new NetworkVariable<int>();
        private readonly NetworkVariable<int> _bravoScore = new NetworkVariable<int>();
        private readonly NetworkVariable<int> _remainingSeconds = new NetworkVariable<int>();
        private readonly NetworkVariable<NetMatchStatus> _status =
            new NetworkVariable<NetMatchStatus>(new NetMatchStatus(GameModeState.Waiting, null));

        // NetworkList は Awake で作る (フィールド初期化子だとエディタで破棄されずに残ることがある)
        private NetworkList<NetPlayerStats> _players;

        private readonly Dictionary<int, NetPlayer> _playerById = new Dictionary<int, NetPlayer>();
        private readonly HashSet<ulong> _rejectedClients = new HashSet<ulong>();
        private TeamDeathmatchSession _session;

        /// <summary>このプロセスで Spawn されている試合。サーバーでもクライアントでも設定される。無ければ null。</summary>
        public static NetTeamDeathmatch Current { get; private set; }

        public int AlphaScore => _alphaScore.Value;
        public int BravoScore => _bravoScore.Value;

        /// <summary>残り時間 (秒、切り上げ)。</summary>
        public int RemainingSeconds => _remainingSeconds.Value;

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

        public int DebugAttackDamage
        {
            get => _debugAttackDamage;
            set => _debugAttackDamage = value;
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

            JoinNewPlayers();
            _session.Tick(Time.deltaTime);
            SyncRemainingSeconds();
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

        /// <summary>動作確認用の攻撃。いちばん近い生きている敵に <see cref="DebugAttackDamage"/> を与える。</summary>
        internal void ServerDebugAttack(NetPlayer attacker)
        {
            if (_session == null || !IsJoined(attacker))
            {
                return;
            }

            var targetId = _session.FindNearestEnemy(attacker.ServerPlayerId);
            if (targetId == null || !_playerById.TryGetValue(targetId.Value, out var target))
            {
                Debug.Log("[Match] attack player=" + attacker.ServerPlayerId + " found no target");
                return;
            }

            // 倒したときの kill / score のログより先に出るよう、ダメージを与える前に書く
            Debug.Log("[Match] attack player=" + attacker.ServerPlayerId + " target=" + targetId.Value
                + " damage=" + _debugAttackDamage + " healthBefore=" + _session.GetHealth(targetId.Value));
            ApplyDamage(attacker, target, _debugAttackDamage);
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
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError("[Match] invalid match settings on " + name + ": " + e.Message);
                return;
            }

            var spawnPoints = CollectSpawnPoints();
            try
            {
                _session = new TeamDeathmatchSession(spec, _maxHealth, spawnPoints, selector, PositionOf);
            }
            catch (ArgumentException e)
            {
                Debug.LogError("[Match] cannot start the match (" + spawnPoints.Count + " spawn points): " + e.Message
                    + "; run Fps.Editor.NetSceneBuilder.Build");
                return;
            }

            _session.Started += HandleStarted;
            _session.ScoreChanged += HandleScoreChanged;
            _session.PlayerChanged += HandlePlayerChanged;
            _session.Killed += HandleKilled;
            _session.Spawned += HandleSpawned;
            _session.Ended += HandleEnded;

            _alphaScore.Value = 0;
            _bravoScore.Value = 0;
            _status.Value = new NetMatchStatus(_session.State, null);
            SyncRemainingSeconds();

            Debug.Log("[Match] waiting for players scoreLimit=" + spec.ScoreLimit + " timeLimit=" + spec.TimeLimitSeconds
                + "s respawn=" + spec.RespawnSeconds + "s spawnPoints=" + spawnPoints.Count);
        }

        private void UnsubscribeSession(TeamDeathmatchSession session)
        {
            session.Started -= HandleStarted;
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

            var p = point.Position;
            player.ServerTeleport(new Vector3(p.X, p.Y, p.Z));
            Debug.Log(MatchLog.Spawn(playerId, point));
        }

        private void HandleEnded(Team? winner)
        {
            _status.Value = new NetMatchStatus(GameModeState.Ended, winner);
            SyncRemainingSeconds();
            Debug.Log(MatchLog.Ended(winner));
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
            }
            else if (current.State == GameModeState.Playing)
            {
                Debug.Log(MatchLog.Tag + " started");
            }
        }
    }
}
