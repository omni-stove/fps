using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shooter.Core
{
    public sealed class ModeSession : IGameMode
    {
        public event Action<GameModeState> StateChanged;

        public event Action<int> RespawnReady;

        public event Action<Side> Eliminated;

        public event Action<Side?> Ended;

        private readonly ITeamLayout _layout;
        private readonly IScoreRule[] _scoreRules;

        // null を許すのは、リスポーンを外で扱うモードが倒されたことを数えるだけで、待ちも脱落も任せないようにするため。
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

        public float TimeLimitSeconds { get; }

        public float RemainingSeconds { get; private set; }

        public double ElapsedSeconds { get; private set; }

        public Side? Winner { get; private set; }

        public double? ScoreLimit => _scoreLimit;

        public bool TracksDowns => _respawn != null;

        public ITeamLayout Layout => _layout;

        public int PlayerCount => _players.Count;

        public IReadOnlyList<Side> Sides => _sidesView;

        public Side? NextSide => _layout.Assign(_joinCount, CountMembers);

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

        public float? SecondsUntilNextWave => State == GameModeState.Playing && _respawn != null ? _respawn.SecondsUntilNextWave : null;

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

        public void Start()
        {
            if (State != GameModeState.Waiting)
            {
                throw new InvalidOperationException("match has already started");
            }

            State = GameModeState.Playing;
            StateChanged?.Invoke(State);
        }

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
                record.Assists = 0;
                record.IsDown = false;
            }

            if (State == GameModeState.Waiting)
            {
                return;
            }

            State = GameModeState.Waiting;
            StateChanged?.Invoke(State);
        }

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

        public void AddPlayer(int playerId, Side side)
        {
            AddPlayer(playerId, side, 0, 0, 0);
        }

        // 戻ってきた人の kills を引き継いでも陣営の得点を動かさないのは、そのキルの点は抜ける前にもう入っているため。
        public void AddPlayer(int playerId, Side side, int kills, int deaths)
        {
            AddPlayer(playerId, side, kills, deaths, 0);
        }

        // 戻ってきた人の kills と assists を引き継いでも陣営の得点を動かさないのは、そのキルとアシストの点は抜ける前にもう入っているため。
        public void AddPlayer(int playerId, Side side, int kills, int deaths, int assists)
        {
            if (kills < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kills), kills, "kills must not be negative");
            }

            if (deaths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deaths), deaths, "deaths must not be negative");
            }

            if (assists < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(assists), assists, "assists must not be negative");
            }

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
            var record = _players[playerId];
            record.Kills = kills;
            record.Deaths = deaths;
            record.Assists = assists;
        }

        public void RemovePlayer(int playerId)
        {
            if (!_players.Remove(playerId))
            {
                throw UnknownPlayer(playerId);
            }

            _respawn?.Cancel(playerId);
        }

        // 抜いて入れ直さず記録の陣営だけを替えるのは、RemovePlayer が倒れている人のリスポーンの待ちを取り消してしまうため。
        public void ChangeSide(int playerId, Side side)
        {
            var record = Find(playerId);
            if (!_layout.Contains(side))
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side is not part of this team layout");
            }

            if (record.Side == side)
            {
                return;
            }

            var capacity = _layout.Capacity;
            if (capacity.HasValue && CountMembers(side) >= capacity.Value)
            {
                throw new InvalidOperationException($"{side} is full");
            }

            RegisterSide(side);
            record.Side = side;
        }

        public bool HasPlayer(int playerId) => _players.ContainsKey(playerId);

        public Side GetSide(int playerId) => Find(playerId).Side;

        public int GetKills(int playerId) => Find(playerId).Kills;

        public int GetDeaths(int playerId) => Find(playerId).Deaths;

        public int GetAssists(int playerId) => Find(playerId).Assists;

        public IReadOnlyList<int> GetPlayerIds()
        {
            var ids = new List<int>(_players.Keys);
            ids.Sort();
            return ids;
        }

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

        public bool IsDown(int playerId) => Find(playerId).IsDown;

        public bool IsAwaitingRespawn(int playerId) => Find(playerId).IsDown && _respawn.WillReturn(this, playerId);

        public bool IsEliminated(int playerId) => Find(playerId).IsDown && !_respawn.WillReturn(this, playerId);

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

        public float? GetRespawnRemaining(int playerId)
        {
            return IsAwaitingRespawn(playerId) ? _respawn.GetRemaining(playerId) : null;
        }

        public double GetScore(Side side)
        {
            if (!_layout.Contains(side))
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side is not part of this team layout");
            }

            return _scores.TryGetValue(side, out var score) ? score : 0d;
        }

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

        public bool Finish(Side? winner)
        {
            if (winner.HasValue && !_layout.Contains(winner.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(winner), winner, "winner is not part of this team layout");
            }

            if (State != GameModeState.Playing)
            {
                return false;
            }

            End(winner);
            return true;
        }

        // TimeLimitSeconds を変えず残りだけを延ばすのは、Reset で始めの長さに戻すため。
        public void ExtendTime(float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative and must be finite");
            }

            if (State != GameModeState.Playing || seconds == 0f)
            {
                return;
            }

            RemainingSeconds += seconds;
        }

        public void ReportKill(int killerId, int victimId)
        {
            ReportKill(killerId, victimId, null);
        }

        public void ReportKill(int killerId, int victimId, IReadOnlyList<int> assisterIds)
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
                var killPoints = 0d;
                foreach (var rule in _scoreRules)
                {
                    killPoints += CheckPoints(rule.PointsForKill(this, killerId, victimId));
                }

                // 点を足す前にアシストの数と点をすべて決めておき、途中で得点上限に届いて終わっても数え漏れが出ないようにする。
                var gains = new List<SidePoints> { new SidePoints(killer.Side, killPoints) };
                CountAssists(killerId, victimId, victim.Side, assisterIds, gains);
                foreach (var gain in gains)
                {
                    if (gain.Points > 0d && AddScoreAndCheckLimit(gain.Side, gain.Points))
                    {
                        return;
                    }
                }
            }

            BeginDown(victimId, victim);
        }

        private void CountAssists(int killerId, int victimId, Side victimSide, IReadOnlyList<int> assisterIds, List<SidePoints> gains)
        {
            if (assisterIds == null || assisterIds.Count == 0)
            {
                return;
            }

            var counted = new HashSet<int>();
            foreach (var assisterId in assisterIds)
            {
                if (assisterId == killerId
                    || assisterId == victimId
                    || !_players.TryGetValue(assisterId, out var assister)
                    || assister.Side == victimSide
                    || !counted.Add(assisterId))
                {
                    continue;
                }

                assister.Assists++;
                var points = 0d;
                foreach (var rule in _scoreRules)
                {
                    points += CheckPoints(rule.PointsForAssist(this, assisterId, victimId));
                }

                var index = gains.FindIndex(g => g.Side == assister.Side);
                if (index < 0)
                {
                    gains.Add(new SidePoints(assister.Side, points));
                }
                else
                {
                    gains[index] = new SidePoints(assister.Side, gains[index].Points + points);
                }
            }
        }

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
            public Side Side { get; set; }
            public int Kills { get; set; }
            public int Deaths { get; set; }
            public int Assists { get; set; }

            public bool IsDown { get; set; }

            public PlayerRecord(Side side)
            {
                Side = side;
            }
        }

        private readonly struct SidePoints
        {
            public SidePoints(Side side, double points)
            {
                Side = side;
                Points = points;
            }

            public Side Side { get; }
            public double Points { get; }
        }
    }
}
