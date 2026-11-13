using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// チームデスマッチのルール。Alpha と Bravo の 2 チームでキル数を競い、どちらかが ScoreLimit に届くか
    /// 制限時間が切れたら終わる。時間は <see cref="Tick"/> で外から進める。
    /// 終了後は何を報告しても状態が変わらない。次の試合は <see cref="Reset"/> で同じ顔ぶれのまま始め直す。
    /// </summary>
    /// <remarks>
    /// 倒された人がいつ戻るかは <see cref="GameRules.RespawnMode"/> で決まる。
    /// <list type="bullet">
    /// <item><see cref="RespawnMode.Individual"/>: 倒されてから RespawnSeconds 後に <see cref="RespawnReady"/>。</item>
    /// <item><see cref="RespawnMode.Wave"/>: 試合開始から WaveIntervalSeconds ごとに波が来る。波の時刻までに
    /// 倒されてから RespawnSeconds 以上たった人を、その波でまとめて <see cref="RespawnReady"/> にする。
    /// 波の時刻ちょうどに倒された人 (その波を出した <see cref="Tick"/> の後に報告された人) は次の波を待つ。</item>
    /// <item><see cref="RespawnMode.None"/>: 試合中は戻らない。<see cref="Tick"/> のたびに、1 人以上いて全員が倒れている
    /// チームがあれば相手チームの勝ちで終える。両チームともそうなら引き分け。</item>
    /// </list>
    /// </remarks>
    public sealed class TeamDeathmatch : IGameMode
    {
        /// <summary>状態が変わるたびに新しい状態を渡して呼ばれる。Ended では <see cref="Ended"/> より先に呼ばれ、その時点で Winner は確定済み。</summary>
        public event Action<GameModeState> StateChanged;

        /// <summary>倒されたプレイヤーがリスポーンできるようになったときに呼ばれる。引数は playerId。None では呼ばれない。</summary>
        public event Action<int> RespawnReady;

        /// <summary>試合が終わった瞬間に 1 度だけ呼ばれる。引数は勝ったチームで、同点なら null。</summary>
        public event Action<Team?> Ended;

        private readonly Dictionary<int, PlayerRecord> _players = new Dictionary<int, PlayerRecord>();
        private int _alphaScore;
        private int _bravoScore;

        // 試合が始まってから進んだ秒数と、過ぎた波の数。波の時刻は間隔を足し続けずに (番号 × 間隔) で出し、誤差をためない。
        private double _elapsedSeconds;
        private long _wavesPassed;

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
        /// 次の試合のために、チームの点数・各プレイヤーのキル数とデス数・残り時間・勝者・波の時刻を初期値に戻し、
        /// リスポーン待ちを取り消して全員を生き返らせ、Waiting に戻す。参加者とチーム分けはそのまま残す。
        /// どの状態からでも呼べる。状態が変わったときだけ <see cref="StateChanged"/> を出す。
        /// </summary>
        public void Reset()
        {
            _alphaScore = 0;
            _bravoScore = 0;
            RemainingSeconds = Spec.TimeLimitSeconds;
            Winner = null;
            _elapsedSeconds = 0d;
            _wavesPassed = 0;
            foreach (var record in _players.Values)
            {
                record.Kills = 0;
                record.Deaths = 0;
                record.Revive();
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

        /// <summary>
        /// プレイヤーを抜く。待っていたリスポーンは取り消す。チームの点数はそのまま残す。
        /// None で抜けたことによりチームが全滅した場合は、次の <see cref="Tick"/> で終える。
        /// </summary>
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

        /// <summary>倒されてリスポーンを待っている間は true。None では戻らないので常に false。</summary>
        public bool IsAwaitingRespawn(int playerId) => Find(playerId).IsDown && Spec.Rules.RespawnMode != RespawnMode.None;

        /// <summary>None で倒され、この試合ではもう戻らない間は true。None 以外では常に false。</summary>
        public bool IsEliminated(int playerId) => Find(playerId).IsDown && Spec.Rules.RespawnMode == RespawnMode.None;

        /// <summary>
        /// <see cref="RespawnReady"/> が出るまでの秒数。Individual では倒されてからの待ちの残り、
        /// Wave では戻れる最初の波の時刻までの秒数。リスポーンを待っていないとき (None を含む) は null。
        /// 制限時間より後になる場合もそのまま返す。
        /// </summary>
        public float? GetRespawnRemaining(int playerId)
        {
            var record = Find(playerId);
            if (!IsAwaitingRespawn(playerId))
            {
                return null;
            }

            var wait = Math.Max(0f, record.RespawnRemaining);
            if (Spec.Rules.RespawnMode == RespawnMode.Individual)
            {
                return wait;
            }

            var waveAt = WaveTime(FirstWaveAtOrAfter(_elapsedSeconds + wait));
            return (float)Math.Max(0d, waveAt - _elapsedSeconds);
        }

        /// <summary>Wave で次の波が来るまでの秒数。Wave 以外のときと、Playing 以外のときは null。</summary>
        public float? SecondsUntilNextWave
        {
            get
            {
                if (Spec.Rules.RespawnMode != RespawnMode.Wave || State != GameModeState.Playing)
                {
                    return null;
                }

                return (float)Math.Max(0d, WaveTime(_wavesPassed + 1) - _elapsedSeconds);
            }
        }

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
        /// Waiting・Ended の間と、倒された人がすでに倒れているとき (二重の報告) は何もしない。
        /// </summary>
        public void ReportKill(int killerId, int victimId)
        {
            var killer = Find(killerId, nameof(killerId));
            var victim = Find(victimId, nameof(victimId));

            if (State != GameModeState.Playing || victim.IsDown)
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
        /// 点数は動かさずにリスポーン待ちにする。Waiting・Ended の間と、すでに倒れているときは何もしない。
        /// </summary>
        public void ReportDeath(int playerId)
        {
            var player = Find(playerId, nameof(playerId));

            if (State != GameModeState.Playing || player.IsDown)
            {
                return;
            }

            player.Deaths++;
            BeginRespawn(playerId, player);
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。None では先に全滅したチームがないか調べ、あれば時間を進めずに終える。
        /// 続けてリスポーン待ちが終わったプレイヤーについて RespawnReady を出し、
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

            // 倒れた瞬間ではなくここで調べるのは、前の Tick から今までの死亡と退出を同時のものとして扱い、
            // 最後の 2 人の相打ちを報告の順番によらず引き分けにするため。
            if (Spec.Rules.RespawnMode == RespawnMode.None && EndIfWipedOut())
            {
                return;
            }

            var elapsed = Math.Min(deltaSeconds, RemainingSeconds);
            RemainingSeconds -= elapsed;
            _elapsedSeconds += elapsed;

            switch (Spec.Rules.RespawnMode)
            {
                case RespawnMode.Individual:
                    TickIndividual(elapsed);
                    break;
                case RespawnMode.Wave:
                    TickWave(elapsed);
                    break;
            }

            if (State == GameModeState.Playing && RemainingSeconds <= 0f)
            {
                End(LeadingTeam());
            }
        }

        private void TickIndividual(float elapsed)
        {
            List<KeyValuePair<int, PlayerRecord>> ready = null;
            foreach (var pair in _players)
            {
                var record = pair.Value;
                if (!record.IsDown)
                {
                    continue;
                }

                record.RespawnRemaining -= elapsed;
                if (record.RespawnRemaining <= 0f)
                {
                    ready ??= new List<KeyValuePair<int, PlayerRecord>>();
                    ready.Add(pair);
                }
            }

            if (ready == null)
            {
                return;
            }

            // 早く待ち終わった順 (同時なら playerId 順) に出し、呼び出し順を決まったものにする。
            SortByWaitedLongest(ready);

            foreach (var pair in ready)
            {
                // ハンドラの中で抜けたり、再び倒されたり、試合が終わったりしたら出さない。
                if (State != GameModeState.Playing)
                {
                    return;
                }

                if (_players.TryGetValue(pair.Key, out var current)
                    && ReferenceEquals(current, pair.Value)
                    && current.IsDown
                    && current.RespawnRemaining <= 0f)
                {
                    FireRespawn(pair.Key, current);
                }
            }
        }

        private void TickWave(float elapsed)
        {
            // 波を出す前に全員の待ちを減らしておき、波のハンドラの中で倒された人は今の時刻から数え始めるようにする。
            foreach (var record in _players.Values)
            {
                if (record.IsDown)
                {
                    record.RespawnRemaining -= elapsed;
                }
            }

            // 1 回の Tick で波を複数またいだときは、古い波から順に出す。
            while (State == GameModeState.Playing)
            {
                var waveAt = WaveTime(_wavesPassed + 1);
                if (waveAt > _elapsedSeconds)
                {
                    return;
                }

                _wavesPassed++;
                var sinceWave = (float)(_elapsedSeconds - waveAt);

                List<KeyValuePair<int, PlayerRecord>> ready = null;
                foreach (var pair in _players)
                {
                    if (IsReadyAtWave(pair.Value, sinceWave))
                    {
                        ready ??= new List<KeyValuePair<int, PlayerRecord>>();
                        ready.Add(pair);
                    }
                }

                if (ready == null)
                {
                    continue;
                }

                SortByWaitedLongest(ready);

                foreach (var pair in ready)
                {
                    if (State != GameModeState.Playing)
                    {
                        return;
                    }

                    if (_players.TryGetValue(pair.Key, out var current)
                        && ReferenceEquals(current, pair.Value)
                        && IsReadyAtWave(current, sinceWave))
                    {
                        FireRespawn(pair.Key, current);
                    }
                }
            }
        }

        private static void SortByWaitedLongest(List<KeyValuePair<int, PlayerRecord>> ready)
        {
            ready.Sort((a, b) =>
            {
                var byTime = a.Value.RespawnRemaining.CompareTo(b.Value.RespawnRemaining);
                return byTime != 0 ? byTime : a.Key.CompareTo(b.Key);
            });
        }

        /// <summary>sinceWave 秒前に来た波の時点で、record の待ちが終わっていたか。</summary>
        private static bool IsReadyAtWave(PlayerRecord record, float sinceWave)
        {
            return record.IsDown && record.RespawnRemaining + sinceWave <= 0f;
        }

        /// <summary>1 から数えて index 番目の波が来る、試合開始からの秒数。</summary>
        private double WaveTime(long index)
        {
            return index * (double)Spec.Rules.WaveIntervalSeconds;
        }

        /// <summary>まだ来ていない波のうち、readyAt 秒かそれより後に来る最初の波の番号。</summary>
        private long FirstWaveAtOrAfter(double readyAt)
        {
            var index = Math.Max(_wavesPassed + 1, (long)Math.Ceiling(readyAt / Spec.Rules.WaveIntervalSeconds));

            // 割り算の丸めで 1 つずれることがあるので、前後を確かめて合わせる。
            while (WaveTime(index) < readyAt)
            {
                index++;
            }

            while (index - 1 > _wavesPassed && WaveTime(index - 1) >= readyAt)
            {
                index--;
            }

            return index;
        }

        /// <summary>1 人以上いて全員が倒れているチームがあれば終える。終えたら true。</summary>
        private bool EndIfWipedOut()
        {
            var alphaOut = IsWipedOut(Team.Alpha);
            var bravoOut = IsWipedOut(Team.Bravo);
            if (!alphaOut && !bravoOut)
            {
                return false;
            }

            if (alphaOut && bravoOut)
            {
                End(null);
            }
            else
            {
                End(alphaOut ? Team.Bravo : Team.Alpha);
            }

            return true;
        }

        private bool IsWipedOut(Team team)
        {
            var hasMember = false;
            foreach (var record in _players.Values)
            {
                if (record.Team != team)
                {
                    continue;
                }

                if (!record.IsDown)
                {
                    return false;
                }

                hasMember = true;
            }

            return hasMember;
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
                record.Revive();
            }

            StateChanged?.Invoke(State);
            Ended?.Invoke(winner);
        }

        private void BeginRespawn(int playerId, PlayerRecord record)
        {
            record.IsDown = true;
            record.RespawnRemaining = Spec.RespawnSeconds;

            // Wave は 0 秒でも次の波を待ち、None は戻らない。
            if (Spec.Rules.RespawnMode == RespawnMode.Individual && Spec.RespawnSeconds == 0f)
            {
                FireRespawn(playerId, record);
            }
        }

        private void FireRespawn(int playerId, PlayerRecord record)
        {
            record.Revive();
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

            /// <summary>倒されてから、まだ戻っていない間は true。</summary>
            public bool IsDown { get; set; }

            /// <summary>倒されてから RespawnSeconds たつまでの残り秒数。過ぎると負になる。IsDown のときだけ意味を持つ。</summary>
            public float RespawnRemaining { get; set; }

            public PlayerRecord(Team team)
            {
                Team = team;
            }

            public void Revive()
            {
                IsDown = false;
                RespawnRemaining = 0f;
            }
        }
    }
}
