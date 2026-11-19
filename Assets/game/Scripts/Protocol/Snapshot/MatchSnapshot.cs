using Shooter.Core;

namespace Shooter.Protocol
{
    public struct MatchSnapshot
    {
        public GameModeState State { get; set; }

        public bool HasWinner { get; set; }

        public Team Winner { get; set; }

        public int AlphaScore { get; set; }
        public int BravoScore { get; set; }

        public int RemainingSeconds { get; set; }

        public int NextMatchSeconds { get; set; }

        public int NextWaveSeconds { get; set; }

        public MatchRulesSync Rules { get; set; }

        public MatchObjective Objective { get; set; }
        public int AlphaPoints { get; set; }
        public int BravoPoints { get; set; }

        // Kill Confirmed の阻止の数は勝敗の点に数えないので、AlphaPoints に混ぜずに別の欄で送る
        public int AlphaDenied { get; set; }
        public int BravoDenied { get; set; }

        public bool HasRound { get; set; }

        public RoundPhase Round { get; set; }

        public bool HasActivePoint { get; set; }

        public int ActivePointIndex { get; set; }

        public bool HasBomb { get; set; }

        public BombState Bomb { get; set; }

        public int ObjectiveRemainingSeconds { get; set; }

        // 始まる前は 0
        public int RoundNumber { get; set; }

        // ラウンド制でない目的では 0
        public int RoundsToWin { get; set; }

        public bool HasLastRound { get; set; }

        public RoundEndReason LastRoundEnd { get; set; }

        // 前のラウンドが引き分けなら false
        public bool HasLastRoundWinner { get; set; }

        public Team LastRoundWinner { get; set; }

        // 0〜1
        public float PlantFraction { get; set; }

        public bool HasDefuser { get; set; }

        public int DefuserId { get; set; }

        // 0〜1
        public float DefuseFraction { get; set; }

        // 0〜1
        public float PayloadProgress { get; set; }

        public int PayloadCheckpointsPassed { get; set; }

        public int PayloadPushers { get; set; }

        internal MatchFields ChangedFields(in MatchSnapshot baseline)
        {
            var fields = MatchFields.None;
            if (State != baseline.State
                || HasWinner != baseline.HasWinner
                || (HasWinner && Winner != baseline.Winner)
                || AlphaScore != baseline.AlphaScore
                || BravoScore != baseline.BravoScore
                || RemainingSeconds != baseline.RemainingSeconds
                || NextMatchSeconds != baseline.NextMatchSeconds
                || NextWaveSeconds != baseline.NextWaveSeconds)
            {
                fields |= MatchFields.Status;
            }

            if (!Rules.SameAs(baseline.Rules))
            {
                fields |= MatchFields.Rules;
            }

            if (Objective != baseline.Objective
                || AlphaPoints != baseline.AlphaPoints
                || BravoPoints != baseline.BravoPoints
                || AlphaDenied != baseline.AlphaDenied
                || BravoDenied != baseline.BravoDenied)
            {
                fields |= MatchFields.Objective;
            }

            if (HasRound != baseline.HasRound || (HasRound && Round != baseline.Round))
            {
                fields |= MatchFields.Round;
            }

            if (HasActivePoint != baseline.HasActivePoint
                || (HasActivePoint && ActivePointIndex != baseline.ActivePointIndex)
                || HasBomb != baseline.HasBomb
                || (HasBomb && Bomb != baseline.Bomb)
                || ObjectiveRemainingSeconds != baseline.ObjectiveRemainingSeconds)
            {
                fields |= MatchFields.ObjectiveState;
            }

            if (RoundNumber != baseline.RoundNumber
                || RoundsToWin != baseline.RoundsToWin
                || HasLastRound != baseline.HasLastRound
                || (HasLastRound && LastRoundEnd != baseline.LastRoundEnd)
                || HasLastRoundWinner != baseline.HasLastRoundWinner
                || (HasLastRoundWinner && LastRoundWinner != baseline.LastRoundWinner))
            {
                fields |= MatchFields.RoundResult;
            }

            if (PlantFraction != baseline.PlantFraction
                || HasDefuser != baseline.HasDefuser
                || (HasDefuser && DefuserId != baseline.DefuserId)
                || DefuseFraction != baseline.DefuseFraction)
            {
                fields |= MatchFields.ObjectiveProgress;
            }

            if (PayloadProgress != baseline.PayloadProgress
                || PayloadCheckpointsPassed != baseline.PayloadCheckpointsPassed
                || PayloadPushers != baseline.PayloadPushers)
            {
                fields |= MatchFields.Payload;
            }

            return fields;
        }

        internal void Write(MessageWriter writer, MatchFields fields)
        {
            if ((fields & MatchFields.Status) != 0)
            {
                writer.WriteByte((byte)State);
                writer.WriteBool(HasWinner);
                writer.WriteByte(HasWinner ? (byte)Winner : (byte)0);
                writer.WriteInt32(AlphaScore);
                writer.WriteInt32(BravoScore);
                writer.WriteInt32(RemainingSeconds);
                writer.WriteInt32(NextMatchSeconds);
                writer.WriteInt32(NextWaveSeconds);
            }

            if ((fields & MatchFields.Rules) != 0)
            {
                Rules.Write(writer);
            }

            if ((fields & MatchFields.Objective) != 0)
            {
                writer.WriteByte((byte)Objective);
                writer.WriteInt32(AlphaPoints);
                writer.WriteInt32(BravoPoints);
                writer.WriteInt32(AlphaDenied);
                writer.WriteInt32(BravoDenied);
            }

            if ((fields & MatchFields.Round) != 0)
            {
                writer.WriteBool(HasRound);
                writer.WriteByte(HasRound ? (byte)Round : (byte)0);
            }

            if ((fields & MatchFields.ObjectiveState) != 0)
            {
                if (HasActivePoint && (ActivePointIndex < 0 || ActivePointIndex >= SnapshotMessage.MaxControlPoints))
                {
                    throw new System.InvalidOperationException(
                        "the active point index must be 0 to " + (SnapshotMessage.MaxControlPoints - 1));
                }

                if (ObjectiveRemainingSeconds < 0)
                {
                    throw new System.InvalidOperationException("objective remaining seconds must not be negative");
                }

                writer.WriteBool(HasActivePoint);
                writer.WriteByte(HasActivePoint ? (byte)ActivePointIndex : (byte)0);
                writer.WriteBool(HasBomb);
                writer.WriteByte(HasBomb ? (byte)Bomb : (byte)0);
                writer.WriteInt32(ObjectiveRemainingSeconds);
            }

            if ((fields & MatchFields.RoundResult) != 0)
            {
                if (RoundNumber < 0 || RoundsToWin < 0)
                {
                    throw new System.InvalidOperationException("the round number and the rounds to win must not be negative");
                }

                if (HasLastRoundWinner && !HasLastRound)
                {
                    throw new System.InvalidOperationException("a last round winner needs a last round result");
                }

                writer.WriteInt32(RoundNumber);
                writer.WriteInt32(RoundsToWin);
                writer.WriteBool(HasLastRound);
                writer.WriteByte(HasLastRound ? (byte)LastRoundEnd : (byte)0);
                writer.WriteBool(HasLastRoundWinner);
                writer.WriteByte(HasLastRoundWinner ? (byte)LastRoundWinner : (byte)0);
            }

            if ((fields & MatchFields.ObjectiveProgress) != 0)
            {
                RequireFraction(PlantFraction, "plant fraction");
                RequireFraction(DefuseFraction, "defuse fraction");
                if (HasDefuser && DefuserId < 0)
                {
                    throw new System.InvalidOperationException("the defuser id must not be negative");
                }

                writer.WriteSingle(PlantFraction);
                writer.WriteInt32(HasDefuser ? DefuserId : PlayerIds.None);
                writer.WriteSingle(DefuseFraction);
            }

            if ((fields & MatchFields.Payload) != 0)
            {
                RequireFraction(PayloadProgress, "payload progress");
                if (PayloadCheckpointsPassed < 0 || PayloadPushers < 0)
                {
                    throw new System.InvalidOperationException("the payload checkpoints and pushers must not be negative");
                }

                writer.WriteSingle(PayloadProgress);
                writer.WriteInt32(PayloadCheckpointsPassed);
                writer.WriteInt32(PayloadPushers);
            }
        }

        private static void RequireFraction(float value, string what)
        {
            if (!(value >= 0f && value <= 1f))
            {
                throw new System.InvalidOperationException(what + " must be within 0 to 1");
            }
        }

        internal void Read(MessageReader reader, MatchFields fields)
        {
            if ((fields & MatchFields.Status) != 0)
            {
                State = (GameModeState)reader.ReadByteBelow(EnumRange<GameModeState>.Count, "match state");
                HasWinner = reader.ReadBool();
                Winner = (Team)reader.ReadByteBelow(EnumRange<Team>.Count, "winner");
                AlphaScore = reader.ReadInt32AtLeast(0, "alpha score");
                BravoScore = reader.ReadInt32AtLeast(0, "bravo score");
                RemainingSeconds = reader.ReadInt32AtLeast(0, "remaining seconds");
                NextMatchSeconds = reader.ReadInt32AtLeast(0, "next match seconds");
                NextWaveSeconds = reader.ReadInt32AtLeast(0, "next wave seconds");
            }

            if ((fields & MatchFields.Rules) != 0)
            {
                Rules = MatchRulesSync.Read(reader, Rules);
            }

            if ((fields & MatchFields.Objective) != 0)
            {
                Objective = (MatchObjective)reader.ReadByteBelow(EnumRange<MatchObjective>.Count, "match objective");
                AlphaPoints = reader.ReadInt32AtLeast(0, "alpha points");
                BravoPoints = reader.ReadInt32AtLeast(0, "bravo points");
                AlphaDenied = reader.ReadInt32AtLeast(0, "alpha denied");
                BravoDenied = reader.ReadInt32AtLeast(0, "bravo denied");
            }

            if ((fields & MatchFields.Round) != 0)
            {
                HasRound = reader.ReadBool();
                Round = (RoundPhase)reader.ReadByteBelow(EnumRange<RoundPhase>.Count, "round phase");
            }

            if ((fields & MatchFields.ObjectiveState) != 0)
            {
                HasActivePoint = reader.ReadBool();
                ActivePointIndex = reader.ReadByteBelow(SnapshotMessage.MaxControlPoints, "active point");
                HasBomb = reader.ReadBool();
                Bomb = (BombState)reader.ReadByteBelow(EnumRange<BombState>.Count, "bomb state");
                ObjectiveRemainingSeconds = reader.ReadInt32AtLeast(0, "objective remaining seconds");
            }

            if ((fields & MatchFields.RoundResult) != 0)
            {
                RoundNumber = reader.ReadInt32AtLeast(0, "round number");
                RoundsToWin = reader.ReadInt32AtLeast(0, "rounds to win");
                HasLastRound = reader.ReadBool();
                LastRoundEnd = (RoundEndReason)reader.ReadByteBelow(EnumRange<RoundEndReason>.Count, "last round end");
                HasLastRoundWinner = reader.ReadBool();
                LastRoundWinner = (Team)reader.ReadByteBelow(EnumRange<Team>.Count, "last round winner");
                if (HasLastRoundWinner && !HasLastRound)
                {
                    throw new MessageReadException("a last round winner needs a last round result");
                }
            }

            if ((fields & MatchFields.ObjectiveProgress) != 0)
            {
                PlantFraction = reader.ReadFiniteSingleBetween(0f, 1f, "plant fraction");
                var defuser = reader.ReadInt32AtLeast(PlayerIds.None, "defuser");
                HasDefuser = defuser != PlayerIds.None;
                DefuserId = HasDefuser ? defuser : 0;
                DefuseFraction = reader.ReadFiniteSingleBetween(0f, 1f, "defuse fraction");
            }

            if ((fields & MatchFields.Payload) != 0)
            {
                PayloadProgress = reader.ReadFiniteSingleBetween(0f, 1f, "payload progress");
                PayloadCheckpointsPassed = reader.ReadInt32AtLeast(0, "payload checkpoints passed");
                PayloadPushers = reader.ReadInt32AtLeast(0, "payload pushers");
            }
        }

        internal void FillFrom(in MatchSnapshot baseline, MatchFields present)
        {
            var missing = MatchFields.All & ~present;
            if ((missing & MatchFields.Status) != 0)
            {
                State = baseline.State;
                HasWinner = baseline.HasWinner;
                Winner = baseline.Winner;
                AlphaScore = baseline.AlphaScore;
                BravoScore = baseline.BravoScore;
                RemainingSeconds = baseline.RemainingSeconds;
                NextMatchSeconds = baseline.NextMatchSeconds;
                NextWaveSeconds = baseline.NextWaveSeconds;
            }

            if ((missing & MatchFields.Rules) != 0)
            {
                Rules = baseline.Rules;
            }

            if ((missing & MatchFields.Objective) != 0)
            {
                Objective = baseline.Objective;
                AlphaPoints = baseline.AlphaPoints;
                BravoPoints = baseline.BravoPoints;
                AlphaDenied = baseline.AlphaDenied;
                BravoDenied = baseline.BravoDenied;
            }

            if ((missing & MatchFields.Round) != 0)
            {
                HasRound = baseline.HasRound;
                Round = baseline.Round;
            }

            if ((missing & MatchFields.ObjectiveState) != 0)
            {
                HasActivePoint = baseline.HasActivePoint;
                ActivePointIndex = baseline.ActivePointIndex;
                HasBomb = baseline.HasBomb;
                Bomb = baseline.Bomb;
                ObjectiveRemainingSeconds = baseline.ObjectiveRemainingSeconds;
            }

            if ((missing & MatchFields.RoundResult) != 0)
            {
                RoundNumber = baseline.RoundNumber;
                RoundsToWin = baseline.RoundsToWin;
                HasLastRound = baseline.HasLastRound;
                LastRoundEnd = baseline.LastRoundEnd;
                HasLastRoundWinner = baseline.HasLastRoundWinner;
                LastRoundWinner = baseline.LastRoundWinner;
            }

            if ((missing & MatchFields.ObjectiveProgress) != 0)
            {
                PlantFraction = baseline.PlantFraction;
                HasDefuser = baseline.HasDefuser;
                DefuserId = baseline.DefuserId;
                DefuseFraction = baseline.DefuseFraction;
            }

            if ((missing & MatchFields.Payload) != 0)
            {
                PayloadProgress = baseline.PayloadProgress;
                PayloadCheckpointsPassed = baseline.PayloadCheckpointsPassed;
                PayloadPushers = baseline.PayloadPushers;
            }
        }
    }
}
