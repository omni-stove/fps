using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// チームデスマッチのルール。Alpha と Bravo の 2 チームでキル数を競い、どちらかが ScoreLimit に届くか
    /// 制限時間が切れたら終わる。時間は <see cref="Tick"/> で外から進める。
    /// 終了後は何を報告しても状態が変わらない。次の試合は <see cref="Reset"/> で同じ顔ぶれのまま始め直す。
    /// </summary>
    public sealed class TeamDeathmatch : IGameMode
    {
        /// <summary>状態が変わるたびに新しい状態を渡して呼ばれる。Ended では <see cref="Ended"/> より先に呼ばれ、その時点で Winner は確定済み。</summary>
        public event Action<GameModeState> StateChanged;

        /// <summary>倒されたプレイヤーが RespawnSeconds 経ってリスポーンできるようになったときに呼ばれる。引数は playerId。</summary>
        public event Action<int> RespawnReady;

        /// <summary>試合が終わった瞬間に 1 度だけ呼ばれる。引数は勝ったチームで、同点なら null。</summary>
        public event Action<Team?> Ended;

        private readonly Dictionary<int, PlayerRecord> _players = new Dictionary<int, PlayerRecord>();
        private int _alphaScore;
        private int _bravoScore;

        public TeamDeathmatchSpec Spec { get; }
        public GameModeState State { get; private set; }

        /// <summary>残り時間 (秒)。開始前は TimeLimitSeconds のまま、終了後は終了した時点の値のまま。</summary>
        public float RemainingSeconds { get; private set; }

        /// <summary>勝ったチーム。終了前と、同点で終わったときは null。</summary>
        public Team? Winner { get; private set; }

        public int PlayerCount => _players.Count;

        /// <param name="spec">試合の設定。</param>
        public TeamDeathmatch(TeamDeathmatchSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            State = GameModeState.Waiting;
            RemainingSeconds = spec.TimeLimitSeconds;
        }

        /// <summary>試合を始める。Waiting のときだけ呼べる。</summary>
        public void Start()
        {
            if (State != GameModeState.Waiting)
            {
                throw new InvalidOperationException("match has already started");
            }

            State = GameModeState.Playing;
            StateChanged?.Invoke(State);
        }

        /// <summary>
        /// 次の試合のために、チームの点数・各プレイヤーのキル数とデス数・残り時間・勝者を初期値に戻し、
        /// リスポーン待ちを取り消して Waiting に戻す。参加者とチーム分けはそのまま残す。
        /// どの状態からでも呼べる。状態が変わったときだけ <see cref="StateChanged"/> を出す。
        /// </summary>
        public void Reset()
        {
            _alphaScore = 0;
            _bravoScore = 0;
            RemainingSeconds = Spec.TimeLimitSeconds;
            Winner = null;
            foreach (var record in _players.Values)
            {
                record.Kills = 0;
                record.Deaths = 0;
                record.RespawnRemaining = null;
            }

            if (State == GameModeState.Waiting)
            {
                return;
            }

            State = GameModeState.Waiting;
            StateChanged?.Invoke(State);
        }

        /// <summary>プレイヤーを team に加える。キル数とデス数は 0 から数える。</summary>
        public void AddPlayer(int playerId, Team team)
        {
            if (team != Team.Alpha && team != Team.Bravo)
            {
                throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }

            if (_players.ContainsKey(playerId))
            {
                throw new ArgumentException($"player {playerId} has already been added", nameof(playerId));
            }

            _players.Add(playerId, new PlayerRecord(team));
        }

        /// <summary>プレイヤーを抜く。待っていたリスポーンは取り消す。チームの点数はそのまま残す。</summary>
        public void RemovePlayer(int playerId)
        {
            if (!_players.Remove(playerId))
            {
                throw UnknownPlayer(playerId);
            }
        }

        public bool HasPlayer(int playerId) => _players.ContainsKey(playerId);

        public Team GetTeam(int playerId) => Find(playerId).Team;

        public int GetKills(int playerId) => Find(playerId).Kills;

        public int GetDeaths(int playerId) => Find(playerId).Deaths;

        /// <summary>倒されてリスポーンを待っている間は true。</summary>
        public bool IsAwaitingRespawn(int playerId) => Find(playerId).RespawnRemaining.HasValue;

        public int GetScore(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return _alphaScore;
                case Team.Bravo:
                    return _bravoScore;
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }

        /// <summary>
        /// killerId が victimId を倒したことを報告する。別チームを倒したら倒した側のチームに 1 点入り、
        /// 倒した人の Kills が増える。同じチームや自滅では加点せず、倒された人の Deaths だけ増える。
        /// Waiting・Ended の間と、倒された人がすでにリスポーン待ちのとき (二重の報告) は何もしない。
        /// </summary>
        public void ReportKill(int killerId, int victimId)
        {
            var killer = Find(killerId, nameof(killerId));
            var victim = Find(victimId, nameof(victimId));

            if (State != GameModeState.Playing || victim.RespawnRemaining.HasValue)
            {
                return;
            }

            victim.Deaths++;
            if (killerId != victimId && killer.Team != victim.Team)
            {
                killer.Kills++;
                if (AddScore(killer.Team) >= Spec.ScoreLimit)
                {
                    End(killer.Team);
                    return;
                }
            }

            BeginRespawn(victimId, victim);
        }

        /// <summary>
        /// playerId が倒した相手なしで死んだこと (落下・環境ダメージなど) を報告する。自滅と同じく Deaths だけ増やし、
        /// 点数は動かさずにリスポーン待ちにする。Waiting・Ended の間と、すでにリスポーン待ちのときは何もしない。
        /// </summary>
        public void ReportDeath(int playerId)
        {
            var player = Find(playerId, nameof(playerId));

            if (State != GameModeState.Playing || player.RespawnRemaining.HasValue)
            {
                return;
            }

            player.Deaths++;
            BeginRespawn(playerId, player);
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。リスポーン待ちが終わったプレイヤーについて RespawnReady を出し、
        /// 残り時間が 0 になったら点数の多いほうを勝ちとして終える。Playing 以外では何もしない。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            if (State != GameModeState.Playing)
            {
                return;
            }

            var elapsed = Math.Min(deltaSeconds, RemainingSeconds);
            RemainingSeconds -= elapsed;

            List<KeyValuePair<int, PlayerRecord>> ready = null;
            foreach (var pair in _players)
            {
                var record = pair.Value;
                if (!record.RespawnRemaining.HasValue)
                {
                    continue;
                }

                record.RespawnRemaining = record.RespawnRemaining.Value - elapsed;
                if (record.RespawnRemaining.Value <= 0f)
                {
                    ready ??= new List<KeyValuePair<int, PlayerRecord>>();
                    ready.Add(pair);
                }
            }

            if (ready != null)
            {
                // 早く待ち終わった順 (同時なら playerId 順) に出し、呼び出し順を決まったものにする。
                ready.Sort((a, b) =>
                {
                    var byTime = a.Value.RespawnRemaining.Value.CompareTo(b.Value.RespawnRemaining.Value);
                    return byTime != 0 ? byTime : a.Key.CompareTo(b.Key);
                });

                foreach (var pair in ready)
                {
                    // ハンドラの中で抜けたり、再び倒されたり、試合が終わったりしたら出さない。
                    if (State != GameModeState.Playing)
                    {
                        return;
                    }

                    if (_players.TryGetValue(pair.Key, out var current)
                        && ReferenceEquals(current, pair.Value)
                        && current.RespawnRemaining.HasValue
                        && current.RespawnRemaining.Value <= 0f)
                    {
                        FireRespawn(pair.Key, current);
                    }
                }
            }

            if (State == GameModeState.Playing && RemainingSeconds <= 0f)
            {
                End(LeadingTeam());
            }
        }

        private int AddScore(Team team)
        {
            return team == Team.Alpha ? ++_alphaScore : ++_bravoScore;
        }

        private Team? LeadingTeam()
        {
            if (_alphaScore > _bravoScore)
            {
                return Team.Alpha;
            }

            if (_bravoScore > _alphaScore)
            {
                return Team.Bravo;
            }

            return null;
        }

        private void End(Team? winner)
        {
            State = GameModeState.Ended;
            Winner = winner;
            foreach (var record in _players.Values)
            {
                record.RespawnRemaining = null;
            }

            StateChanged?.Invoke(State);
            Ended?.Invoke(winner);
        }

        private void BeginRespawn(int playerId, PlayerRecord record)
        {
            record.RespawnRemaining = Spec.RespawnSeconds;
            if (Spec.RespawnSeconds == 0f)
            {
                FireRespawn(playerId, record);
            }
        }

        private void FireRespawn(int playerId, PlayerRecord record)
        {
            record.RespawnRemaining = null;
            RespawnReady?.Invoke(playerId);
        }

        private PlayerRecord Find(int playerId, string paramName = "playerId")
        {
            if (!_players.TryGetValue(playerId, out var record))
            {
                throw UnknownPlayer(playerId, paramName);
            }

            return record;
        }

        private static ArgumentOutOfRangeException UnknownPlayer(int playerId, string paramName = "playerId")
        {
            return new ArgumentOutOfRangeException(paramName, playerId, "no player with this id");
        }

        private sealed class PlayerRecord
        {
            public Team Team { get; }
            public int Kills { get; set; }
            public int Deaths { get; set; }

            /// <summary>リスポーンまでの残り秒数。待っていないときは null。</summary>
            public float? RespawnRemaining { get; set; }

            public PlayerRecord(Team team)
            {
                Team = team;
            }
        }
    }
}
