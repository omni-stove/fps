using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    /// <summary>
    /// ボイスチャットの、プレイヤーが決める設定 (マイクの入り切りと、受ける声の音量)。
    /// JSON (<see cref="ToJson"/> / <see cref="FromJson"/>) で保存する。
    /// </summary>
    public sealed class VoiceSettings : IEquatable<VoiceSettings>
    {
        public const int CurrentVersion = 1;

        public const bool DefaultMicrophoneEnabled = true;

        /// <summary>受ける声の音量の既定 (%)。100 でそのまま。</summary>
        public const int DefaultVolumePercent = 100;

        /// <summary>受ける声の音量の最大 (%)。EOS の RTC の受ける音量の上限 (200%) に合わせる。</summary>
        public const int MaxVolumePercent = 200;

        /// <summary>受ける声の音量の既定 (倍率)。</summary>
        public const float DefaultVolume = DefaultVolumePercent / 100f;

        /// <summary>受ける声の音量の最大 (倍率)。</summary>
        public const float MaxVolume = MaxVolumePercent / 100f;

        private const string _versionKey = "version";
        private const string _microphoneKey = "microphoneEnabled";
        private const string _volumeKey = "volumePercent";

        private static readonly string[] _keys = { _versionKey, _microphoneKey, _volumeKey };

        public static readonly VoiceSettings Default = new VoiceSettings(DefaultMicrophoneEnabled, DefaultVolumePercent);

        /// <param name="microphoneEnabled">マイクを使うか。false なら押しても送らない。</param>
        /// <param name="volumePercent">受ける声の音量 (%)。0 で無音、<see cref="MaxVolumePercent"/> で最大。</param>
        /// <exception cref="ArgumentOutOfRangeException">volumePercent が範囲の外。</exception>
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

        /// <summary>受ける声の音量 (%)。</summary>
        public int VolumePercent { get; }

        /// <summary>受ける声の音量 (倍率)。0 で無音、1 でそのまま、<see cref="MaxVolume"/> で最大。</summary>
        public float Volume => VolumePercent / 100f;

        public VoiceSettings WithMicrophoneEnabled(bool enabled)
        {
            return new VoiceSettings(enabled, VolumePercent);
        }

        /// <summary>音量を変えた設定。範囲の外は範囲に収める。</summary>
        public VoiceSettings WithVolumePercent(int volumePercent)
        {
            return new VoiceSettings(MicrophoneEnabled, Math.Max(0, Math.Min(MaxVolumePercent, volumePercent)));
        }

        /// <summary>空白も改行も入れない 1 行の JSON。<see cref="FromJson"/> で同じ設定に読み戻せる。</summary>
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

        /// <summary>
        /// <see cref="ToJson"/> の JSON を読む。読めない所は初期値 (範囲の外の音量は範囲に収めた値) にし、理由を <paramref name="problems"/> に入れる。
        /// 無い欄は理由を出さずに初期値で補う。
        /// </summary>
        /// <exception cref="ArgumentNullException">json が null。</exception>
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
