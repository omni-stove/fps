using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// サーバーで動かすチームデスマッチの進行役。<see cref="TeamDeathmatch"/> (点数・時間・リスポーン待ち) に、
    /// 参加順のチーム割り当て (<see cref="TeamAssigner"/>)、プレイヤーごとの <see cref="Health"/>、
    /// <see cref="SpawnSelector"/> による出現地点の選択をまとめる。ネットワークの層はここを呼び、
    /// イベントを受けて同期するだけにする。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>両チームに 1 人以上そろった時点で試合を始める。</item>
    /// <item>味方へのダメージは無視する (自滅は数える)。</item>
    /// <item>プレイヤーごとに <see cref="HealthRegenerator"/> を持ち、<see cref="TeamDeathmatchSpec.Rules"/> に従って
    /// 試合中だけ体力を自動回復させる。</item>
    /// <item>体力が 0 になったら <see cref="TeamDeathmatch.ReportKill"/> を呼び、リスポーン待ちが終わったら
    /// 体力を満タンに戻して地点を選び <see cref="Spawned"/> を出す。</item>
    /// <item>イベントはいつも「倒した → 点数 → リスポーン/終了」の順に出す
    /// (RespawnSeconds が 0 でも倒したことより先にリスポーンを出さない)。</item>
    /// <item>再開までの秒数 (<see cref="RestartDelaySeconds"/>) を渡したときは、終わってからその秒数だけ
    /// <see cref="Tick"/> が進んだら <see cref="Restart"/> して同じ顔ぶれ・同じチーム分けで次の試合を始める。</item>
    /// </list>
    /// </remarks>
    public sealed class TeamDeathmatchSession
    {
        /// <summary>試合が始まるたびに呼ばれる (<see cref="Restart"/> で始め直したときも)。</summary>
        public event Action Started;

        /// <summary>
        /// <see cref="Restart"/> で点数・キル数・デス数・残り時間・体力を初期値に戻した直後に呼ばれる。
        /// 続けて <see cref="ScoreChanged"/> (0, 0)、各プレイヤーの <see cref="PlayerChanged"/> と <see cref="Spawned"/>、
        /// 両チームがそろっていれば <see cref="Started"/> の順に出る。
        /// </summary>
        public event Action Restarted;

        /// <summary>チームの点数が変わったときに呼ばれる。引数は (Alpha の点数, Bravo の点数)。</summary>
        public event Action<int, int> ScoreChanged;

        /// <summary>プレイヤーのチーム・キル数・デス数・体力のどれかが変わったときに呼ばれる。引数は playerId。</summary>
        public event Action<int> PlayerChanged;

        /// <summary>プレイヤーが倒されたときに呼ばれる。引数は (killerId, victimId)。</summary>
        public event Action<int, int> Killed;

        /// <summary>参加したときとリスポーンするときに、出現する地点を知らせる。引数は (playerId, 地点)。</summary>
        public event Action<int, SpawnPoint> Spawned;

        /// <summary>試合が終わったときに 1 度だけ呼ばれる。引数は勝ったチームで、同点なら null。</summary>
        public event Action<Team?> Ended;

        private readonly TeamDeathmatch _match;
        private readonly TeamAssigner _assigner = new TeamAssigner();
        private readonly SpawnSelector _selector;
        private readonly SpawnPoint[] _spawnPoints;
        private readonly Func<int, Vector3> _positionOf;

        // playerId の昇順に回して、同じ入力なら同じ結果になるようにする。
        private readonly SortedDictionary<int, Health> _health = new SortedDictionary<int, Health>();

        // _health と同じ顔ぶれを持つ。Health を作り直すたびに作り直す。
        private readonly Dictionary<int, HealthRegenerator> _regenerators = new Dictionary<int, HealthRegenerator>();
        private readonly Queue<Action> _deferred = new Queue<Action>();
        private int _busy;

        // 終わってから次の試合までの待ち。待っていないときは _restartPending が false
        private readonly float? _restartDelaySeconds;
        private float _restartRemaining;
        private bool _restartPending;

        public TeamDeathmatchSpec Spec => _match.Spec;

        /// <summary>終わってから自動で次の試合を始めるまでの秒数。null なら自動では始めない。</summary>
        public float? RestartDelaySeconds => _restartDelaySeconds;

        /// <summary>終わった後、次の試合を待っている間は true。</summary>
        public bool IsRestartPending => _restartPending;

        /// <summary>次の試合が始まるまでの残り秒数。待っていないときは 0。</summary>
        public float RestartRemainingSeconds => _restartPending ? Math.Max(0f, _restartRemaining) : 0f;

        /// <summary>参加したときとリスポーンしたときの体力。</summary>
        public int MaxHealth { get; }

        public GameModeState State => _match.State;
        public float RemainingSeconds => _match.RemainingSeconds;
        public Team? Winner => _match.Winner;
        public int PlayerCount => _health.Count;

        /// <summary>次に参加する人のチーム。</summary>
        public Team NextTeam => _assigner.Peek;

        /// <param name="spec">試合の設定。</param>
        /// <param name="maxHealth">プレイヤーの最大体力。1 以上。</param>
        /// <param name="spawnPoints">出現地点。両チームとも 1 か所以上使えること。順番は同点時の決め手になる。</param>
        /// <param name="selector">出現地点の選び方。</param>
        /// <param name="positionOf">playerId から現在位置を引く。参加中のプレイヤーについてだけ呼ぶ。</param>
        /// <param name="restartDelaySeconds">
        /// 終わってから自動で次の試合を始めるまでの秒数。0 以上の有限値。null なら自動では始めない。
        /// </param>
        public TeamDeathmatchSession(
            TeamDeathmatchSpec spec,
            int maxHealth,
            IReadOnlyList<SpawnPoint> spawnPoints,
            SpawnSelector selector,
            Func<int, Vector3> positionOf,
            float? restartDelaySeconds = null)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (restartDelaySeconds.HasValue
                && (!(restartDelaySeconds.Value >= 0f) || float.IsInfinity(restartDelaySeconds.Value)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(restartDelaySeconds), restartDelaySeconds, "restartDelaySeconds must not be negative and must be finite");
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be positive");
            }

            if (spawnPoints == null)
            {
                throw new ArgumentNullException(nameof(spawnPoints));
            }

            _spawnPoints = new SpawnPoint[spawnPoints.Count];
            for (var i = 0; i < spawnPoints.Count; i++)
            {
                _spawnPoints[i] = spawnPoints[i] ?? throw new ArgumentException("spawnPoints must not contain null", nameof(spawnPoints));
            }

            RequireSpawnPointFor(_spawnPoints, Team.Alpha, nameof(spawnPoints));
            RequireSpawnPointFor(_spawnPoints, Team.Bravo, nameof(spawnPoints));

            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _positionOf = positionOf ?? throw new ArgumentNullException(nameof(positionOf));
            MaxHealth = maxHealth;
            _restartDelaySeconds = restartDelaySeconds;

            _match = new TeamDeathmatch(spec);
            _match.RespawnReady += playerId => Defer(() => Respawn(playerId));
            _match.Ended += winner =>
            {
                // 終わった瞬間から数える (Ended の受け手が残り秒数を読めるよう、知らせるより先に)
                if (_restartDelaySeconds.HasValue)
                {
                    _restartPending = true;
                    _restartRemaining = _restartDelaySeconds.Value;
                }

                Defer(() => Ended?.Invoke(winner));
            };
        }

        public bool HasPlayer(int playerId) => _health.ContainsKey(playerId);

        public Team GetTeam(int playerId) => _match.GetTeam(playerId);

        public int GetKills(int playerId) => _match.GetKills(playerId);

        public int GetDeaths(int playerId) => _match.GetDeaths(playerId);

        public int GetScore(Team team) => _match.GetScore(team);

        public int GetHealth(int playerId) => FindHealth(playerId).Current;

        public bool IsAlive(int playerId) => !FindHealth(playerId).IsDead;

        /// <summary>参加中のプレイヤーの id を昇順で返す。</summary>
        public IReadOnlyList<int> GetPlayerIds()
        {
            return new List<int>(_health.Keys);
        }

        /// <summary>
        /// プレイヤーを参加させ、参加順に交互にチームを割り当てる。<see cref="PlayerChanged"/> と
        /// <see cref="Spawned"/> を出し、両チームがそろったら試合を始める。
        /// </summary>
        /// <returns>割り当てたチーム。</returns>
        /// <exception cref="ArgumentException">すでに参加している。</exception>
        public Team Join(int playerId)
        {
            if (_health.ContainsKey(playerId))
            {
                throw new ArgumentException($"player {playerId} has already joined", nameof(playerId));
            }

            var team = _assigner.Assign();
            _match.AddPlayer(playerId, team);
            ResetHealth(playerId);

            PlayerChanged?.Invoke(playerId);
            Spawned?.Invoke(playerId, SelectSpawn(playerId));
            TryStart();
            return team;
        }

        /// <summary>プレイヤーを抜く。チームの点数はそのまま残し、リスポーン待ちは取り消す。</summary>
        public void Leave(int playerId)
        {
            FindHealth(playerId);
            _match.RemovePlayer(playerId);
            _health.Remove(playerId);
            _regenerators.Remove(playerId);
        }

        /// <summary>
        /// attackerId が victimId に amount のダメージを与える。体力が 0 になったら倒したことにして
        /// <see cref="TeamDeathmatch.ReportKill"/> を呼ぶ。ネットワーク側のダメージ処理はすべてここを通す。
        /// </summary>
        /// <param name="attackerId">撃った人。victimId と同じなら自滅。</param>
        /// <param name="victimId">撃たれた人。</param>
        /// <param name="amount">ダメージ。0 以上。</param>
        public DamageOutcome ApplyDamage(int attackerId, int victimId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            FindHealth(attackerId, nameof(attackerId));
            var victim = FindHealth(victimId, nameof(victimId));

            if (amount == 0 || State != GameModeState.Playing || victim.IsDead)
            {
                return DamageOutcome.Ignored;
            }

            if (attackerId != victimId && GetTeam(attackerId) == GetTeam(victimId))
            {
                return DamageOutcome.Ignored;
            }

            _busy++;
            try
            {
                victim.TakeDamage(amount);
                _regenerators[victimId].NotifyDamaged();
                if (!victim.IsDead)
                {
                    PlayerChanged?.Invoke(victimId);
                    return DamageOutcome.Damaged;
                }

                var alphaBefore = _match.GetScore(Team.Alpha);
                var bravoBefore = _match.GetScore(Team.Bravo);
                _match.ReportKill(attackerId, victimId);

                Killed?.Invoke(attackerId, victimId);
                PlayerChanged?.Invoke(victimId);
                if (attackerId != victimId)
                {
                    PlayerChanged?.Invoke(attackerId);
                }

                var alpha = _match.GetScore(Team.Alpha);
                var bravo = _match.GetScore(Team.Bravo);
                if (alpha != alphaBefore || bravo != bravoBefore)
                {
                    ScoreChanged?.Invoke(alpha, bravo);
                }

                return DamageOutcome.Killed;
            }
            finally
            {
                _busy--;
                FlushDeferred();
            }
        }

        /// <summary>
        /// 時間を進める。試合中なら先に各プレイヤーの体力を自動回復させ (<see cref="GameRules.RegenPerSecond"/> が
        /// 0 なら何もしない)、体力が変わった人ごとに <see cref="PlayerChanged"/> を出す。続けて
        /// リスポーン待ちが終わった人を出現させ、時間切れなら試合を終える。
        /// 終わった後は次の試合までの待ちを進め、待ち終わったら <see cref="Restart"/> する
        /// (試合が終わった回の Tick の残りは待ちに数えない)。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            var waitingForRestart = _restartPending && State == GameModeState.Ended;

            // _match.Tick より先に回すのは、この Tick で時間切れになっても終わるまでの分は回復させるため
            if (State == GameModeState.Playing)
            {
                TickRegeneration(deltaSeconds);
            }

            // 終わった後は時間を進めないので何も起きない
            _match.Tick(deltaSeconds);

            if (!waitingForRestart)
            {
                return;
            }

            _restartRemaining -= deltaSeconds;
            if (_restartRemaining <= 0f)
            {
                Restart();
            }
        }

        /// <summary>
        /// 終わった試合を同じ顔ぶれ・同じチーム分けで始め直す。チームの点数、各プレイヤーのキル数とデス数、
        /// 残り時間を初期値に戻し、全員を体力満タンにして自分のチームの地点へ出現させる (<see cref="Spawned"/>)。
        /// 両チームに 1 人以上いればそのまま始まり、いなければ Waiting に戻って参加を待つ。
        /// </summary>
        /// <exception cref="InvalidOperationException">試合がまだ終わっていない。</exception>
        public void Restart()
        {
            if (State != GameModeState.Ended)
            {
                throw new InvalidOperationException("the match can only be restarted after it has ended, but is " + State);
            }

            _restartPending = false;
            _restartRemaining = 0f;
            _match.Reset();

            var playerIds = new List<int>(_health.Keys);
            foreach (var playerId in playerIds)
            {
                ResetHealth(playerId);
            }

            Restarted?.Invoke();
            ScoreChanged?.Invoke(0, 0);

            // 受け手の中で抜けた人は飛ばす
            foreach (var playerId in playerIds)
            {
                if (_health.ContainsKey(playerId))
                {
                    PlayerChanged?.Invoke(playerId);
                }
            }

            foreach (var playerId in playerIds)
            {
                if (_health.ContainsKey(playerId))
                {
                    Spawned?.Invoke(playerId, SelectSpawn(playerId));
                }
            }

            TryStart();
        }

        /// <summary>
        /// playerId が出現する地点を選ぶ。生きている他のプレイヤーを敵・味方に分けて <see cref="SpawnSelector"/> に渡す。
        /// </summary>
        public SpawnPoint SelectSpawn(int playerId)
        {
            var team = GetTeam(playerId);
            var enemies = new List<Vector3>();
            var allies = new List<Vector3>();
            foreach (var pair in _health)
            {
                if (pair.Key == playerId || pair.Value.IsDead)
                {
                    continue;
                }

                var position = _positionOf(pair.Key);
                if (GetTeam(pair.Key) == team)
                {
                    allies.Add(position);
                }
                else
                {
                    enemies.Add(position);
                }
            }

            return _selector.Select(_spawnPoints, enemies, allies, team);
        }

        /// <summary>
        /// playerId から最も近い、生きている敵の id。同じ距離なら id の小さいほう。
        /// 生きている敵がいないとき、試合中でないとき、playerId 自身が倒れているときは null。
        /// </summary>
        public int? FindNearestEnemy(int playerId)
        {
            var self = FindHealth(playerId);
            if (State != GameModeState.Playing || self.IsDead)
            {
                return null;
            }

            var team = GetTeam(playerId);
            var from = _positionOf(playerId);
            int? nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var pair in _health)
            {
                if (pair.Value.IsDead || GetTeam(pair.Key) == team)
                {
                    continue;
                }

                var distance = Vector3.DistanceSquared(from, _positionOf(pair.Key));
                if (nearest == null || distance < nearestDistance)
                {
                    nearest = pair.Key;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void TryStart()
        {
            if (State != GameModeState.Waiting)
            {
                return;
            }

            var hasAlpha = false;
            var hasBravo = false;
            foreach (var playerId in _health.Keys)
            {
                if (GetTeam(playerId) == Team.Alpha)
                {
                    hasAlpha = true;
                }
                else
                {
                    hasBravo = true;
                }
            }

            if (hasAlpha && hasBravo)
            {
                _match.Start();
                Started?.Invoke();
            }
        }

        private void Respawn(int playerId)
        {
            // 待っている間に抜けていたら何もしない。
            if (!_health.ContainsKey(playerId))
            {
                return;
            }

            ResetHealth(playerId);
            PlayerChanged?.Invoke(playerId);
            Spawned?.Invoke(playerId, SelectSpawn(playerId));
        }

        private void ResetHealth(int playerId)
        {
            var health = new Health(MaxHealth);
            _health[playerId] = health;
            _regenerators[playerId] = new HealthRegenerator(health, Spec.Rules);
        }

        private void TickRegeneration(float deltaSeconds)
        {
            if (Spec.Rules.RegenPerSecond == 0f)
            {
                return;
            }

            // 受け手が抜けたりダメージを与えたりしても回している最中の辞書を崩さないよう、知らせるのは回し終えてから
            List<int> changed = null;
            foreach (var playerId in _health.Keys)
            {
                if (_regenerators[playerId].Tick(deltaSeconds) > 0)
                {
                    (changed ??= new List<int>()).Add(playerId);
                }
            }

            if (changed == null)
            {
                return;
            }

            foreach (var playerId in changed)
            {
                if (_health.ContainsKey(playerId))
                {
                    PlayerChanged?.Invoke(playerId);
                }
            }
        }

        /// <summary>ApplyDamage の途中ならそれが終わるまで遅らせ、そうでなければすぐに実行する。</summary>
        private void Defer(Action action)
        {
            if (_busy > 0)
            {
                _deferred.Enqueue(action);
                return;
            }

            action();
        }

        private void FlushDeferred()
        {
            while (_busy == 0 && _deferred.Count > 0)
            {
                _deferred.Dequeue()();
            }
        }

        private static void RequireSpawnPointFor(SpawnPoint[] spawnPoints, Team team, string paramName)
        {
            foreach (var point in spawnPoints)
            {
                if (point.IsUsableBy(team))
                {
                    return;
                }
            }

            throw new ArgumentException($"no spawn point is usable by team {team}", paramName);
        }

        private Health FindHealth(int playerId, string paramName = "playerId")
        {
            if (!_health.TryGetValue(playerId, out var health))
            {
                throw new ArgumentOutOfRangeException(paramName, playerId, "no player with this id");
            }

            return health;
        }
    }
}
