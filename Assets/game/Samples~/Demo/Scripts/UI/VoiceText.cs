using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class VoiceText
    {
        public const string LocalKey = "voice.local";

        public const string SpeakingKey = "voice.speaking";

        public const string UnknownSpeakerKey = "voice.unknownSpeaker";

        public const string SettingsSectionKey = "voice.settings.section";

        public const string MicrophoneKey = "voice.settings.microphone";

        public const string VolumeKey = "voice.settings.volume";

        public static IReadOnlyList<string> Keys { get; } = new[]
        {
            LocalKey, SpeakingKey, UnknownSpeakerKey, SettingsSectionKey, MicrophoneKey, VolumeKey,
        };

        public static LocalizedText NameOf(int id, IParticipants participants)
        {
            return participants != null && participants.TryGet(id, out var participant)
                ? LocalizedText.Literal(participant.Name)
                : new LocalizedText(UnknownSpeakerKey, id);
        }

        public static LocalizedText Local()
        {
            return new LocalizedText(LocalKey);
        }

        public static LocalizedText Speaking(int id, IParticipants participants)
        {
            return new LocalizedText(SpeakingKey, NameOf(id, participants));
        }

        public static List<LocalizedText> Lines(VoiceStatus status, IParticipants participants)
        {
            var lines = new List<LocalizedText>();
            if (status.IsLocalSpeaking)
            {
                lines.Add(Local());
            }

            foreach (var id in status.SpeakingIds)
            {
                lines.Add(Speaking(id, participants));
            }

            return lines;
        }
    }
}
