using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // 話している人を名前でなく参加者の Id で持つのは、名前に引くのを UI (IParticipants) に任せるため。
    public readonly struct VoiceStatus : IEquatable<VoiceStatus>
    {
        // 渡された一覧を後から書き換えられても変わらないよう写しを持つ。default ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly int[] _speakingIds;

        public VoiceStatus(bool isLocalSpeaking, IReadOnlyList<int> speakingIds, bool microphoneAvailable)
        {
            if (isLocalSpeaking && !microphoneAvailable)
            {
                throw new ArgumentException("the local player cannot speak without a microphone", nameof(isLocalSpeaking));
            }

            var copy = new int[speakingIds?.Count ?? 0];
            var seen = new HashSet<int>();
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = speakingIds[i];
                if (!seen.Add(copy[i]))
                {
                    throw new ArgumentException("speakingIds lists " + copy[i].ToString(CultureInfo.InvariantCulture) + " more than once", nameof(speakingIds));
                }
            }

            IsLocalSpeaking = isLocalSpeaking;
            _speakingIds = copy;
            MicrophoneAvailable = microphoneAvailable;
        }

        public bool IsLocalSpeaking { get; }

        public IReadOnlyList<int> SpeakingIds => _speakingIds ?? Array.Empty<int>();

        public bool MicrophoneAvailable { get; }

        public bool Equals(VoiceStatus other)
        {
            if (IsLocalSpeaking != other.IsLocalSpeaking || MicrophoneAvailable != other.MicrophoneAvailable)
            {
                return false;
            }

            var mine = SpeakingIds;
            var theirs = other.SpeakingIds;
            if (mine.Count != theirs.Count)
            {
                return false;
            }

            for (var i = 0; i < mine.Count; i++)
            {
                if (mine[i] != theirs[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is VoiceStatus other && Equals(other);
        }

        public static bool operator ==(VoiceStatus left, VoiceStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VoiceStatus left, VoiceStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(IsLocalSpeaking, MicrophoneAvailable);
            foreach (var id in SpeakingIds)
            {
                hash = HashCode.Combine(hash, id);
            }

            return hash;
        }

        public override string ToString()
        {
            var ids = new string[SpeakingIds.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = SpeakingIds[i].ToString(CultureInfo.InvariantCulture);
            }

            return "local=" + IsLocalSpeaking + " mic=" + MicrophoneAvailable + " speaking=[" + string.Join(", ", ids) + "]";
        }
    }
}
