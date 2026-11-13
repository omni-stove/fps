using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、人が足りない分 (<c>-bots</c> か <see cref="GameRules.MinPlayers"/>) を
    /// サーバーだけで動く Bot で埋め、人が入ったら抜けさせる処理。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 人数: 人と Bot を合わせて <see cref="BotRoster.TargetPlayers"/> 人になるまで Bot を足す (<see cref="BotRoster.DesiredBots"/>)。
    /// 人が 1 人もいなければ Bot も置かない。Bot はクライアントを持たない <see cref="NetPlayer"/> で、接続したクライアントと同じ
    /// プレイヤーのプレハブ (<see cref="NetworkConfig.PlayerPrefab"/>) をサーバーが持ち主のまま Spawn し、
    /// プレイヤー Id は <see cref="BotRoster.FirstBotPlayerId"/> から数える。チームは人と同じく Session が人数の少ない側に決める。
    /// 人が入ったら、その人のチームの Bot を 1 人抜く (いなければ人数の多いチームから。<see cref="BotRoster.TeamToTrim"/>)。
    /// 人が抜けたら、次のフレームで足りない分を足す。試合が終わっている間は足さない (抜くのはいつでも)。
    /// </para>
    /// <para>
    /// 動き: Bot ごとに <see cref="EnemyBrain"/> が判断し、<see cref="BotInputPlanner"/> が人と同じ入力に変える。移動は
    /// <see cref="NetworkPlayerMotor.ServerApplyBotInput"/> で人の入力と同じ確定処理を通し、射撃は <see cref="ServerFire"/> に渡して
    /// 人と同じ検査 (武器・連射・始点・弾数) を通す。tick は人と同じ <see cref="NetworkPlayerMotor.TickSeconds"/> ごと。
    /// 経路は NavMesh (<c>NavMesh.CalculatePath</c>) で引き、NavMesh が無ければ目的地へまっすぐ進む。
    /// 相手が見えないときはシーンの出現地点を Bot ごとに並べ替えた順に回り、撃たれたら近い敵の方を探しに行く。
    /// </para>
    /// <para>
    /// 差し込み口はどれもサーバーでだけ、試合が動いている (Session がある) 間に呼ぶ。Bot の参加は放置の判定 (.Idle.cs) には渡さず
    /// (入力の無い Bot を切らないため)、拠点 (.Objective.cs) には人と同じく渡す。退出は ServerLeave がどちらにも渡す。
    /// </para>
    /// </remarks>
    public sealed partial class NetTeamDeathmatch
    {
        /// <summary>Bot の出し入れと動きのログの頭。</summary>
        public const string BotLogTag = "[Bot]";

        // Bot の判断の距離と時間。NetTest (60m 四方) で、出会った相手と撃ち合える距離にする
        private const float _botSightRange = 40f;
        private const float _botAttackRange = 25f;
        private const float _botLoseSightSeconds = 2f;
        private const float _botSearchSeconds = 6f;
        private const float _botReactionSeconds = 0.6f;

        // 本体 (カプセルの中心) は床から 1m 上にあるので、NavMesh を探す半径はそれより広くとる
        private const float _botNavMeshSampleRadius = 2.5f;

        // 経路を引き直す間隔と、目的地がこれ以上動いたら待たずに引き直す距離
        private const float _botRepathSeconds = 0.5f;
        private const float _botRepathDistance = 1f;

        // この秒数のうちにこの距離 (水平) しか進めなければ、向かっている先をあきらめる
        private const float _botStuckSeconds = 2f;
        private const float _botStuckProgress = 0.5f;

        // 重いフレームの後に追いつこうとして 1 フレームで何十 tick も回さないための上限
        private const int _botMaxTicksPerFrame = 4;
        private const int _botMaxPathCorners = 64;

        private const int _noBot = NetPlayer.NotJoined;

        private readonly Dictionary<int, BotAgent> _bots = new Dictionary<int, BotAgent>();
        private readonly List<int> _botIds = new List<int>();
        private readonly Queue<Team> _botTrimRequests = new Queue<Team>();
        private readonly List<System.Numerics.Vector3> _botPatrolPoints = new List<System.Numerics.Vector3>();
        private int _launchBots;
        private int _nextBotPlayerId = BotRoster.FirstBotPlayerId;
        private int _botTarget;
        private int _loggedDesiredBots = -1;
        private bool _botsHaveNavMesh;

        // 生成できない (プレハブが無い・壊れている) と分かったら、毎フレーム作り直してログを埋めないよう止める
        private bool _botsUnavailable;

        /// <summary>
        /// 起動引数 <c>-bots</c> (<see cref="NetLaunchOptions.Bots"/>) の数。<see cref="GameRules.MinPlayers"/> と大きいほうの人数まで Bot で埋める。
        /// 試合の開始時 (Spawn 時) に読むので、<see cref="NetBootstrap"/> が Spawn の前に渡す。0 以上 <see cref="NetLaunchOptions.MaxBots"/> 以下。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">範囲外の値を入れた。</exception>
        public int LaunchBots
        {
            get => _launchBots;
            set
            {
                if (value < 0 || value > NetLaunchOptions.MaxBots)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "bots must be between 0 and " + NetLaunchOptions.MaxBots);
                }

                _launchBots = value;
            }
        }

        /// <summary>サーバーで今試合にいる Bot の数。クライアントでは 0。</summary>
        public int BotCount => _bots.Count;

        /// <summary>サーバーで試合を始められた (Session を作った) ときに呼ぶ。<see cref="ServerRules"/> が読める。</summary>
        partial void BotsServerStarted();

        /// <summary>サーバーの毎フレーム、Session を進めた直後に呼ぶ。</summary>
        /// <param name="deltaSeconds">前のフレームからの秒数。</param>
        partial void BotsServerTick(float deltaSeconds);

        /// <summary>プレイヤーを試合に加えた直後に呼ぶ。Bot を加えたときは呼ばない。</summary>
        /// <param name="playerId">加えたプレイヤーの Id (クライアント Id と同じ値)。</param>
        /// <param name="player">加えたプレイヤー。</param>
        partial void BotsPlayerJoined(int playerId, NetPlayer player);

        /// <summary>プレイヤーを試合から外した直後に呼ぶ (切断と Bot を抜いたときを含む)。</summary>
        /// <param name="playerId">外したプレイヤーの Id。</param>
        partial void BotsPlayerLeft(int playerId);

        /// <summary>プレイヤーが倒されたときに呼ぶ。自滅なら killerId と victimId が同じ。</summary>
        partial void BotsPlayerKilled(int killerId, int victimId);

        /// <summary>試合が終わって次の試合のために点数などを戻した直後に呼ぶ。</summary>
        partial void BotsMatchRestarted();

        /// <summary>試合が終わったときに呼ぶ。</summary>
        /// <param name="winner">勝ったチーム (<c>ObjectiveDecideWinner</c> の後の値)。引き分けなら null。</param>
        partial void BotsMatchEnded(Team? winner);

        /// <summary>サーバーで試合を Despawn するとき、Session を手放す前に呼ぶ。</summary>
        partial void BotsServerStopped();

        partial void BotsServerStarted()
        {
            _bots.Clear();
            _botTrimRequests.Clear();
            _loggedDesiredBots = -1;
            _botsUnavailable = false;
            _botTarget = BotRoster.TargetPlayers(_launchBots, ServerRules.MinPlayers);

            _botPatrolPoints.Clear();
            foreach (var point in CollectSpawnPoints())
            {
                _botPatrolPoints.Add(point.Position);
            }

            Debug.Log(BotLogTag + " fill target=" + _botTarget.ToString(CultureInfo.InvariantCulture)
                + " bots=" + _launchBots.ToString(CultureInfo.InvariantCulture)
                + " minPlayers=" + ServerRules.MinPlayers.ToString(CultureInfo.InvariantCulture));
            if (_botTarget == 0)
            {
                return;
            }

            _botsHaveNavMesh = HasNavMeshNear(_botPatrolPoints);
            if (!_botsHaveNavMesh)
            {
                Debug.LogWarning(BotLogTag + " no NavMesh near the spawn points; bots walk straight to their goals"
                    + "; run Fps.Editor.NetSceneBuilder.Build");
            }

            if (NetworkManager.NetworkConfig.PlayerPrefab == null)
            {
                _botsUnavailable = true;
                Debug.LogError(BotLogTag + " the NetworkManager has no player prefab; bots are disabled");
            }
        }

        partial void BotsServerTick(float deltaSeconds)
        {
            if (_botTarget > 0)
            {
                ReconcileBots();
            }

            DriveBots(deltaSeconds);
        }

        partial void BotsPlayerJoined(int playerId, NetPlayer player)
        {
            if (player.IsBot || _botTarget == 0)
            {
                return;
            }

            // 抜くのは次の ReconcileBots。足りているかは人数を数え直して決める
            _botTrimRequests.Enqueue(_session.GetTeam(playerId));
        }

        partial void BotsPlayerLeft(int playerId)
        {
            // Bot を抜いたとき (RemoveBot の Despawn) も、外から Despawn されたときもここを通る
            _bots.Remove(playerId);
        }

        partial void BotsPlayerKilled(int killerId, int victimId)
        {
            if (_bots.TryGetValue(victimId, out var agent))
            {
                agent.MarkDead();
            }
        }

        partial void BotsMatchRestarted()
        {
            // Session は全員を体力満タンで出し直すので、判断も巡回の最初からやり直す
            foreach (var agent in _bots.Values)
            {
                agent.MarkDead();
            }
        }

        partial void BotsMatchEnded(Team? winner)
        {
            Debug.Log(BotLogTag + " match ended bots=" + _bots.Count.ToString(CultureInfo.InvariantCulture));
        }

        partial void BotsServerStopped()
        {
            var shuttingDown = NetworkManager == null || NetworkManager.ShutdownInProgress;
            _botIds.Clear();
            _botIds.AddRange(_bots.Keys);
            _bots.Clear();
            foreach (var playerId in _botIds)
            {
                if (!_playerById.TryGetValue(playerId, out var player) || player == null)
                {
                    continue;
                }

                // 試合はこの後片付けるので、Despawn で ServerLeave を通さない (同期する一覧を消えかけの試合で書き換えない)
                player.ServerPlayerId = NetPlayer.NotJoined;
                _playerById.Remove(playerId);
                if (!shuttingDown && player.NetworkObject != null && player.NetworkObject.IsSpawned)
                {
                    player.NetworkObject.Despawn(true);
                }
            }

            _botIds.Clear();
            _botTrimRequests.Clear();
            _botPatrolPoints.Clear();
            _botTarget = 0;
        }

        /// <summary>人の数から置く Bot の数を決め、多ければ抜き、少なければ足す。</summary>
        private void ReconcileBots()
        {
            var humans = 0;
            foreach (var player in _playerById.Values)
            {
                if (player != null && !player.IsBot)
                {
                    humans++;
                }
            }

            var desired = BotRoster.DesiredBots(_launchBots, _session.Spec.Rules.MinPlayers, humans);
            if (desired != _loggedDesiredBots)
            {
                _loggedDesiredBots = desired;
                Debug.Log(BotLogTag + " humans=" + humans.ToString(CultureInfo.InvariantCulture)
                    + " bots=" + _bots.Count.ToString(CultureInfo.InvariantCulture)
                    + " desired=" + desired.ToString(CultureInfo.InvariantCulture));
            }

            while (_bots.Count > desired)
            {
                Team? preferred = _botTrimRequests.Count > 0 ? _botTrimRequests.Dequeue() : (Team?)null;
                if (!TrimBot(preferred, humans > 0 ? "replaced" : "no-humans"))
                {
                    break;
                }
            }

            // 足りている間に入った人の分は、抜く必要が無かったので忘れる
            _botTrimRequests.Clear();

            if (_session.State == GameModeState.Ended)
            {
                return;
            }

            while (_bots.Count < desired && !_botsUnavailable)
            {
                if (!AddBot())
                {
                    break;
                }
            }
        }

        /// <summary><see cref="BotRoster.TeamToTrim"/> で決めたチームの、いちばん後に入った Bot を抜く。抜けなければ false。</summary>
        private bool TrimBot(Team? preferred, string reason)
        {
            int alphaPlayers = 0, bravoPlayers = 0, alphaBots = 0, bravoBots = 0;
            foreach (var playerId in _playerById.Keys)
            {
                if (!_session.HasPlayer(playerId))
                {
                    continue;
                }

                var bravo = _session.GetTeam(playerId) == Team.Bravo;
                var bot = _bots.ContainsKey(playerId);
                if (bravo)
                {
                    bravoPlayers++;
                    bravoBots += bot ? 1 : 0;
                }
                else
                {
                    alphaPlayers++;
                    alphaBots += bot ? 1 : 0;
                }
            }

            var team = BotRoster.TeamToTrim(preferred, alphaPlayers, bravoPlayers, alphaBots, bravoBots);
            var victim = _noBot;
            if (team.HasValue)
            {
                foreach (var playerId in _bots.Keys)
                {
                    if (_session.HasPlayer(playerId) && _session.GetTeam(playerId) == team.Value && playerId > victim)
                    {
                        victim = playerId;
                    }
                }
            }

            // Session にいない Bot (参加し損ねたもの) が残っていれば、それから片付ける
            if (victim == _noBot)
            {
                foreach (var playerId in _bots.Keys)
                {
                    if (!_session.HasPlayer(playerId))
                    {
                        victim = playerId;
                        break;
                    }
                }
            }

            if (victim == _noBot)
            {
                return false;
            }

            RemoveBot(victim, reason);
            return true;
        }

        /// <summary>Bot を 1 人生成して Spawn し、試合に加える。生成できなければ以後 Bot を止めて false。</summary>
        private bool AddBot()
        {
            if (_nextBotPlayerId == int.MaxValue)
            {
                _botsUnavailable = true;
                Debug.LogError(BotLogTag + " ran out of bot player ids; bots are disabled");
                return false;
            }

            var prefab = NetworkManager.NetworkConfig.PlayerPrefab;
            var playerId = _nextBotPlayerId;
            GameObject instance = null;
            NetPlayer player;
            NetworkPlayerMotor motor;
            try
            {
                instance = Instantiate(prefab);
                instance.name = prefab.name + " " + BotRoster.KillFeedName(playerId);
                player = instance.GetComponent<NetPlayer>();
                motor = instance.GetComponent<NetworkPlayerMotor>();
                if (player == null || player.Weapon == null || motor == null)
                {
                    // 人のプレイヤーと同じく、弾数と移動はサーバーがこれらの部品で持つので、無いと人と同じ検査を通せない
                    throw new InvalidOperationException("player prefab " + prefab.name
                        + " needs NetPlayer, NetPlayerWeapon and NetworkPlayerMotor; run Fps.Editor.NetSceneBuilder.Build");
                }

                player.ServerMarkBot(playerId);
                instance.GetComponent<NetworkObject>().Spawn(true);
            }
            catch (Exception e)
            {
                // 生成と Spawn は Netcode の外の境界なので、何が飛んできても Bot を止めてログに変え、試合は続ける
                Debug.LogException(e);
                if (instance != null)
                {
                    Destroy(instance);
                }

                _botsUnavailable = true;
                Debug.LogError(BotLogTag + " could not spawn a bot; bots are disabled");
                return false;
            }

            _nextBotPlayerId++;
            if (!motor.AcceptsBotInput)
            {
                // 設定の誤りで NetworkPlayerMotor が動けない (エラーはそちらが出している)
                player.NetworkObject.Despawn(true);
                _botsUnavailable = true;
                Debug.LogError(BotLogTag + " " + instance.name + " cannot be moved by the server; bots are disabled");
                return false;
            }

            var agent = new BotAgent(playerId, motor, ShuffledPatrolRoute(playerId));
            player.ServerPlayerId = playerId;
            _playerById[playerId] = player;
            _bots[playerId] = agent;

            // 武器は Join の中の出現 (HandleSpawned) で持たせる
            var team = _session.NextTeam;
            Debug.Log(MatchLog.Joined(playerId, team));
            Debug.Log(BotLogTag + " added player=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " name=" + BotRoster.ScoreboardName(playerId) + " team=" + team);
            _session.Join(playerId);
            ObjectivePlayerJoined(playerId, player);
            return true;
        }

        private void RemoveBot(int playerId, string reason)
        {
            _bots.Remove(playerId);
            Debug.Log(BotLogTag + " removed player=" + playerId.ToString(CultureInfo.InvariantCulture) + " reason=" + reason);
            if (!_playerById.TryGetValue(playerId, out var player) || player == null)
            {
                return;
            }

            // Despawn で NetPlayer.OnNetworkDespawn → ServerLeave を通り、人が抜けたときと同じく Session と一覧から外れる
            var networkObject = player.NetworkObject;
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn(true);
                return;
            }

            ServerLeave(player);
            Destroy(player.gameObject);
        }

        private List<System.Numerics.Vector3> ShuffledPatrolRoute(int playerId)
        {
            var route = new List<System.Numerics.Vector3>(_botPatrolPoints);
            var random = new System.Random(playerId);
            for (var i = route.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (route[i], route[j]) = (route[j], route[i]);
            }

            return route;
        }

        private static bool HasNavMeshNear(IReadOnlyList<System.Numerics.Vector3> points)
        {
            foreach (var point in points)
            {
                if (NavMesh.SamplePosition(NetMath.ToUnity(point), out _, _botNavMeshSampleRadius, NavMesh.AllAreas))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>全ての Bot を、フレームの経過時間で溜まった tick の数だけ進める。</summary>
        private void DriveBots(float deltaSeconds)
        {
            if (_bots.Count == 0)
            {
                return;
            }

            _botIds.Clear();
            _botIds.AddRange(_bots.Keys);
            foreach (var playerId in _botIds)
            {
                if (!_bots.TryGetValue(playerId, out var agent)
                    || !_playerById.TryGetValue(playerId, out var player)
                    || player == null
                    || !_session.HasPlayer(playerId))
                {
                    continue;
                }

                var ticks = agent.Advance(deltaSeconds, NetworkPlayerMotor.TickSeconds, _botMaxTicksPerFrame);
                try
                {
                    for (var i = 0; i < ticks; i++)
                    {
                        StepBot(agent, player, playerId);
                    }
                }
                catch (ArgumentException e)
                {
                    // 位置や向きが壊れた (有限でない) Bot は、同じ例外でログを埋めないよう抜く
                    Debug.LogException(e);
                    RemoveBot(playerId, "error");
                }
                catch (InvalidOperationException e)
                {
                    Debug.LogException(e);
                    RemoveBot(playerId, "error");
                }
            }

            _botIds.Clear();
        }

        /// <summary>Bot を 1 tick 進める。判断・経路・入力の変換をして、人と同じ移動と射撃に渡す。</summary>
        private void StepBot(BotAgent agent, NetPlayer player, int playerId)
        {
            var dt = NetworkPlayerMotor.TickSeconds;
            if (!_session.IsAlive(playerId))
            {
                agent.MarkDead();
                return;
            }

            agent.ReviveIfDead();
            agent.Clock += dt;

            var position = player.transform.position;
            var eye = player.EyePosition();
            var yaw = player.transform.eulerAngles.y * Mathf.Deg2Rad;
            var thinking = _session.State != GameModeState.Ended && !_session.IsDowned(playerId);
            var decision = thinking ? agent.Brain.Tick(dt, Perceive(agent, player, playerId, position, eye)) : EnemyDecision.None;

            IReadOnlyList<Vector3> corners = null;
            if (decision.MoveTo.HasValue)
            {
                var destination = NetMath.ToUnity(decision.MoveTo.Value);
                if (_botsHaveNavMesh && !agent.TryPath(position, destination))
                {
                    // 届かない先 (NavMesh の外の相手など) は Brain にあきらめさせ、次の tick に別の先を選ばせる
                    agent.Brain.AbandonGoal();
                }

                corners = _botsHaveNavMesh ? agent.Corners : null;
            }

            var input = agent.Planner.Plan(dt, decision, position, eye, yaw, agent.Pitch, corners);
            agent.Pitch = input.Pitch;
            agent.Motor.ServerApplyBotInput(input.Motor);

            if (agent.IsStuck(position, decision.MoveTo.HasValue && input.Motor.Move.sqrMagnitude > 0f))
            {
                Debug.Log(BotLogTag + " stuck player=" + playerId.ToString(CultureInfo.InvariantCulture)
                    + " state=" + agent.Brain.State + "; abandoning the goal");
                agent.Brain.AbandonGoal();
                agent.ClearPath();
            }

            BotWeapon(agent, player, playerId, input);
        }

        /// <summary>弾倉が空ならリロードを頼み、撃つ判断で連射間隔が空いていれば人と同じ <see cref="ServerFire"/> に渡す。</summary>
        private void BotWeapon(BotAgent agent, NetPlayer player, int playerId, BotInput input)
        {
            var weapon = player.Weapon;
            var spec = weapon.CurrentSpec;
            if (spec == null)
            {
                return;
            }

            var trigger = agent.TriggerFor(spec);
            trigger.Tick(NetworkPlayerMotor.TickSeconds);

            var state = weapon.State;
            if (state.IsReloading || state.IsSwitching)
            {
                return;
            }

            if (state.AmmoInMagazine <= 0)
            {
                // 持ち主はサーバー自身なので、人のリロードの要求と同じ検査を通る
                weapon.ServerHandleReloadRequest(weapon.OwnerClientId);
                return;
            }

            // Semi も連射間隔ごとに引き直す (人が速く叩くのと同じ)。間隔より速くはサーバーの検査も通さない
            if (!input.Fire || !CanFire(_session, playerId) || !trigger.Pull(true, true))
            {
                return;
            }

            var serverTime = NetworkManager.ServerTime.Time;
            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime) || serverTime < 0d)
            {
                return;
            }

            // サーバーの上で今の位置を見て撃つので、巻き戻しは無い (ホストの人と同じ遅れ 0)
            var time = FireTiming.ViewTime(serverTime, NetworkPlayerMotor.TickRate, 0);
            player.ServerNextShotNumber(out var shotIndex, out var generation);
            ServerFire(player, spec.Id, input.FireOrigin, input.FireDirection, time, shotIndex, generation);
            agent.Brain.RecordShot();
        }

        /// <summary>
        /// Bot の知覚。視線の通る、射程 (<see cref="_botSightRange"/>) 内でいちばん近い、動ける敵を目標にする。
        /// 見える敵がいないときに体力が減っていたら、いちばん近い敵の位置を音として聞かせ、そちらを探しに行かせる。
        /// </summary>
        private EnemyPerception Perceive(BotAgent agent, NetPlayer self, int selfId, Vector3 position, Vector3 eye)
        {
            var team = _session.GetTeam(selfId);
            NetPlayer seen = null;
            var seenDistance = float.PositiveInfinity;
            NetPlayer nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var pair in _playerById)
            {
                var other = pair.Value;
                if (other == null || other == self || !_session.HasPlayer(pair.Key) || _session.GetTeam(pair.Key) == team
                    || !_session.IsAlive(pair.Key) || _session.IsDowned(pair.Key))
                {
                    continue;
                }

                var distance = Vector3.Distance(position, other.transform.position);
                if (distance < nearestDistance)
                {
                    nearest = other;
                    nearestDistance = distance;
                }

                if (distance <= _botSightRange && distance < seenDistance && HasLineOfSight(eye, other))
                {
                    seen = other;
                    seenDistance = distance;
                }
            }

            var health = _session.GetHealth(selfId);
            // int.MaxValue は「前の値がまだ無い」印なので、出現直後の最初の tick は被弾に数えない。
            var wasHit = agent.LastHealth != int.MaxValue && health < agent.LastHealth;
            agent.LastHealth = health;
            var healthFraction = _session.MaxHealth > 0 ? Mathf.Clamp01((float)health / _session.MaxHealth) : 1f;

            var heard = seen == null && wasHit && nearest != null;
            return new EnemyPerception(
                NetMath.ToNumerics(position),
                seen != null ? NetMath.ToNumerics(seen.transform.position) : (System.Numerics.Vector3?)null,
                seen != null,
                heard,
                heard ? NetMath.ToNumerics(nearest.transform.position) : (System.Numerics.Vector3?)null,
                false,
                null,
                healthFraction);
        }

        // 目から相手の本体の中心までに、相手より先に当たる物 (壁・他のプレイヤー) が無ければ見えている。
        // 目は自分のカプセルの中にあるので、自分には当たらない
        private static bool HasLineOfSight(Vector3 eye, NetPlayer target)
        {
            var toTarget = target.transform.position - eye;
            var distance = toTarget.magnitude;
            if (!(distance > 0f))
            {
                return true;
            }

            if (!Physics.Raycast(eye, toTarget / distance, out var hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.collider.GetComponentInParent<NetPlayer>() == target;
        }

        /// <summary>サーバーで動かす Bot 1 人分の判断・入力変換・経路・射撃の状態。</summary>
        private sealed class BotAgent
        {
            private readonly NavMeshPath _path = new NavMeshPath();
            private readonly Vector3[] _cornerBuffer = new Vector3[_botMaxPathCorners];
            private readonly List<Vector3> _corners = new List<Vector3>();
            private float _accumulator;
            private bool _hasPath;
            private bool _pathFailed;
            private Vector3 _pathDestination;
            private float _pathTime;
            private Vector3 _progressPosition;
            private float _progressTime;
            private bool _tracking;
            private FireTrigger _trigger;
            private WeaponSpec _triggerSpec;
            private bool _dead = true;

            public BotAgent(int playerId, NetworkPlayerMotor motor, IReadOnlyList<System.Numerics.Vector3> route)
            {
                Motor = motor;
                Brain = new EnemyBrain(new EnemySpec(
                    _botSightRange, _botAttackRange, _botLoseSightSeconds, _botSearchSeconds, _botReactionSeconds));
                Brain.SetPatrolRoute(route);

                // 照準の誤差の乱数。Bot ごとに違うぶれ方にする
                Planner = new BotInputPlanner(BotInputSpec.Default, new System.Random(unchecked(playerId * 31 + Environment.TickCount)));
            }

            public EnemyBrain Brain { get; }

            public BotInputPlanner Planner { get; }

            public NetworkPlayerMotor Motor { get; }

            /// <summary>今の上下の向き (度、正が下)。motor は上下を持たないのでここで持つ。</summary>
            public float Pitch { get; set; }

            /// <summary>前の tick の体力。減ったら撃たれたとみなす。</summary>
            public int LastHealth { get; set; } = int.MaxValue;

            /// <summary>この Bot の時計 (秒)。動かした tick の分だけ進む。</summary>
            public float Clock { get; set; }

            /// <summary>最後に引けた経路の角。</summary>
            public IReadOnlyList<Vector3> Corners => _corners;

            /// <summary>deltaSeconds を溜め、このフレームで進める tick の数を返す (上限 maxTicks、越えた分は捨てる)。</summary>
            public int Advance(float deltaSeconds, float tickSeconds, int maxTicks)
            {
                _accumulator += Mathf.Max(0f, deltaSeconds);
                var ticks = Mathf.FloorToInt(_accumulator / tickSeconds);
                _accumulator -= ticks * tickSeconds;
                if (ticks > maxTicks)
                {
                    ticks = maxTicks;
                    _accumulator = 0f;
                }

                return ticks;
            }

            /// <summary>倒れた (か次の試合で出し直す) ことを覚える。次に生きている tick で判断をやり直す。</summary>
            public void MarkDead()
            {
                if (_dead)
                {
                    return;
                }

                _dead = true;
                Brain.Kill();
            }

            /// <summary>倒れていたなら、判断・照準・経路を出現した位置から始め直す。</summary>
            public void ReviveIfDead()
            {
                if (!_dead)
                {
                    return;
                }

                _dead = false;
                if (Brain.IsDead)
                {
                    Brain.Revive();
                }

                Planner.ResetAim();
                Pitch = 0f;
                LastHealth = int.MaxValue;
                ClearPath();
                _tracking = false;
            }

            /// <summary>
            /// from から destination までの経路を、引き直す時期なら NavMesh で引く。引けた (前の経路を使う場合を含む) なら true。
            /// 引けなければ <see cref="Corners"/> を空にして false (同じ先では <see cref="_botRepathSeconds"/> の間引き直さない)。
            /// </summary>
            public bool TryPath(Vector3 from, Vector3 destination)
            {
                var fresh = Clock - _pathTime < _botRepathSeconds
                    && (destination - _pathDestination).sqrMagnitude < _botRepathDistance * _botRepathDistance;
                if ((_hasPath || _pathFailed) && fresh)
                {
                    return _hasPath;
                }

                _pathTime = Clock;
                _pathDestination = destination;
                _corners.Clear();
                _hasPath = NavMesh.SamplePosition(from, out var start, _botNavMeshSampleRadius, NavMesh.AllAreas)
                    && NavMesh.SamplePosition(destination, out var end, _botNavMeshSampleRadius, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path)
                    && _path.status != NavMeshPathStatus.PathInvalid;
                _pathFailed = !_hasPath;
                if (_hasPath)
                {
                    var count = _path.GetCornersNonAlloc(_cornerBuffer);
                    for (var i = 0; i < count; i++)
                    {
                        _corners.Add(_cornerBuffer[i]);
                    }
                }

                return _hasPath;
            }

            public void ClearPath()
            {
                _hasPath = false;
                _pathFailed = false;
                _corners.Clear();
            }

            /// <summary>
            /// 進もうとしているのに <see cref="_botStuckSeconds"/> 秒で <see cref="_botStuckProgress"/> m (水平) しか進んでいなければ true。
            /// 進もうとしていない tick では測り直す。
            /// </summary>
            public bool IsStuck(Vector3 position, bool moving)
            {
                if (!moving || !_tracking)
                {
                    _tracking = moving;
                    _progressPosition = position;
                    _progressTime = Clock;
                    return false;
                }

                if (Clock - _progressTime < _botStuckSeconds)
                {
                    return false;
                }

                var moved = new Vector2(position.x - _progressPosition.x, position.z - _progressPosition.z).magnitude;
                _progressPosition = position;
                _progressTime = Clock;
                return moved < _botStuckProgress;
            }

            /// <summary>spec の射撃モードと連射間隔の引き金。持ち替えで武器が変わったら作り直す。</summary>
            public FireTrigger TriggerFor(WeaponSpec spec)
            {
                if (_trigger == null || !ReferenceEquals(spec, _triggerSpec))
                {
                    _trigger = new FireTrigger(spec);
                    _triggerSpec = spec;
                }

                return _trigger;
            }
        }
    }
}
