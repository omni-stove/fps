using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fps.Core
{
    /// <summary>
    /// 部品を組み合わせて作るゲームモードの本体。チーム分け (<see cref="ITeamLayout"/>)・得点 (<see cref="IScoreRule"/>)・
    /// リスポーン (<see cref="IRespawnPolicy"/>)・勝利条件 (<see cref="IWinCondition"/>) を受け取り、参加者と陣営ごとの得点、
    /// キル数とデス数、残り時間を持つ。時間は <see cref="Tick"/> で外から進める。
    /// 終了後は何を報告しても状態が変わらない。次の試合は <see cref="Reset"/> で同じ顔ぶれのまま始め直す。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>別の陣営を倒すと倒した人の Kills が増え、得点の部品が返した点が倒した人の陣営に入る。味方や自分を倒したときは Deaths だけ増える。</item>
    /// <item>リスポーンの部品を渡したときは、倒された人をリスポーン待ちにし、戻るまでの重ねての報告を無視する。
    /// null のときは倒されたことを数えるだけで、待ちも脱落も扱わない (リスポーンを外で扱うモード向け)。</item>
    /// <item><see cref="Tick"/> は、陣営の脱落を知らせ (<see cref="Eliminated"/>)、勝利条件の <see cref="IWinCondition.CheckStanding"/> を調べ、
    /// 時間で入る点を足し (得点上限に届く時刻があればそこで止めて終える)、リスポーン待ちを進め、時間切れなら終える、の順に進める。</item>
    /// </list>
    /// </remarks>
    public sealed class ModeSession : IGameMode
    {
        /// <summary>状態が変わるたびに新しい状態を渡して呼ばれる。Ended では <see cref="Ended"/> より先に呼ばれ、その時点で Winner は確定済み。</summary>
        public event Action<GameModeState> StateChanged;

        /// <summary>倒されたプレイヤーがリスポーンできるようになったときに呼ばれる。引数は playerId。</summary>
        public event Action<int> RespawnReady;

        /// <summary>
        /// 陣営が脱落した (1 人以上いて、全員が戻らない形で倒れている) ことを <see cref="Tick"/> の頭で知らせる。陣営ごとに 1 度だけ呼ばれ、
        /// 誰かが戻るか加わって脱落でなくなった後にまた脱落したら、もう一度呼ばれる。勝利条件を調べるより先に呼ぶ。
        /// </summary>
        public event Action<Side> Eliminated;

        /// <summary>試合が終わった瞬間に 1 度だけ呼ばれる。引数は勝った陣営で、引き分けなら null。</summary>
        public event Action<Side?> Ended;

        private readonly ITeamLayout _layout;
        private readonly IScoreRule[] _scoreRules;
        private readonly IRespawnPolicy _respawn;
        private readonly IWinCondition[] _winConditions;
        private readonly double? _scoreLimit;

        private readonly Dictionary<int, PlayerRecord> _players = new Dictionary<int, PlayerRecord>();
        private readonly List<Side> _sides = new List<Side>();
        private readonly ReadOnlyCollection<Side> _sidesView;
        private readonly Dictionary<Side, double> _scores = new Dictionary<Side, double>();
        private readonly HashSet<Side> _eliminated = new HashSet<Side>();
        private int _joinCount;

        public GameModeState State { get; private set; }

        /// <summary>試合の制限時間 (秒)。0 より大きい。<see cref="float.PositiveInfinity"/> なら時間切れにならない。</summary>
        public float TimeLimitSeconds { get; }

        /// <summary>残り時間 (秒)。開始前は TimeLimitSeconds のまま、終了後は終了した時点の値のまま。</summary>
        public float RemainingSeconds { get; private set; }

        /// <summary>試合が始まってから進んだ秒数。</summary>
        public double ElapsedSeconds { get; private set; }

        /// <summary>勝った陣営。終了前と、引き分けで終わったときは null。</summary>
        public Side? Winner { get; private set; }

        /// <summary>勝利条件のうちいちばん低い得点の上限。どの条件も持たなければ null。</summary>
        public double? ScoreLimit => _scoreLimit;

        /// <summary>倒された人をリスポーン待ちにするなら true (リスポーンの部品を渡した)。</summary>
        public bool TracksDowns => _respawn != null;

        public ITeamLayout Layout => _layout;

        public int PlayerCount => _players.Count;

        /// <summary>
        /// 得点を持つ陣営 (ID の昇順)。チーム分けの <see cref="ITeamLayout.FixedSides"/> と、これまでに誰かが入った陣営。
        /// 抜けて誰もいなくなった陣営も得点を持ったまま残る。
        /// </summary>
        public IReadOnlyList<Side> Sides => _sidesView;

        /// <summary>次に <see cref="Join"/> した人が入る陣営。どこにも入れなければ null。</summary>
        public Side? NextSide => _layout.Assign(_joinCount, CountMembers);

        /// <summary>得点がいちばん多い陣営。いちばん多い陣営が複数あるか、陣営が 1 つもなければ null。</summary>
        public Side? LeadingSide
        {
            get
            {
                Side? leader = null;
                var best = double.NegativeInfinity;
                var tied = false;
                foreach (var side in _sides)
                {
                    var score = _scores[side];
                    if (score > best)
                    {
                        best = score;
                        leader = side;
                        tied = false;
                    }
                    else if (score == best)
                    {
                        tied = true;
                    }
                }

                return tied ? null : leader;
            }
        }

        /// <summary>Wave のように、まとめて戻す次の時刻までの秒数。そういう仕組みがないときと、Playing 以外のときは null。</summary>
        public float? SecondsUntilNextWave => State == GameModeState.Playing && _respawn != null ? _respawn.SecondsUntilNextWave : null;

        /// <param name="layout">チーム分け。</param>
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい値。<see cref="float.PositiveInfinity"/> なら時間切れにならない。</param>
        /// <param name="scoreRules">得点の部品。空でもよい。null を含まない。</param>
        /// <param name="respawnPolicy">リスポーンの部品。null なら倒されたことを数えるだけで、待ちも脱落も扱わない。</param>
        /// <param name="winConditions">勝利条件。調べる順に並べる。空でもよい (時間切れは引き分けになる)。null を含まない。</param>
        public ModeSession(
            ITeamLayout layout,
            float timeLimitSeconds,
            IReadOnlyList<IScoreRule> scoreRules,
            IRespawnPolicy respawnPolicy,
            IReadOnlyList<IWinCondition> winConditions)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));

            if (!(timeLimitSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "timeLimitSeconds must be positive");
            }

            _scoreRules = CopyWithoutNulls(scoreRules, nameof(scoreRules));
            _winConditions = CopyWithoutNulls(winConditions, nameof(winConditions));
            _respawn = respawnPolicy;

            foreach (var condition in _winConditions)
            {
                var limit = condition.ScoreLimit;
                if (limit.HasValue && (!_scoreLimit.HasValue || limit.Value < _scoreLimit.Value))
                {
                    _scoreLimit = limit;
                }
            }

            TimeLimitSeconds = timeLimitSeconds;
            RemainingSeconds = timeLimitSeconds;
            State = GameModeState.Waiting;
            _sidesView = _sides.AsReadOnly();
            foreach (var side in layout.FixedSides)
            {
                RegisterSide(side);
            }
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
        /// 次の試合のために、陣営の得点・各プレイヤーのキル数とデス数・残り時間・経過時間・勝者・リスポーン待ち・脱落を初期値に戻し、
        /// 全員を生き返らせて Waiting に戻す。参加者と陣営分け、参加順の番号はそのまま残す。
        /// どの状態からでも呼べる。状態が変わったときだけ <see cref="StateChanged"/> を出す。
        /// </summary>
        public void Reset()
        {
            foreach (var side in _sides)
            {
                _scores[side] = 0d;
            }

            RemainingSeconds = TimeLimitSeconds;
            ElapsedSeconds = 0d;
            Winner = null;
            _eliminated.Clear();
            _respawn?.Reset();
            foreach (var record in _players.Values)
            {
                record.Kills = 0;
                record.Deaths = 0;
                record.IsDown = false;
            }

            if (State == GameModeState.Waiting)
            {
                return;
            }

            State = GameModeState.Waiting;
            StateChanged?.Invoke(State);
        }

        /// <summary>プレイヤーを参加させ、チーム分けに従って陣営を割り当てる。キル数とデス数は 0 から数える。</summary>
        /// <returns>割り当てた陣営。</returns>
        /// <exception cref="ArgumentException">すでに参加している。</exception>
        /// <exception cref="InvalidOperationException">どの陣営にも空きがない。</exception>
        public Side Join(int playerId)
        {
            if (_players.ContainsKey(playerId))
            {
                throw new ArgumentException($"player {playerId} has already been added", nameof(playerId));
            }

            var side = _layout.Assign(_joinCount, CountMembers)
                ?? throw new InvalidOperationException("no side has room for another player");
            AddRecord(playerId, side);
            _joinCount++;
            return side;
        }

        /// <summary>プレイヤーを side に加える。参加順の番号は進めない。キル数とデス数は 0 から数える。</summary>
        /// <exception cref="ArgumentOutOfRangeException">side がチーム分けにない。</exception>
        /// <exception cref="ArgumentException">すでに参加している。</exception>
        /// <exception cref="InvalidOperationException">side が満員。</exception>
        public void AddPlayer(int playerId, Side side)
        {
            if (!_layout.Contains(side))
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side is not part of this team layout");
            }

            if (_players.ContainsKey(playerId))
            {
                throw new ArgumentException($"player {playerId} has already been added", nameof(playerId));
            }

            var capacity = _layout.Capacity;
            if (capacity.HasValue && CountMembers(side) >= capacity.Value)
            {
                throw new InvalidOperationException($"{side} is full");
            }

            AddRecord(playerId, side);
        }

        /// <summary>
        /// プレイヤーを抜く。待っていたリスポーンは取り消す。陣営の得点はそのまま残す。
        /// 抜けたことで陣営が脱落した場合は、次の <see cref="Tick"/> で扱う。
        /// </summary>
        public void RemovePlayer(int playerId)
        {
            if (!_players.Remove(playerId))
            {
                throw UnknownPlayer(playerId);
            }

            _respawn?.Cancel(playerId);
        }

        public bool HasPlayer(int playerId) => _players.ContainsKey(playerId);

        public Side GetSide(int playerId) => Find(playerId).Side;

        public int GetKills(int playerId) => Find(playerId).Kills;

        public int GetDeaths(int playerId) => Find(playerId).Deaths;

        /// <summary>参加中のプレイヤーの id を昇順で返す。</summary>
        public IReadOnlyList<int> GetPlayerIds()
        {
            var ids = new List<int>(_players.Keys);
            ids.Sort();
            return ids;
        }

        /// <summary>side に今いる人数。</summary>
        public int CountMembers(Side side)
        {
            var count = 0;
            foreach (var record in _players.Values)
            {
                if (record.Side == side)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>倒されてまだ戻っていない間は true。リスポーンの部品がなければ常に false。</summary>
        public bool IsDown(int playerId) => Find(playerId).IsDown;

        /// <summary>倒されてリスポーンを待っている間は true。</summary>
        public bool IsAwaitingRespawn(int playerId) => Find(playerId).IsDown && _respawn.WillReturn(this, playerId);

        /// <summary>倒されていて、この試合のうちにはもう戻らない間は true。</summary>
        public bool IsEliminated(int playerId) => Find(playerId).IsDown && !_respawn.WillReturn(this, playerId);

        /// <summary>side に 1 人以上いて、全員が <see cref="IsEliminated"/> なら true。</summary>
        public bool IsSideEliminated(Side side)
        {
            if (_respawn == null)
            {
                return false;
            }

            var hasMember = false;
            foreach (var pair in _players)
            {
                if (pair.Value.Side != side)
                {
                    continue;
                }

                if (!pair.Value.IsDown || _respawn.WillReturn(this, pair.Key))
                {
                    return false;
                }

                hasMember = true;
            }

            return hasMember;
        }

        /// <summary><see cref="RespawnReady"/> が出るまでの秒数。リスポーンを待っていないときは null。制限時間より後になる場合もそのまま返す。</summary>
        public float? GetRespawnRemaining(int playerId)
        {
            return IsAwaitingRespawn(playerId) ? _respawn.GetRemaining(playerId) : null;
        }

        /// <summary>side の得点。小数を含みうる。</summary>
        /// <exception cref="ArgumentOutOfRangeException">side がチーム分けにない。</exception>
        public double GetScore(Side side)
        {
            if (!_layout.Contains(side))
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side is not part of this team layout");
            }

            return _scores.TryGetValue(side, out var score) ? score : 0d;
        }

        /// <summary>
        /// 目標を果たしたなどで side に points を足す。得点上限に届いたら side の勝ちで終える。Playing 以外では何もしない。
        /// </summary>
        /// <param name="side">点を足す陣営。</param>
        /// <param name="points">足す点。0 以上の有限値。</param>
        public void AddScore(Side side, double points)
        {
            if (!_layout.Contains(side))
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side is not part of this team layout");
            }

            if (!(points >= 0d) || double.IsInfinity(points))
            {
                throw new ArgumentOutOfRangeException(nameof(points), points, "points must not be negative and must be finite");
            }

            if (State != GameModeState.Playing || points == 0d)
            {
                return;
            }

            RegisterSide(side);
            AddScoreAndCheckLimit(side, points);
        }

        /// <summary>
        /// killerId が victimId を倒したことを報告する。別の陣営を倒したら倒した人の Kills が増え、得点の部品が返した点が倒した人の陣営に入る。
        /// 味方や自滅では倒された人の Deaths だけ増える。得点上限に届いたら倒した側の勝ちで終える。
        /// Waiting・Ended の間と、リスポーンを扱うときに倒された人がすでに倒れているとき (二重の報告) は何もしない。
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
            if (killerId != victimId && killer.Side != victim.Side)
            {
                killer.Kills++;
                var points = 0d;
                foreach (var rule in _scoreRules)
                {
                    points += CheckPoints(rule.PointsForKill(this, killerId, victimId));
                }

                if (points > 0d && AddScoreAndCheckLimit(killer.Side, points))
                {
                    return;
                }
            }

            BeginDown(victimId, victim);
        }

        /// <summary>
        /// playerId が倒した相手なしで死んだこと (落下・環境ダメージなど) を報告する。自滅と同じく Deaths だけ増やす。
        /// Waiting・Ended の間と、リスポーンを扱うときにすでに倒れているときは何もしない。
        /// </summary>
        public void ReportDeath(int playerId)
        {
            var player = Find(playerId, nameof(playerId));

            if (State != GameModeState.Playing || player.IsDown)
            {
                return;
            }

            player.Deaths++;
            BeginDown(playerId, player);
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。順に、脱落した陣営を知らせ、勝利条件の <see cref="IWinCondition.CheckStanding"/> で決着すれば時間を進めずに終える。
        /// 続けて得点の部品から 1 秒あたりの点を集めて目標を進め、点を足す。得点上限に届く陣営があれば先に届いた時刻で止め、
        /// 先に届いた陣営の勝ち (同時なら引き分け) で終える。届かなければリスポーン待ちを進め、
        /// 残り時間が 0 になったら <see cref="IWinCondition.CheckTimeUp"/> で勝敗を決めて終える。Playing 以外では何もしない。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (State != GameModeState.Playing)
            {
                return;
            }

            if (NotifyEliminated())
            {
                return;
            }

            foreach (var condition in _winConditions)
            {
                var outcome = condition.CheckStanding(this);
                if (outcome.IsDecided)
                {
                    End(outcome.Winner);
                    return;
                }
            }

            var elapsed = Math.Min(deltaSeconds, RemainingSeconds);

            // 部品の中で陣営が増えても、この Tick で点を足すのは頭にあった陣営だけにする。
            var sides = _sides.ToArray();
            double[] rates = null;
            double[] reach = null;
            var firstReach = double.PositiveInfinity;
            if (_scoreRules.Length > 0)
            {
                // 目標を取り終えた Tick の分まで点を与えると、長い Tick で取った側が持っていなかった時間の点を得てしまうので、Tick の頭の値で数える。
                rates = new double[sides.Length];
                reach = new double[sides.Length];
                for (var i = 0; i < sides.Length; i++)
                {
                    var rate = 0d;
                    foreach (var rule in _scoreRules)
                    {
                        rate += CheckPoints(rule.PointsPerSecond(this, sides[i]));
                    }

                    rates[i] = rate;
                    reach[i] = rate > 0d && _scoreLimit.HasValue
                        ? Math.Max(0d, (_scoreLimit.Value - _scores[sides[i]]) / rate)
                        : double.PositiveInfinity;
                    firstReach = Math.Min(firstReach, reach[i]);
                }

                // 目標も得点上限に届く時刻までしか進めない。
                var advance = (float)Math.Min(elapsed, firstReach);
                foreach (var rule in _scoreRules)
                {
                    rule.Advance(this, advance);

                    // 目標のイベントのハンドラで Reset されたら、ここで点を足さない。
                    if (State != GameModeState.Playing)
                    {
                        return;
                    }
                }
            }

            // 1 回の Tick が長くても、得点上限に届いた時刻で止めて、そこで勝敗を決める。
            if (!double.IsPositiveInfinity(firstReach) && firstReach <= elapsed)
            {
                Accrue(sides, rates, firstReach);
                RemainingSeconds = (float)Math.Max(0d, RemainingSeconds - firstReach);
                ElapsedSeconds += firstReach;

                Side? winner = null;
                var reachedCount = 0;
                for (var i = 0; i < sides.Length; i++)
                {
                    if (reach[i] == firstReach)
                    {
                        reachedCount++;
                        winner = sides[i];
                        _scores[sides[i]] = _scoreLimit.Value;
                    }
                }

                End(reachedCount == 1 ? winner : null);
                return;
            }

            Accrue(sides, rates, elapsed);
            RemainingSeconds -= elapsed;
            ElapsedSeconds += elapsed;

            _respawn?.Tick(this, elapsed, FireRespawn);

            if (State == GameModeState.Playing && RemainingSeconds <= 0f)
            {
                EndByTimeUp();
            }
        }

        private void AddRecord(int playerId, Side side)
        {
            RegisterSide(side);
            _players.Add(playerId, new PlayerRecord(side));
        }

        private void RegisterSide(Side side)
        {
            if (_scores.ContainsKey(side))
            {
                return;
            }

            _scores.Add(side, 0d);
            var index = _sides.BinarySearch(side);
            _sides.Insert(~index, side);
        }

        /// <summary>side に points を足し、得点上限に届いたら side の勝ちで終える。終えたら true。</summary>
        private bool AddScoreAndCheckLimit(Side side, double points)
        {
            var score = _scores[side] + points;
            _scores[side] = score;
            if (_scoreLimit.HasValue && score >= _scoreLimit.Value)
            {
                End(side);
                return true;
            }

            return false;
        }

        private void Accrue(Side[] sides, double[] rates, double seconds)
        {
            if (rates == null)
            {
                return;
            }

            for (var i = 0; i < sides.Length; i++)
            {
                if (rates[i] > 0d)
                {
                    _scores[sides[i]] += rates[i] * seconds;
                }
            }
        }

        /// <summary>新しく脱落した陣営を知らせる。ハンドラの中で Playing でなくなったら true。</summary>
        private bool NotifyEliminated()
        {
            if (_respawn == null)
            {
                return false;
            }

            List<Side> newlyEliminated = null;
            foreach (var side in _sides)
            {
                if (!IsSideEliminated(side))
                {
                    _eliminated.Remove(side);
                }
                else if (_eliminated.Add(side))
                {
                    (newlyEliminated ??= new List<Side>()).Add(side);
                }
            }

            if (newlyEliminated == null)
            {
                return false;
            }

            foreach (var side in newlyEliminated)
            {
                if (State != GameModeState.Playing)
                {
                    return true;
                }

                Eliminated?.Invoke(side);
            }

            return State != GameModeState.Playing;
        }

        private void EndByTimeUp()
        {
            foreach (var condition in _winConditions)
            {
                var outcome = condition.CheckTimeUp(this);
                if (outcome.IsDecided)
                {
                    End(outcome.Winner);
                    return;
                }
            }

            End(null);
        }

        private void End(Side? winner)
        {
            State = GameModeState.Ended;
            Winner = winner;
            foreach (var record in _players.Values)
            {
                record.IsDown = false;
            }

            _respawn?.CancelAll();
            StateChanged?.Invoke(State);
            Ended?.Invoke(winner);
        }

        private void BeginDown(int playerId, PlayerRecord record)
        {
            if (_respawn == null)
            {
                return;
            }

            record.IsDown = true;
            if (_respawn.BeginWait(this, playerId))
            {
                FireRespawn(playerId);
            }
        }

        private void FireRespawn(int playerId)
        {
            if (!_players.TryGetValue(playerId, out var record) || !record.IsDown)
            {
                return;
            }

            record.IsDown = false;
            RespawnReady?.Invoke(playerId);
        }

        private static double CheckPoints(double points)
        {
            if (!(points >= 0d) || double.IsInfinity(points))
            {
                throw new InvalidOperationException($"a score rule returned {points}; points must not be negative and must be finite");
            }

            return points;
        }

        private static T[] CopyWithoutNulls<T>(IReadOnlyList<T> items, string paramName)
            where T : class
        {
            if (items == null)
            {
                throw new ArgumentNullException(paramName);
            }

            var copy = new T[items.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = items[i] ?? throw new ArgumentException($"{paramName} must not contain null", paramName);
            }

            return copy;
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
            public Side Side { get; }
            public int Kills { get; set; }
            public int Deaths { get; set; }

            /// <summary>倒されてから、まだ戻っていない間は true。リスポーンの部品がなければ立たない。</summary>
            public bool IsDown { get; set; }

            public PlayerRecord(Side side)
            {
                Side = side;
            }
        }
    }
}
