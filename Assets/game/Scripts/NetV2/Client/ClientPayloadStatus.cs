using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // Detail に Core の PayloadStatus を置くのは、オフラインの PayloadRunner と同じ型にして、UI が PayloadStatus に登録した書式役をそのまま使えるようにするため。
    // サーバーが送るのは進み・通った中継点の数・押している人数と、台車の物 (ObjectiveItemKind.Cart) の陣営 (= 攻め側) だけ。
    // 中継点の数は道筋と同じくマップで決まるので、手元のシーンの台車から使う側が渡す。
    // 取り合いと戻り中は送られないので、前のスナップショットからの進みの増減で決める
    public sealed class ClientPayloadStatus
    {
        private readonly int _localPlayerId;
        private Shown _shown;
        private bool _hasPrevious;
        private int _previousTick;
        private float _previousProgress;
        private bool _previousPlaying;
        private bool _contested;
        private bool _rollingBack;

        public ClientPayloadStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // Payload の試合のスナップショットが届いていなければ null
        public ModeStatus Status { get; private set; }

        public PayloadStatus Payload => Status?.Detail as PayloadStatus;

        // サーバーが手元の道筋より多くの中継点を通ったと送ってきたら true。例外にせず手元の数に寄せて見せ、知らせるかは使う側が決める
        public bool CheckpointMismatch { get; private set; }

        // Status を作り直したら true。届いた値が前と同じなら、UI が持っている Status を差し替えない
        public bool Apply(SnapshotMessage snapshot, int checkpointCount)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (checkpointCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(checkpointCount), checkpointCount, "checkpointCount must not be negative");
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.Payload || !TryFindAttackers(snapshot, out var attackers))
            {
                return Clear();
            }

            var progress = Math.Max(0f, Math.Min(1f, match.PayloadProgress));
            var pushers = Math.Max(0, match.PayloadPushers);
            var playing = match.State == GameModeState.Playing;
            Follow(snapshot.Tick, progress, pushers, playing);

            var reached = Math.Max(0, match.PayloadCheckpointsPassed);
            CheckpointMismatch = reached > checkpointCount;
            if (CheckpointMismatch)
            {
                reached = checkpointCount;
            }

            var ended = match.State == GameModeState.Ended;
            var shown = new Shown(
                attackers,
                progress,
                reached,
                checkpointCount,
                pushers,
                _contested,
                _rollingBack,
                match.AlphaPoints,
                match.BravoPoints,
                Math.Max(0, match.RemainingSeconds),
                OwnSide(snapshot),
                RespawnSeconds(snapshot),
                ended,
                ended && match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null);
            if (Status != null && shown.Equals(_shown))
            {
                return false;
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
                shown.OwnSide,
                shown.RespawnSeconds,
                shown.IsEnded,
                shown.Winner,
                new PayloadStatus(
                    shown.Attackers,
                    shown.Progress,
                    shown.CheckpointsReached,
                    shown.CheckpointCount,
                    shown.Pushers,
                    shown.IsContested,
                    shown.IsRollingBack));
            return true;
        }

        // 試合を離れたときや、Payload でない試合に移ったときに呼ぶ。消したら true
        public bool Clear()
        {
            _hasPrevious = false;
            _contested = false;
            _rollingBack = false;
            CheckpointMismatch = false;
            if (Status == null)
            {
                return false;
            }

            Status = null;
            _shown = default;
            return true;
        }

        // オフラインの Payload と同じく、取り合いは押している人がいるのに進まないとき、戻り中は押す人がいないのに下がったとき。
        // 前も今も試合中の、より新しい tick どうしでだけ比べる。試合の始まりで進みが 0 に戻るのを戻り中と読まないため
        private void Follow(int tick, float progress, int pushers, bool playing)
        {
            if (_hasPrevious && tick <= _previousTick)
            {
                return;
            }

            if (!playing)
            {
                _contested = false;
                _rollingBack = false;
            }
            else if (_hasPrevious && _previousPlaying)
            {
                _contested = pushers > 0 && progress <= _previousProgress && progress < 1f;
                _rollingBack = pushers == 0 && progress < _previousProgress;
            }
            else
            {
                _contested = false;
                _rollingBack = false;
            }

            _hasPrevious = true;
            _previousTick = tick;
            _previousProgress = progress;
            _previousPlaying = playing;
        }

        private static bool TryFindAttackers(SnapshotMessage snapshot, out Side attackers)
        {
            var items = snapshot.ObjectiveItems;
            for (var i = 0; i < items.Count; i++)
            {
                ref var item = ref items[i];
                if (item.Kind == ObjectiveItemKind.Cart)
                {
                    attackers = Side.FromTeam(item.Team);
                    return true;
                }
            }

            attackers = default;
            return false;
        }

        private Side? OwnSide(SnapshotMessage snapshot)
        {
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId == _localPlayerId)
                {
                    return Side.FromTeam(row.Team);
                }
            }

            return null;
        }

        private static float? RespawnSeconds(SnapshotMessage snapshot)
        {
            if (!snapshot.HasOwner || !snapshot.Owner.RespawnSeconds.HasValue)
            {
                return null;
            }

            return Math.Max(0, snapshot.Owner.RespawnSeconds.Value);
        }

        private readonly struct Shown : IEquatable<Shown>
        {
            public Shown(
                Side attackers,
                float progress,
                int checkpointsReached,
                int checkpointCount,
                int pushers,
                bool isContested,
                bool isRollingBack,
                int alphaPoints,
                int bravoPoints,
                int remainingSeconds,
                Side? ownSide,
                float? respawnSeconds,
                bool isEnded,
                Side? winner)
            {
                Attackers = attackers;
                Progress = progress;
                CheckpointsReached = checkpointsReached;
                CheckpointCount = checkpointCount;
                Pushers = pushers;
                IsContested = isContested;
                IsRollingBack = isRollingBack;
                AlphaPoints = alphaPoints;
                BravoPoints = bravoPoints;
                RemainingSeconds = remainingSeconds;
                OwnSide = ownSide;
                RespawnSeconds = respawnSeconds;
                IsEnded = isEnded;
                Winner = winner;
            }

            public Side Attackers { get; }

            public float Progress { get; }

            public int CheckpointsReached { get; }

            public int CheckpointCount { get; }

            public int Pushers { get; }

            public bool IsContested { get; }

            public bool IsRollingBack { get; }

            public int AlphaPoints { get; }

            public int BravoPoints { get; }

            public int RemainingSeconds { get; }

            public Side? OwnSide { get; }

            public float? RespawnSeconds { get; }

            public bool IsEnded { get; }

            public Side? Winner { get; }

            public bool Equals(Shown other)
            {
                return Attackers == other.Attackers
                    && Progress == other.Progress
                    && CheckpointsReached == other.CheckpointsReached
                    && CheckpointCount == other.CheckpointCount
                    && Pushers == other.Pushers
                    && IsContested == other.IsContested
                    && IsRollingBack == other.IsRollingBack
                    && AlphaPoints == other.AlphaPoints
                    && BravoPoints == other.BravoPoints
                    && RemainingSeconds == other.RemainingSeconds
                    && OwnSide == other.OwnSide
                    && RespawnSeconds == other.RespawnSeconds
                    && IsEnded == other.IsEnded
                    && Winner == other.Winner;
            }

            public override bool Equals(object obj) => obj is Shown other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Attackers, Progress, CheckpointsReached, Pushers, RemainingSeconds);
        }
    }
}
