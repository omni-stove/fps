using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ハードポイントのルール。Alpha と Bravo の 2 チームが <see cref="HardpointSpec.PointCount"/> 個の拠点のうち、
    /// 今有効な 1 つ (<see cref="ActivePoint"/>) を取り合う。有効な拠点は <see cref="HardpointSpec.RotateSeconds"/> 秒ごとに
    /// 番号の順 (最後の次は 0) に次の拠点へ移る。有効な拠点に生きている自チームだけがいる間、1 秒あたり PointsPerSecond のポイントが入り、
    /// 両チームがいれば争奪中でどちらにも入らない。どちらかが ScoreLimit に届くか制限時間が切れたら終わる。
    /// 誰がどの拠点にいるかは <see cref="SetPlayerPoint"/> で外から伝え、時間は <see cref="Session"/> の Tick で進める。
    /// </summary>
    /// <remarks>
    /// 中身は <see cref="ModeSession"/> に <see cref="TwoTeams"/>・拠点のポイント (このクラスの <see cref="IScoreRule"/>)・
    /// <see cref="IndividualRespawn"/>・<see cref="ScoreLimitWin"/>・<see cref="TimeLimitWin"/> を組んだもの。
    /// 参加者は <see cref="Session"/> に直接加える (シーンの Runner が ModeSession に参加者を加える形に合わせるため)。
    /// ポイントは <see cref="ModeSession"/> の決まりどおり Tick の頭の確保で数えるので、Tick の途中で拠点が移ったときも
    /// その Tick の残りは移る前の確保で数える。
    /// </remarks>
    public sealed class Hardpoint
    {
        // 小刻みな Tick を足し合わせると移る時刻にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        /// <summary>
        /// 有効な拠点が移った瞬間に呼ばれる。引数は移る前と後の拠点の番号。呼ばれた時点で <see cref="ActivePoint"/> と
        /// <see cref="SecondsUntilMove"/> は更新済み。1 回の Tick で複数回移ったら、移った回数だけ順に呼ぶ。
        /// </summary>
        public event Action<int, int> PointMoved;

        private readonly Dictionary<int, int?> _playerPoints = new Dictionary<int, int?>();
        private double _secondsUntilMove;

        public HardpointSpec Spec { get; }

        /// <summary>進めているモード。参加者の追加・キルの報告・Tick はここに対して行う。</summary>
        public ModeSession Session { get; }

        /// <summary>今有効な拠点の番号。試合の始めは 0。</summary>
        public int ActivePoint { get; private set; }

        /// <summary>有効な拠点が次へ移るまでの秒数。開始前は RotateSeconds のまま。</summary>
        public float SecondsUntilMove => (float)_secondsUntilMove;

        /// <summary>次に有効になる拠点の番号。</summary>
        public int NextPoint => (ActivePoint + 1) % Spec.PointCount;

        public int PointCount => Spec.PointCount;

        /// <param name="spec">試合の設定。</param>
        public Hardpoint(HardpointSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _secondsUntilMove = spec.RotateSeconds;
            Session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new ActivePointScore(this) },
                new IndividualRespawn(spec.RespawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(spec.ScoreLimit), new TimeLimitWin() });
        }

        /// <summary>
        /// 次の試合のために、有効な拠点を 0 に、移るまでの秒数を RotateSeconds に戻し、誰もどの拠点にもいないことにしてから
        /// <see cref="ModeSession.Reset"/> する。<see cref="PointMoved"/> は出さない。
        /// </summary>
        public void Reset()
        {
            ActivePoint = 0;
            _secondsUntilMove = Spec.RotateSeconds;
            _playerPoints.Clear();
            Session.Reset();
        }

        /// <summary>
        /// 参加者がどの拠点の範囲にいるかを伝える。どこにもいなければ null。次の Tick から数える。
        /// 倒れている (<see cref="ModeSession.IsDown"/>) 間は、ここで伝えた値にかかわらず数えない。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">playerId が参加していないか、pointIndex が拠点の番号でない。</exception>
        public void SetPlayerPoint(int playerId, int? pointIndex)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            if (pointIndex.HasValue && (pointIndex.Value < 0 || pointIndex.Value >= Spec.PointCount))
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "no control point with this index");
            }

            _playerPoints[playerId] = pointIndex;
        }

        /// <summary>参加者が最後に伝えられた拠点。伝えられていないか、どこにもいなければ null。</summary>
        public int? GetPlayerPoint(int playerId)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            return _playerPoints.TryGetValue(playerId, out var point) ? point : null;
        }

        /// <summary>有効な拠点にいる、生きている side の参加者の数。</summary>
        public int CountOnActivePoint(Side side)
        {
            var count = 0;
            foreach (var pair in _playerPoints)
            {
                if (pair.Value == ActivePoint
                    && Session.HasPlayer(pair.Key)
                    && !Session.IsDown(pair.Key)
                    && Session.GetSide(pair.Key) == side)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>有効な拠点の様子。</summary>
        public HardpointStatus Status
        {
            get
            {
                var alpha = CountOnActivePoint(Side.Alpha) > 0;
                var bravo = CountOnActivePoint(Side.Bravo) > 0;
                if (alpha && bravo)
                {
                    return HardpointStatus.Contested;
                }

                return alpha || bravo ? HardpointStatus.Held : HardpointStatus.Empty;
            }
        }

        /// <summary>有効な拠点を自チームだけで確保しているチーム。誰もいないか争奪中なら null。</summary>
        public Side? Holder
        {
            get
            {
                var alpha = CountOnActivePoint(Side.Alpha) > 0;
                var bravo = CountOnActivePoint(Side.Bravo) > 0;
                if (alpha == bravo)
                {
                    return null;
                }

                return alpha ? Side.Alpha : Side.Bravo;
            }
        }

        private void Advance(float seconds)
        {
            if (!(seconds > 0f))
            {
                return;
            }

            _secondsUntilMove -= seconds;
            while (_secondsUntilMove <= _snap)
            {
                var previous = ActivePoint;
                ActivePoint = NextPoint;
                _secondsUntilMove += Spec.RotateSeconds;
                PointMoved?.Invoke(previous, ActivePoint);

                // ハンドラの中で Reset されたら、戻した値の上でさらに移さない。
                if (Session.State != GameModeState.Playing)
                {
                    return;
                }
            }
        }

        /// <summary>有効な拠点を自チームだけで確保している間、1 秒あたり PointsPerSecond を入れ、有効な拠点を移す。キルでは点を入れない。</summary>
        private sealed class ActivePointScore : IScoreRule
        {
            private readonly Hardpoint _owner;

            public ActivePointScore(Hardpoint owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side)
            {
                return _owner.Holder == side ? _owner.Spec.PointsPerSecond : 0d;
            }

            public void Advance(ModeSession session, float seconds) => _owner.Advance(seconds);
        }
    }
}
