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
    /// <item>体力が 0 になったら <see cref="TeamDeathmatch.ReportKill"/> を呼び、リスポーン待ちが終わったら
    /// 体力を満タンに戻して地点を選び <see cref="Spawned"/> を出す。</item>
    /// <item>イベントはいつも「倒した → 点数 → リスポーン/終了」の順に出す
    /// (RespawnSeconds が 0 でも倒したことより先にリスポーンを出さない)。</item>
    /// </list>
    /// </remarks>
    public sealed class TeamDeathmatchSession
    {
        /// <summary>試合が始まったときに 1 度だけ呼ばれる。</summary>
        public event Action Started;

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
        private readonly Queue<Action> _deferred = new Queue<Action>();
        private int _busy;

        public TeamDeathmatchSpec Spec => _match.Spec;

        /// <summary>参加したときとリスポーンしたときの体力。</summary>
        public int MaxHealth { get; }

        public TeamDeathmatchState State => _match.State;
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
        public TeamDeathmatchSession(
            TeamDeathmatchSpec spec,
            int maxHealth,
            IReadOnlyList<SpawnPoint> spawnPoints,
            SpawnSelector selector,
            Func<int, Vector3> positionOf)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
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

            _match = new TeamDeathmatch(spec);
            _match.RespawnReady += playerId => Defer(() => Respawn(playerId));
            _match.Ended += winner => Defer(() => Ended?.Invoke(winner));
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
            _health.Add(playerId, new Health(MaxHealth));

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

            if (amount == 0 || State != TeamDeathmatchState.Playing || victim.IsDead)
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

        /// <summary>時間を進める。リスポーン待ちが終わった人を出現させ、時間切れなら試合を終える。</summary>
        public void Tick(float deltaSeconds)
        {
            _match.Tick(deltaSeconds);
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
            if (State != TeamDeathmatchState.Playing || self.IsDead)
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
            if (State != TeamDeathmatchState.Waiting)
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

            _health[playerId] = new Health(MaxHealth);
            PlayerChanged?.Invoke(playerId);
            Spawned?.Invoke(playerId, SelectSpawn(playerId));
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
