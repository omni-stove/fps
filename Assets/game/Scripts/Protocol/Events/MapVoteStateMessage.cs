using System;

namespace Shooter.Protocol
{
    public sealed class MapVoteStateMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int MaxCandidates = 16;

        public const int NotChosen = -1;

        private readonly string[] _mapIds = new string[MaxCandidates];
        private readonly int[] _votes = new int[MaxCandidates];
        private int _chosenIndex = NotChosen;

        public ushort Id => MessageIds.MapVoteState;

        public byte Version => CurrentVersion;

        public int CandidateCount { get; private set; }

        public float RemainingSeconds { get; set; }

        public int ChosenIndex
        {
            get => _chosenIndex;
            set
            {
                if (value < NotChosen || value >= MaxCandidates)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "chosen index must be -1 or below " + MaxCandidates);
                }

                _chosenIndex = value;
            }
        }

        public string MapId(int index)
        {
            CheckIndex(index);
            return _mapIds[index];
        }

        public int Votes(int index)
        {
            CheckIndex(index);
            return _votes[index];
        }

        public void ClearCandidates()
        {
            CandidateCount = 0;
        }

        public void AddCandidate(string mapId, int votes)
        {
            if (votes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(votes), votes, "votes must not be negative");
            }

            if (CandidateCount == MaxCandidates)
            {
                throw new InvalidOperationException("a map vote holds at most " + MaxCandidates + " candidates");
            }

            _mapIds[CandidateCount] = mapId ?? "";
            _votes[CandidateCount] = votes;
            CandidateCount++;
        }

        public void Write(MessageWriter writer)
        {
            if (float.IsNaN(RemainingSeconds) || float.IsInfinity(RemainingSeconds) || RemainingSeconds < 0f)
            {
                throw new InvalidOperationException("remaining seconds must be finite and not negative");
            }

            if (ChosenIndex >= CandidateCount)
            {
                throw new InvalidOperationException("chosen index " + ChosenIndex + " is not one of the " + CandidateCount + " candidates");
            }

            writer.WriteByte((byte)CandidateCount);
            for (var i = 0; i < CandidateCount; i++)
            {
                writer.WriteString(_mapIds[i], MatchProtocol.MaxContentIdBytes);
                writer.WriteInt32(_votes[i]);
            }

            writer.WriteSingle(RemainingSeconds);
            writer.WriteByte((byte)(ChosenIndex + 1));
        }

        public void Read(MessageReader reader)
        {
            CandidateCount = 0;
            var count = reader.ReadByteBelow(MaxCandidates + 1, "map vote candidate count");
            for (var i = 0; i < count; i++)
            {
                _mapIds[i] = reader.ReadString(MatchProtocol.MaxContentIdBytes, _mapIds[i]);
                _votes[i] = reader.ReadInt32AtLeast(0, "map votes");
            }

            CandidateCount = count;
            RemainingSeconds = reader.ReadFiniteSingleBetween(0f, float.MaxValue, "map vote remaining seconds");
            _chosenIndex = reader.ReadByteBelow(count + 1, "chosen map index") - 1;
        }

        private void CheckIndex(int index)
        {
            if ((uint)index >= (uint)CandidateCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + CandidateCount);
            }
        }
    }
}
