using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 拠点制圧のルール。Alpha と Bravo の 2 チームが <see cref="DominationSpec.PointCount"/> 個の拠点 (<see cref="ControlPoint"/>) を取り合い、
    /// 持っている拠点 1 つにつき 1 秒あたり PointsPerSecond のポイントを得る。どちらかが ScoreLimit に届くか制限時間が切れたら終わる。
    /// 時間は <see cref="Tick"/> で外から進め、誰がどの拠点の半径にいるかは <see cref="SetPlayerPoint"/> で外から伝える。
    /// キルとデスは数えるだけで、ポイントにはしない。リスポーンはこのクラスでは扱わない。
    /// 終了後は何を報告しても状態が変わらない。次の試合は <see cref="Reset"/> で同じ顔ぶれのまま始め直す。
    /// </summary>
    public sealed class Domination : IGameMode
    {
        /// <summary>状態が変わるたびに新しい状態を渡して呼ばれる。Ended では <see cref="Ended"/> より先に呼ばれ、その時点で Winner は確定済み。</summary>
        public event Action<GameModeState> StateChanged;

        /// <summary>試合が終わった瞬間に 1 度だけ呼ばれる。引数は勝ったチームで、同点なら null。</summary>
        public event Action<Team?> Ended;

        /// <summary>拠点が占領された瞬間に呼ばれる。引数は拠点の番号と占領したチーム。</summary>
        public event Action<int, Team> PointCaptured;

        /// <summary>持ち主のいた拠点が中立に戻った瞬間に呼ばれる。引数は拠点の番号とそれまでの持ち主。</summary>
        public event Action<int, Team> PointNeutralized;

        private readonly Dictionary<int, PlayerRecord> _players = new Dictionary<int, PlayerRecord>();
        private readonly ControlPoint[] _points;

        // 1 秒あたりのポイントは小数なので、丸めをためないよう double で足していく。
        private double _alphaScore;
        private double _bravoScore;

        public DominationSpec Spec { get; }
        public GameModeState State { get; private set; }

        /// <summary>残り時間 (秒)。開始前は TimeLimitSeconds のまま、終了後は終了した時点の値のまま。</summary>
        public float RemainingSeconds { get; private set; }

        /// <summary>勝ったチーム。終了前と、同点で終わったときは null。</summary>
        public Team? Winner { get; private set; }

        public int PlayerCount => _players.Count;

        public int PointCount => _points.Length;

        /// <param name="spec">試合の設定。</param>
        public Domination(DominationSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            State = GameModeState.Waiting;
            RemainingSeconds = spec.TimeLimitSeconds;

            _points = new ControlPoint[spec.PointCount];
            for (var i = 0; i < _points.Length; i++)
            {
                var index = i;
                var point = new ControlPoint(spec.CaptureSeconds, spec.MaxCapturers);
                point.Captured += team => PointCaptured?.Invoke(index, team);
                point.Neutralized += team => PointNeutralized?.Invoke(index, team);
                _points[i] = point;
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
        /// 次の試合のために、ポイント・各プレイヤーのキル数とデス数・残り時間・勝者・全拠点の占領を初期値に戻し、
        /// 誰もどの拠点にもいないことにして Waiting に戻す。参加者とチーム分けはそのまま残す。
        /// どの状態からでも呼べる。状態が変わったときだけ <see cref="StateChanged"/> を出す。拠点のイベントは出さない。
        /// </summary>
        public void Reset()
        {
            _alphaScore = 0d;
            _bravoScore = 0d;
            RemainingSeconds = Spec.TimeLimitSeconds;
            Winner = null;
            foreach (var point in _points)
            {
                point.Reset();
            }

            foreach (var record in _players.Values)
            {
                record.Kills = 0;
                record.Deaths = 0;
                record.PointIndex = null;
            }

            if (State == GameModeState.Waiting)
            {
                return;
            }

            State = GameModeState.Waiting;
            StateChanged?.Invoke(State);
        }

        /// <summary>プレイヤーを team に加える。どの拠点にもいない状態で、キル数とデス数は 0 から数える。</summary>
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

        /// <summary>プレイヤーを抜く。次の <see cref="Tick"/> から拠点の人数に数えない。チームのポイントはそのまま残す。</summary>
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

        /// <summary>プレイヤーがいる拠点の番号。どの拠点の半径にもいなければ null。</summary>
        public int? GetPlayerPoint(int playerId) => Find(playerId).PointIndex;

        /// <summary>
        /// プレイヤーがどの拠点の半径にいるかを伝える。どこにもいなければ null。次の <see cref="Tick"/> から占領に数える。
        /// どの状態でも呼べる。倒れているプレイヤーは数えないよう、生き返るまで null にしておく。
        /// </summary>
        public void SetPlayerPoint(int playerId, int? pointIndex)
        {
            var record = Find(playerId);
            if (pointIndex.HasValue && (pointIndex.Value < 0 || pointIndex.Value >= _points.Length))
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "no control point with this index");
            }

            record.PointIndex = pointIndex;
        }

        /// <summary>拠点を占領しているチーム。中立なら null。</summary>
        public Team? GetOwner(int pointIndex) => Point(pointIndex).Owner;

        /// <summary>拠点の占領の進み。-1 (Bravo) から +1 (Alpha) で、0 が中立。</summary>
        public float GetControl(int pointIndex) => Point(pointIndex).Control;

        /// <summary>最後の <see cref="Tick"/> で両チームが拠点にいて、進みが止まっていたら true。</summary>
        public bool IsContested(int pointIndex) => Point(pointIndex).IsContested;

        /// <summary>team が持っている拠点の数。</summary>
        public int CountOwned(Team team)
        {
            ValidateTeam(team);
            var owned = 0;
            foreach (var point in _points)
            {
                if (point.Owner == team)
                {
                    owned++;
                }
            }

            return owned;
        }

        /// <summary>team のポイント。小数を含む。表示では切り捨てて使う。</summary>
        public float GetScore(Team team)
        {
            ValidateTeam(team);
            return (float)(team == Team.Alpha ? _alphaScore : _bravoScore);
        }

        /// <summary>
        /// killerId が victimId を倒したことを数える。倒された人の Deaths を増やし、別チームを倒したなら倒した人の Kills を増やす。
        /// ポイントは動かさない。倒された人はどの拠点にもいないことにする。Waiting・Ended の間は何もしない。
        /// </summary>
        public void ReportKill(int killerId, int victimId)
        {
            var killer = Find(killerId, nameof(killerId));
            var victim = Find(victimId, nameof(victimId));

            if (State != GameModeState.Playing)
            {
                return;
            }

            victim.Deaths++;
            victim.PointIndex = null;
            if (killerId != victimId && killer.Team != victim.Team)
            {
                killer.Kills++;
            }
        }

        /// <summary>
        /// playerId が倒した相手なしで死んだことを数える。Deaths を増やし、どの拠点にもいないことにする。
        /// Waiting・Ended の間は何もしない。
        /// </summary>
        public void ReportDeath(int playerId)
        {
            var player = Find(playerId, nameof(playerId));

            if (State != GameModeState.Playing)
            {
                return;
            }

            player.Deaths++;
            player.PointIndex = null;
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。この Tick を始めた時点の持ち主に、持っている拠点の数 × PointsPerSecond × 秒数のポイントを足し、
        /// 各拠点の占領を進める。この Tick で占領した拠点のポイントは次の Tick から入る。
        /// この Tick の途中で ScoreLimit に届くチームがあれば、先に届いたほうの勝ちで終える (同時なら引き分け)。
        /// 届かずに残り時間が 0 になったら、ポイントの多いほうを勝ちとして終える。Playing 以外では何もしない。
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

            // 占領し終えた Tick の分まで点を与えると、長い Tick で占領した側が持っていなかった時間の点を得てしまうので、Tick の頭の持ち主で数える。
            var alphaRate = CountOwned(Team.Alpha) * (double)Spec.PointsPerSecond;
            var bravoRate = CountOwned(Team.Bravo) * (double)Spec.PointsPerSecond;

            var alphaReach = SecondsToLimit(_alphaScore, alphaRate);
            var bravoReach = SecondsToLimit(_bravoScore, bravoRate);
            var firstReach = Math.Min(alphaReach, bravoReach);

            // 拠点も勝利ポイントに届く時刻までしか進めない。
            UpdatePoints((float)Math.Min(elapsed, firstReach));

            // 拠点のイベントのハンドラで Reset されたら、ここで点を足さない。
            if (State != GameModeState.Playing)
            {
                return;
            }

            // 1 回の Tick が長くても、勝利ポイントに届いた時刻で止めて、そこで勝敗を決める。
            if (firstReach <= elapsed)
            {
                Advance(firstReach, alphaRate, bravoRate);
                Team? winner = null;
                if (alphaReach < bravoReach)
                {
                    winner = Team.Alpha;
                }
                else if (bravoReach < alphaReach)
                {
                    winner = Team.Bravo;
                }

                if (alphaReach == firstReach)
                {
                    _alphaScore = Spec.ScoreLimit;
                }

                if (bravoReach == firstReach)
                {
                    _bravoScore = Spec.ScoreLimit;
                }

                End(winner);
                return;
            }

            Advance(elapsed, alphaRate, bravoRate);
            if (RemainingSeconds <= 0f)
            {
                End(LeadingTeam());
            }
        }

        private void UpdatePoints(float elapsed)
        {
            var alphaCounts = new int[_points.Length];
            var bravoCounts = new int[_points.Length];
            foreach (var record in _players.Values)
            {
                if (!record.PointIndex.HasValue)
                {
                    continue;
                }

                if (record.Team == Team.Alpha)
                {
                    alphaCounts[record.PointIndex.Value]++;
                }
                else
                {
                    bravoCounts[record.PointIndex.Value]++;
                }
            }

            for (var i = 0; i < _points.Length && State == GameModeState.Playing; i++)
            {
                _points[i].Update(alphaCounts[i], bravoCounts[i], elapsed);
            }
        }

        private double SecondsToLimit(double score, double rate)
        {
            return rate > 0d ? Math.Max(0d, (Spec.ScoreLimit - score) / rate) : double.PositiveInfinity;
        }

        private void Advance(double seconds, double alphaRate, double bravoRate)
        {
            _alphaScore += alphaRate * seconds;
            _bravoScore += bravoRate * seconds;
            RemainingSeconds = (float)Math.Max(0d, RemainingSeconds - seconds);
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
            StateChanged?.Invoke(State);
            Ended?.Invoke(winner);
        }

        private ControlPoint Point(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= _points.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "no control point with this index");
            }

            return _points[pointIndex];
        }

        private static void ValidateTeam(Team team)
        {
            if (team != Team.Alpha && team != Team.Bravo)
            {
                throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
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

            /// <summary>半径の中にいる拠点の番号。どこにもいなければ null。</summary>
            public int? PointIndex { get; set; }

            public PlayerRecord(Team team)
            {
                Team = team;
            }
        }
    }
}
