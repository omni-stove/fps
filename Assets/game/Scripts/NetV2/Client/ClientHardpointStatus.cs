using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // Detail に専用の型を作らず Core の Hardpoint を置くのは、オフラインの HardpointRunner と同じ型にして、UI が Hardpoint に登録した書式役をそのまま使えるようにするため。
    // サーバーは拠点の数・有効な拠点・移るまでの秒・押さえている陣営しか送らないので、手元の Hardpoint をその値まで進めて写す
    public sealed class ClientHardpointStatus
    {
        // 移る間隔はサーバーから届かない。写しの間隔を送られうる最大の秒数にすると、どの残り秒も「最初から間隔 - 残り秒だけ進めた」位置で表せる。
        // 値はどれも整数で 2^24 未満なので、float に直しても進める秒がずれない
        private const int _mirrorRotateSeconds = HudFormat.MaxDisplayedSeconds;

        // 写しの陣営ごとに 1 人ずつ置き、拠点に立たせるかどうかで押さえている陣営と取り合いを作る
        private const int _alphaProbe = 0;
        private const int _bravoProbe = 1;

        private readonly int _localPlayerId;
        private Hardpoint _mirror;
        private Shown _shown;

        public ClientHardpointStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // Hardpoint の試合のスナップショットが届いていなければ null
        public ModeStatus Status { get; private set; }

        public Hardpoint Hardpoint => Status != null ? _mirror : null;

        // Status を作り直したら true。届いた値が前と同じなら、UI が持っている Status を差し替えない
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryRead(snapshot, out var shown))
            {
                return Clear();
            }

            if (Status != null && shown.Equals(_shown))
            {
                return false;
            }

            if (Status == null || !shown.SamePoint(_shown))
            {
                Mirror(shown);
            }

            _shown = shown;

            // ModeStatus は出した後も UI が持つので、得点の並びは使い回さずに毎回作る
            var sideScores = new[]
            {
                new SideScore(Side.Alpha, shown.AlphaPoints),
                new SideScore(Side.Bravo, shown.BravoPoints),
            };
            Status = new ModeStatus(
                shown.RemainingSeconds,
                sideScores,
                Side.FromTeam(shown.OwnTeam),
                null,
                shown.IsEnded,
                Side.FromTeam(shown.Winner),
                _mirror);
            return true;
        }

        // 試合を離れたときや、Hardpoint でない試合に移ったときに呼ぶ。消したら true
        public bool Clear()
        {
            if (Status == null)
            {
                return false;
            }

            Status = null;
            _shown = default;
            return true;
        }

        private bool TryRead(SnapshotMessage snapshot, out Shown shown)
        {
            shown = default;
            var match = snapshot.Match;
            var points = snapshot.ControlPoints;
            if (match.Objective != MatchObjective.Hardpoint
                || !match.HasActivePoint
                || points.Count < HardpointSpec.MinPointCount
                || match.ActivePointIndex >= points.Count)
            {
                return false;
            }

            var found = false;
            var alphaOn = false;
            var bravoOn = false;
            for (var i = 0; i < points.Count; i++)
            {
                ref var point = ref points[i];
                if (point.Index != match.ActivePointIndex)
                {
                    continue;
                }

                found = true;
                alphaOn = point.Contested || (point.HasOwner && point.Owner == Team.Alpha);
                bravoOn = point.Contested || (point.HasOwner && point.Owner == Team.Bravo);
                break;
            }

            if (!found)
            {
                return false;
            }

            Team? ownTeam = null;
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId == _localPlayerId)
                {
                    ownTeam = row.Team;
                    break;
                }
            }

            var ended = match.State == GameModeState.Ended;
            shown = new Shown(
                points.Count,
                match.ActivePointIndex,
                // サーバーは正の残りを切り上げて送るので、拠点がある間に 0 は来ない。来ても 0 は「もう移った」になり写せないので 1 秒として出す
                Math.Max(1, Math.Min(match.ObjectiveRemainingSeconds, _mirrorRotateSeconds)),
                alphaOn,
                bravoOn,
                match.AlphaPoints,
                match.BravoPoints,
                match.RemainingSeconds,
                ownTeam,
                ended,
                ended && match.HasWinner ? match.Winner : (Team?)null);
            return true;
        }

        private void Mirror(Shown shown)
        {
            if (_mirror == null || _mirror.PointCount != shown.PointCount)
            {
                // 点・時間切れ・出直しは写しでは起こさないので、上限は届かない値にする
                _mirror = new Hardpoint(new HardpointSpec(
                    shown.PointCount, _mirrorRotateSeconds, HardpointSpec.DefaultPointsPerSecond, int.MaxValue, float.MaxValue, 0f));
                _mirror.Session.AddPlayer(_alphaProbe, Side.Alpha);
                _mirror.Session.AddPlayer(_bravoProbe, Side.Bravo);
            }
            else
            {
                _mirror.Reset();
            }

            // 誰も拠点に立たせないまま進めるので、写しに点は入らない
            _mirror.Session.Start();
            var elapsed = (shown.ActivePoint * _mirrorRotateSeconds) + (_mirrorRotateSeconds - shown.SecondsUntilMove);
            _mirror.Session.Tick(elapsed);
            _mirror.SetPlayerPoint(_alphaProbe, shown.AlphaOn ? shown.ActivePoint : (int?)null);
            _mirror.SetPlayerPoint(_bravoProbe, shown.BravoOn ? shown.ActivePoint : (int?)null);
        }

        private readonly struct Shown : IEquatable<Shown>
        {
            public Shown(
                int pointCount,
                int activePoint,
                int secondsUntilMove,
                bool alphaOn,
                bool bravoOn,
                int alphaPoints,
                int bravoPoints,
                int remainingSeconds,
                Team? ownTeam,
                bool isEnded,
                Team? winner)
            {
                PointCount = pointCount;
                ActivePoint = activePoint;
                SecondsUntilMove = secondsUntilMove;
                AlphaOn = alphaOn;
                BravoOn = bravoOn;
                AlphaPoints = alphaPoints;
                BravoPoints = bravoPoints;
                RemainingSeconds = remainingSeconds;
                OwnTeam = ownTeam;
                IsEnded = isEnded;
                Winner = winner;
            }

            public int PointCount { get; }

            public int ActivePoint { get; }

            public int SecondsUntilMove { get; }

            public bool AlphaOn { get; }

            public bool BravoOn { get; }

            public int AlphaPoints { get; }

            public int BravoPoints { get; }

            public int RemainingSeconds { get; }

            public Team? OwnTeam { get; }

            public bool IsEnded { get; }

            public Team? Winner { get; }

            public bool SamePoint(Shown other)
            {
                return PointCount == other.PointCount
                    && ActivePoint == other.ActivePoint
                    && SecondsUntilMove == other.SecondsUntilMove
                    && AlphaOn == other.AlphaOn
                    && BravoOn == other.BravoOn;
            }

            public bool Equals(Shown other)
            {
                return SamePoint(other)
                    && AlphaPoints == other.AlphaPoints
                    && BravoPoints == other.BravoPoints
                    && RemainingSeconds == other.RemainingSeconds
                    && OwnTeam == other.OwnTeam
                    && IsEnded == other.IsEnded
                    && Winner == other.Winner;
            }

            public override bool Equals(object obj) => obj is Shown other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(PointCount, ActivePoint, SecondsUntilMove, AlphaPoints, BravoPoints, RemainingSeconds);
        }
    }
}
