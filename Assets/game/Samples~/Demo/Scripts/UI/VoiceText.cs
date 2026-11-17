using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// ボイスの欄 (<see cref="VoiceIndicatorView"/>) と設定画面のボイスの行に出す文。
    /// 語は表 (Resources/Localization/&lt;言語&gt;/voice.json) のキーで持ち、話している人の名前は参加者の表 (<see cref="IParticipants"/>) から引く。
    /// </summary>
    public static class VoiceText
    {
        /// <summary>自分が声を送っている印の行。</summary>
        public const string LocalKey = "voice.local";

        /// <summary>話している人の行。{0} が名前。</summary>
        public const string SpeakingKey = "voice.speaking";

        /// <summary>参加者の表に無い Id の名前。{0} が Id。</summary>
        public const string UnknownSpeakerKey = "voice.unknownSpeaker";

        /// <summary>設定画面のボイスの見出し。</summary>
        public const string SettingsSectionKey = "voice.settings.section";

        /// <summary>設定画面のマイクの入り切りの行の名前。</summary>
        public const string MicrophoneKey = "voice.settings.microphone";

        /// <summary>設定画面の声の音量の行の名前。</summary>
        public const string VolumeKey = "voice.settings.volume";

        /// <summary>表にあるべきすべてのキー。</summary>
        public static IReadOnlyList<string> Keys { get; } = new[]
        {
            LocalKey, SpeakingKey, UnknownSpeakerKey, SettingsSectionKey, MicrophoneKey, VolumeKey,
        };

        /// <summary>id の参加者の名前。participants が null か、表に無ければ <see cref="UnknownSpeakerKey"/> の文。</summary>
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

        /// <summary>欄の行。自分が送っていればその印を先頭に、続けて話している人を <see cref="VoiceStatus.SpeakingIds"/> の並びで 1 行ずつ。</summary>
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
