using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class CaptureTheFlag
    {
        // 段差の上下で旗を拾ったり運び込んだりしないよう、上下の差も見る。
        public const float ReachHeight = 2f;

        // 小刻みな Tick を足し合わせると戻る時刻にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        private static readonly Side[] _flagSides = { Side.Alpha, Side.Bravo };
        private static readonly IReadOnlyList<Side> _flagSidesView = Array.AsReadOnly(_flagSides);

        public event Action<Side, int> FlagTaken;

        public event Action<Side, int> FlagDropped;

        public event Action<Side, int?> FlagReturned;

        public event Action<Side, int> FlagCaptured;

        private readonly Dictionary<Side, Flag> _flags = new Dictionary<Side, Flag>();
        private readonly Dictionary<Side, int> _captures = new Dictionary<Side, int>();

        // イベントのハンドラの中で Reset されたら、その後の処理と通知を止めるために数える。
        private int _generation;

        public CaptureTheFlagSpec Spec { get; }

        public ModeSession Session { get; }

        public CaptureTheFlag(CaptureTheFlagSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new FlagClock(this) },
                new IndividualRespawn(spec.RespawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(spec.ScoreLimit), new TimeLimitWin() });
            foreach (var side in _flagSides)
            {
                _flags[side] = new Flag();
            }
        }

        public static IReadOnlyList<Side> FlagSides => _flagSidesView;

        public static Side Opponent(Side side)
        {
            RequireFlagSide(side, nameof(side));
            return side == Side.Alpha ? Side.Bravo : Side.Alpha;
        }

        public static bool IsWithin(Vector3 ground, Vector3 point, float radius)
        {
            var offset = ground - point;
            return Math.Abs(offset.Y) <= ReachHeight && ((offset.X * offset.X) + (offset.Z * offset.Z)) <= radius * radius;
        }

        public FlagState GetFlagState(Side flag) => Find(flag).State;

        public int? GetCarrierId(Side flag) => Find(flag).CarrierId;

        public float? GetReturnRemaining(Side flag)
        {
            var record = Find(flag);
            return record.State == FlagState.Dropped ? (float)Math.Max(0d, record.ReturnRemaining) : (float?)null;
        }

        public int GetCaptures(Side side) => _captures.TryGetValue(side, out var count) ? count : 0;

        public Side? GetCarriedFlag(int playerId)
        {
            foreach (var side in _flagSides)
            {
                var record = _flags[side];
                if (record.State == FlagState.Carried && record.CarrierId == playerId)
                {
                    return side;
                }
            }

            return null;
        }

        public bool IsCarrying(int playerId) => GetCarriedFlag(playerId).HasValue;

        public bool Touch(Side flag, int playerId)
        {
            var record = Find(flag);
            RequirePlayer(playerId, nameof(playerId));
            if (!CanAct(playerId))
            {
                return false;
            }

            if (Session.GetSide(playerId) == flag)
            {
                if (record.State != FlagState.Dropped)
                {
                    return false;
                }

                SendHome(record);
                FlagReturned?.Invoke(flag, playerId);
                return true;
            }

            if (record.State == FlagState.Carried)
            {
                return false;
            }

            record.State = FlagState.Carried;
            record.CarrierId = playerId;
            record.ReturnRemaining = 0d;
            FlagTaken?.Invoke(flag, playerId);
            return true;
        }

        public bool ReachBase(int playerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            if (!CanAct(playerId))
            {
                return false;
            }

            var carried = GetCarriedFlag(playerId);
            var side = Session.GetSide(playerId);
            if (!carried.HasValue || _flags[side].State != FlagState.AtBase)
            {
                return false;
            }

            SendHome(_flags[carried.Value]);
            _captures[side] = GetCaptures(side) + 1;
            var generation = _generation;
            Session.AddScore(side, Spec.PointsPerCapture);
            if (generation == _generation)
            {
                FlagCaptured?.Invoke(carried.Value, playerId);
            }

            return true;
        }

        public void ReportKill(int killerId, int victimId)
        {
            RequirePlayer(victimId, nameof(victimId));
            var deaths = Session.GetDeaths(victimId);
            Session.ReportKill(killerId, victimId);
            if (Session.GetDeaths(victimId) > deaths)
            {
                DropFrom(victimId);
            }
        }

        public void ReportDeath(int playerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            var deaths = Session.GetDeaths(playerId);
            Session.ReportDeath(playerId);
            if (Session.GetDeaths(playerId) > deaths)
            {
                DropFrom(playerId);
            }
        }

        // Session に直接報告すると旗は次の Tick で落ちるが、リスポーンが 0 秒だとその場で戻って倒れたことが残らないので、
        // 報告の直後にこれで落とす。
        public bool DropCarriedFlag(int playerId)
        {
            if (Session.State != GameModeState.Playing || !IsCarrying(playerId))
            {
                return false;
            }

            DropFrom(playerId);
            return true;
        }

        public void RemovePlayer(int playerId)
        {
            var carried = GetCarriedFlag(playerId);
            Session.RemovePlayer(playerId);
            if (!carried.HasValue)
            {
                return;
            }

            if (Session.State == GameModeState.Playing)
            {
                DropFrom(playerId);
            }
            else
            {
                SendHome(_flags[carried.Value]);
            }
        }

        public void Reset()
        {
            _generation++;
            foreach (var side in _flagSides)
            {
                SendHome(_flags[side]);
            }

            _captures.Clear();
            Session.Reset();
        }

        private bool CanAct(int playerId)
        {
            if (Session.State != GameModeState.Playing)
            {
                return false;
            }

            var generation = _generation;
            DropDownedCarriers();
            return generation == _generation
                && Session.State == GameModeState.Playing
                && Session.HasPlayer(playerId)
                && !Session.IsDown(playerId);
        }

        private void Advance(float seconds)
        {
            var generation = _generation;
            DropDownedCarriers();
            if (generation != _generation || !(seconds > 0f))
            {
                return;
            }

            foreach (var side in _flagSides)
            {
                var record = _flags[side];
                if (record.State != FlagState.Dropped)
                {
                    continue;
                }

                record.ReturnRemaining -= seconds;
                if (record.ReturnRemaining > _snap)
                {
                    continue;
                }

                SendHome(record);
                FlagReturned?.Invoke(side, null);
                if (generation != _generation)
                {
                    return;
                }
            }
        }

        private void DropDownedCarriers()
        {
            var generation = _generation;
            foreach (var side in _flagSides)
            {
                var record = _flags[side];
                if (record.State != FlagState.Carried || !record.CarrierId.HasValue)
                {
                    continue;
                }

                var carrier = record.CarrierId.Value;
                if (!Session.HasPlayer(carrier) || Session.IsDown(carrier))
                {
                    DropFrom(carrier);
                    if (generation != _generation)
                    {
                        return;
                    }
                }
            }
        }

        private void DropFrom(int carrierId)
        {
            if (Session.State != GameModeState.Playing)
            {
                return;
            }

            var flag = GetCarriedFlag(carrierId);
            if (!flag.HasValue)
            {
                return;
            }

            var record = _flags[flag.Value];
            record.State = FlagState.Dropped;
            record.CarrierId = null;
            record.ReturnRemaining = Spec.ReturnSeconds;
            FlagDropped?.Invoke(flag.Value, carrierId);
        }

        private static void SendHome(Flag record)
        {
            record.State = FlagState.AtBase;
            record.CarrierId = null;
            record.ReturnRemaining = 0d;
        }

        private Flag Find(Side flag)
        {
            RequireFlagSide(flag, nameof(flag));
            return _flags[flag];
        }

        private static void RequireFlagSide(Side side, string parameterName)
        {
            if (side != Side.Alpha && side != Side.Bravo)
            {
                throw new ArgumentOutOfRangeException(parameterName, side, "side must be Alpha or Bravo");
            }
        }

        private void RequirePlayer(int playerId, string parameterName)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(parameterName, playerId, "no player with this id");
            }
        }

        private sealed class Flag
        {
            public FlagState State { get; set; } = FlagState.AtBase;

            public int? CarrierId { get; set; }

            public double ReturnRemaining { get; set; }
        }

        // 点は入れないが IScoreRule にするのは、Session の時間で倒れた運ぶ人の旗を落とし、落ちた旗を戻すため。
        private sealed class FlagClock : IScoreRule
        {
            private readonly CaptureTheFlag _owner;

            public FlagClock(CaptureTheFlag owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsForAssist(ModeSession session, int assisterId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side) => 0d;

            public void Advance(ModeSession session, float seconds) => _owner.Advance(seconds);
        }
    }
}
