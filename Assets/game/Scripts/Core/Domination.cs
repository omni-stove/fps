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
    /// <remarks>
    /// 中身は <see cref="ModeSession"/> に <see cref="TwoTeams"/>・拠点のポイント (このクラスの <see cref="IScoreRule"/>)・
    /// <see cref="ScoreLimitWin"/>・<see cref="TimeLimitWin"/> を組んだもの。リスポーンの部品は渡さない。
    /// </remarks>
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

        private readonly ModeSession _session;
        private readonly ControlPoint[] _points;

        // 参加者ごとの、半径の中にいる拠点の番号。どこにもいなければ null。顔ぶれは _session と同じに保つ。
        private readonly Dictionary<int, int?> _playerPoints = new Dictionary<int, int?>();

        public DominationSpec Spec { get; }
        public GameModeState State => _session.State;

        /// <summary>残り時間 (秒)。開始前は TimeLimitSeconds のまま、終了後は終了した時点の値のまま。</summary>
        public float RemainingSeconds => _session.RemainingSeconds;

        /// <summary>勝ったチーム。終了前と、同点で終わったときは null。</summary>
        public Team? Winner => Side.ToTeam(_session.Winner);

        public int PlayerCount => _session.PlayerCount;

        public int PointCount => _points.Length;

        /// <param name="spec">試合の設定。</param>
        public Domination(DominationSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            _points = new ControlPoint[spec.PointCount];
            for (var i = 0; i < _points.Length; i++)
            {
                var index = i;
                var point = new ControlPoint(spec.CaptureSeconds, spec.MaxCapturers);
                point.Captured += team => PointCaptured?.Invoke(index, team);
                point.Neutralized += team => PointNeutralized?.Invoke(index, team);
                _points[i] = point;
            }

            _session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new ControlPointScore(this) },
                null,
                new IWinCondition[] { new ScoreLimitWin(spec.ScoreLimit), new TimeLimitWin() });
            _session.StateChanged += state => StateChanged?.Invoke(state);
            _session.Ended += winner => Ended?.Invoke(Side.ToTeam(winner));
        }

        /// <summary>試合を始める。Waiting のときだけ呼べる。</summary>
        public void Start() => _session.Start();

        /// <summary>
        /// 次の試合のために、ポイント・各プレイヤーのキル数とデス数・残り時間・勝者・全拠点の占領を初期値に戻し、
        /// 誰もどの拠点にもいないことにして Waiting に戻す。参加者とチーム分けはそのまま残す。
        /// どの状態からでも呼べる。状態が変わったときだけ <see cref="StateChanged"/> を出す。拠点のイベントは出さない。
        /// </summary>
        public void Reset()
        {
            foreach (var point in _points)
            {
                point.Reset();
            }

            foreach (var playerId in new List<int>(_playerPoints.Keys))
            {
                _playerPoints[playerId] = null;
            }

            _session.Reset();
        }

        /// <summary>プレイヤーを team に加える。どの拠点にもいない状態で、キル数とデス数は 0 から数える。</summary>
        public void AddPlayer(int playerId, Team team)
        {
            _session.AddPlayer(playerId, Side.FromTeam(team));
            _playerPoints[playerId] = null;
        }

        /// <summary>プレイヤーを抜く。次の <see cref="Tick"/> から拠点の人数に数えない。チームのポイントはそのまま残す。</summary>
        public void RemovePlayer(int playerId)
        {
            _session.RemovePlayer(playerId);
            _playerPoints.Remove(playerId);
        }

        public bool HasPlayer(int playerId) => _session.HasPlayer(playerId);

        public Team GetTeam(int playerId) => _session.GetSide(playerId).ToTeam();

        public int GetKills(int playerId) => _session.GetKills(playerId);

        public int GetDeaths(int playerId) => _session.GetDeaths(playerId);

        /// <summary>プレイヤーがいる拠点の番号。どの拠点の半径にもいなければ null。</summary>
        public int? GetPlayerPoint(int playerId) => FindPoint(playerId);

        /// <summary>
        /// プレイヤーがどの拠点の半径にいるかを伝える。どこにもいなければ null。次の <see cref="Tick"/> から占領に数える。
        /// どの状態でも呼べる。倒れているプレイヤーは数えないよう、生き返るまで null にしておく。
        /// </summary>
        public void SetPlayerPoint(int playerId, int? pointIndex)
        {
            FindPoint(playerId);
            if (pointIndex.HasValue && (pointIndex.Value < 0 || pointIndex.Value >= _points.Length))
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "no control point with this index");
            }

            _playerPoints[playerId] = pointIndex;
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
            Side.FromTeam(team);
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
        public float GetScore(Team team) => (float)_session.GetScore(Side.FromTeam(team));

        /// <summary>
        /// killerId が victimId を倒したことを数える。倒された人の Deaths を増やし、別チームを倒したなら倒した人の Kills を増やす。
        /// ポイントは動かさない。倒された人はどの拠点にもいないことにする。Waiting・Ended の間は何もしない。
        /// </summary>
        public void ReportKill(int killerId, int victimId)
        {
            var playing = State == GameModeState.Playing;
            _session.ReportKill(killerId, victimId);
            if (playing)
            {
                LeavePoint(victimId);
            }
        }

        /// <summary>
        /// playerId が倒した相手なしで死んだことを数える。Deaths を増やし、どの拠点にもいないことにする。
        /// Waiting・Ended の間は何もしない。
        /// </summary>
        public void ReportDeath(int playerId)
        {
            var playing = State == GameModeState.Playing;
            _session.ReportDeath(playerId);
            if (playing)
            {
                LeavePoint(playerId);
            }
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。この Tick を始めた時点の持ち主に、持っている拠点の数 × PointsPerSecond × 秒数のポイントを足し、
        /// 各拠点の占領を進める。この Tick で占領した拠点のポイントは次の Tick から入る。
        /// この Tick の途中で ScoreLimit に届くチームがあれば、先に届いたほうの勝ちで終える (同時なら引き分け)。
        /// 届かずに残り時間が 0 になったら、ポイントの多いほうを勝ちとして終える。Playing 以外では何もしない。
        /// </summary>
        public void Tick(float deltaSeconds) => _session.Tick(deltaSeconds);

        private void LeavePoint(int playerId)
        {
            if (_playerPoints.ContainsKey(playerId))
            {
                _playerPoints[playerId] = null;
            }
        }

        private void UpdatePoints(float elapsed)
        {
            var alphaCounts = new int[_points.Length];
            var bravoCounts = new int[_points.Length];
            foreach (var pair in _playerPoints)
            {
                if (!pair.Value.HasValue)
                {
                    continue;
                }

                if (_session.GetSide(pair.Key) == Side.Alpha)
                {
                    alphaCounts[pair.Value.Value]++;
                }
                else
                {
                    bravoCounts[pair.Value.Value]++;
                }
            }

            for (var i = 0; i < _points.Length && State == GameModeState.Playing; i++)
            {
                _points[i].Update(alphaCounts[i], bravoCounts[i], elapsed);
            }
        }

        private ControlPoint Point(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= _points.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "no control point with this index");
            }

            return _points[pointIndex];
        }

        private int? FindPoint(int playerId)
        {
            if (!_playerPoints.TryGetValue(playerId, out var pointIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            return pointIndex;
        }

        /// <summary>持っている拠点 1 つにつき 1 秒あたり PointsPerSecond を入れ、拠点の占領を進める。キルでは点を入れない。</summary>
        private sealed class ControlPointScore : IScoreRule
        {
            private readonly Domination _owner;

            public ControlPointScore(Domination owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side)
            {
                return _owner.CountOwned(side.ToTeam()) * (double)_owner.Spec.PointsPerSecond;
            }

            public void Advance(ModeSession session, float seconds) => _owner.UpdatePoints(seconds);
        }
    }
}
