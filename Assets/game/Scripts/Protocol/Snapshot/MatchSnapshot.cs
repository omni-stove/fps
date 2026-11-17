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

        public bool HasRound { get; set; }

        public RoundPhase Round { get; set; }

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

            if (Objective != baseline.Objective || AlphaPoints != baseline.AlphaPoints || BravoPoints != baseline.BravoPoints)
            {
                fields |= MatchFields.Objective;
            }

            if (HasRound != baseline.HasRound || (HasRound && Round != baseline.Round))
            {
                fields |= MatchFields.Round;
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
            }

            if ((fields & MatchFields.Round) != 0)
            {
                writer.WriteBool(HasRound);
                writer.WriteByte(HasRound ? (byte)Round : (byte)0);
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
                Rules = MatchRulesSync.Read(reader);
            }

            if ((fields & MatchFields.Objective) != 0)
            {
                Objective = (MatchObjective)reader.ReadByteBelow(EnumRange<MatchObjective>.Count, "match objective");
                AlphaPoints = reader.ReadInt32AtLeast(0, "alpha points");
                BravoPoints = reader.ReadInt32AtLeast(0, "bravo points");
            }

            if ((fields & MatchFields.Round) != 0)
            {
                HasRound = reader.ReadBool();
                Round = (RoundPhase)reader.ReadByteBelow(EnumRange<RoundPhase>.Count, "round phase");
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
            }

            if ((missing & MatchFields.Round) != 0)
            {
                HasRound = baseline.HasRound;
                Round = baseline.Round;
            }
        }
    }
}
