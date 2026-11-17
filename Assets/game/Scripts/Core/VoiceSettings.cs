using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public sealed class VoiceSettings : IEquatable<VoiceSettings>
    {
        public const int CurrentVersion = 1;

        public const bool DefaultMicrophoneEnabled = true;

        public const int DefaultVolumePercent = 100;

        // 200 なのは、EOS の RTC の受ける音量の上限 (200%) に合わせるため。
        public const int MaxVolumePercent = 200;

        public const float DefaultVolume = DefaultVolumePercent / 100f;

        public const float MaxVolume = MaxVolumePercent / 100f;

        private const string _versionKey = "version";
        private const string _microphoneKey = "microphoneEnabled";
        private const string _volumeKey = "volumePercent";

        private static readonly string[] _keys = { _versionKey, _microphoneKey, _volumeKey };

        public static readonly VoiceSettings Default = new VoiceSettings(DefaultMicrophoneEnabled, DefaultVolumePercent);

        public VoiceSettings(bool microphoneEnabled, int volumePercent)
        {
            if (volumePercent < 0 || volumePercent > MaxVolumePercent)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(volumePercent), volumePercent, "the voice volume must be within 0 to " + Format(MaxVolumePercent) + "%");
            }

            MicrophoneEnabled = microphoneEnabled;
            VolumePercent = volumePercent;
        }

        public bool MicrophoneEnabled { get; }

        public int VolumePercent { get; }

        public float Volume => VolumePercent / 100f;

        public VoiceSettings WithMicrophoneEnabled(bool enabled)
        {
            return new VoiceSettings(enabled, VolumePercent);
        }

        public VoiceSettings WithVolumePercent(int volumePercent)
        {
            return new VoiceSettings(MicrophoneEnabled, Math.Max(0, Math.Min(MaxVolumePercent, volumePercent)));
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            SaveJson.AppendString(builder, _versionKey);
            builder.Append(':').Append(Format(CurrentVersion)).Append(',');
            SaveJson.AppendString(builder, _microphoneKey);
            builder.Append(':').Append(MicrophoneEnabled ? "true" : "false").Append(',');
            SaveJson.AppendString(builder, _volumeKey);
            builder.Append(':').Append(Format(VolumePercent));
            builder.Append('}');
            return builder.ToString();
        }

        public static VoiceSettings FromJson(string json, out IReadOnlyList<string> problems)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var found = new List<string>();
            problems = new ReadOnlyCollection<string>(found);
            object parsed;
            try
            {
                parsed = SaveJson.Parse(json);
            }
            catch (FormatException e)
            {
                found.Add("not valid JSON: " + e.Message);
                return Default;
            }

            if (!(parsed is Dictionary<string, object> root))
            {
                found.Add("the root must be an object");
                return Default;
            }

            // 形式が変わったときに古い解釈で読み違えないよう、読めるバージョンでなければ中身は見ない
            if (!root.TryGetValue(_versionKey, out var versionValue) || !(versionValue is long version)
                || version < 1 || version > CurrentVersion)
            {
                found.Add("unsupported version (expected 1 to " + Format(CurrentVersion) + ")");
                return Default;
            }

            foreach (var key in root.Keys)
            {
                if (Array.IndexOf(_keys, key) < 0)
                {
                    found.Add("ignored unknown key '" + key + "'");
                }
            }

            var microphone = Default.MicrophoneEnabled;
            if (root.TryGetValue(_microphoneKey, out var microphoneValue))
            {
                if (microphoneValue is bool flag)
                {
                    microphone = flag;
                }
                else
                {
                    found.Add("'" + _microphoneKey + "' must be true or false");
                }
            }

            var volume = Default.VolumePercent;
            if (root.TryGetValue(_volumeKey, out var volumeValue))
            {
                if (!(volumeValue is long number))
                {
                    found.Add("'" + _volumeKey + "' must be an integer");
                }
                else if (number < 0 || number > MaxVolumePercent)
                {
                    volume = (int)Math.Max(0, Math.Min(MaxVolumePercent, number));
                    found.Add("'" + _volumeKey + "' " + number.ToString(CultureInfo.InvariantCulture) + " is out of range (0 to "
                        + Format(MaxVolumePercent) + ")");
                }
                else
                {
                    volume = (int)number;
                }
            }

            return new VoiceSettings(microphone, volume);
        }

        public bool Equals(VoiceSettings other)
        {
            return other != null && MicrophoneEnabled == other.MicrophoneEnabled && VolumePercent == other.VolumePercent;
        }

        public override bool Equals(object obj)
        {
            return obj is VoiceSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MicrophoneEnabled, VolumePercent);
        }

        public override string ToString()
        {
            return "microphone=" + (MicrophoneEnabled ? "on" : "off") + " volume=" + Format(VolumePercent) + "%";
        }

        private static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
