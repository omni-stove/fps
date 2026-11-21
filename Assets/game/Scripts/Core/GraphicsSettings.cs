using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 画質の段階は名前でなく番号で持つ。段階の並びと名前はゲームごとのプロジェクト設定 (Quality) が決め、キットは知らないため。
    public sealed class GraphicsSettings : IEquatable<GraphicsSettings>
    {
        public const int CurrentVersion = 1;

        // プロジェクト設定がその機器に選ぶ画質の段階のまま、という意味。
        public const int ProjectQualityLevel = -1;

        public const int MaxQualityLevel = 63;

        public const int NoFrameRateLimit = 0;

        public const int MinFrameRateLimit = 15;

        public const int MaxFrameRateLimit = 1000;

        // ProjectSettings/QualitySettings の各段階が VSync を切っており、上限も入れていなかったので、今までの動きを既定値にする。
        public const bool DefaultVSync = false;

        public const int DefaultFrameRateLimit = NoFrameRateLimit;

        public const int DefaultQualityLevel = ProjectQualityLevel;

        private const string _versionKey = "version";
        private const string _widthKey = "resolutionWidth";
        private const string _heightKey = "resolutionHeight";
        private const string _qualityKey = "qualityLevel";
        private const string _vSyncKey = "vSync";
        private const string _frameRateKey = "frameRateLimit";

        private static readonly string[] _keys = { _versionKey, _widthKey, _heightKey, _qualityKey, _vSyncKey, _frameRateKey };

        public GraphicsSettings(ScreenResolution resolution, int qualityLevel, bool vSync, int frameRateLimit)
        {
            if (!IsValidQualityLevel(qualityLevel))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(qualityLevel),
                    qualityLevel,
                    $"{_qualityKey} must be {Format(ProjectQualityLevel)} (the project's own level) or 0 to {Format(MaxQualityLevel)}");
            }

            if (!IsValidFrameRateLimit(frameRateLimit))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameRateLimit),
                    frameRateLimit,
                    $"{_frameRateKey} must be {Format(NoFrameRateLimit)} (no limit) or {Format(MinFrameRateLimit)} to {Format(MaxFrameRateLimit)}");
            }

            Resolution = resolution;
            QualityLevel = qualityLevel;
            VSync = vSync;
            FrameRateLimit = frameRateLimit;
        }

        public static GraphicsSettings Default { get; } =
            new GraphicsSettings(ScreenResolution.Native, DefaultQualityLevel, DefaultVSync, DefaultFrameRateLimit);

        public ScreenResolution Resolution { get; }

        public int QualityLevel { get; }

        public bool VSync { get; }

        public int FrameRateLimit { get; }

        public bool HasFrameRateLimit => FrameRateLimit != NoFrameRateLimit;

        public static bool IsValidQualityLevel(int level)
        {
            return level == ProjectQualityLevel || (level >= 0 && level <= MaxQualityLevel);
        }

        public static bool IsValidFrameRateLimit(int limit)
        {
            return limit == NoFrameRateLimit || (limit >= MinFrameRateLimit && limit <= MaxFrameRateLimit);
        }

        public GraphicsSettings WithResolution(ScreenResolution resolution)
        {
            return resolution == Resolution ? this : new GraphicsSettings(resolution, QualityLevel, VSync, FrameRateLimit);
        }

        public GraphicsSettings WithQualityLevel(int level)
        {
            return level == QualityLevel ? this : new GraphicsSettings(Resolution, level, VSync, FrameRateLimit);
        }

        public GraphicsSettings WithVSync(bool vSync)
        {
            return vSync == VSync ? this : new GraphicsSettings(Resolution, QualityLevel, vSync, FrameRateLimit);
        }

        public GraphicsSettings WithFrameRateLimit(int limit)
        {
            return limit == FrameRateLimit ? this : new GraphicsSettings(Resolution, QualityLevel, VSync, limit);
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendKey(builder, _versionKey).Append(Format(CurrentVersion)).Append(',');
            AppendKey(builder, _widthKey).Append(Format(Resolution.Width)).Append(',');
            AppendKey(builder, _heightKey).Append(Format(Resolution.Height)).Append(',');
            AppendKey(builder, _qualityKey).Append(Format(QualityLevel)).Append(',');
            AppendKey(builder, _vSyncKey).Append(VSync ? "true" : "false").Append(',');
            AppendKey(builder, _frameRateKey).Append(Format(FrameRateLimit));
            builder.Append('}');
            return builder.ToString();
        }

        // 欠けた値や読めない値は、保存した後でゲームが変えた既定値で埋めるよう、呼ぶ側の既定値 (データから読んだもの) で埋める。
        public static GraphicsSettings FromJson(string json, GraphicsSettings defaults, out IReadOnlyList<string> problems)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
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
                return defaults;
            }

            if (!(parsed is Dictionary<string, object> root))
            {
                found.Add("the root must be an object");
                return defaults;
            }

            // 形式が変わったときに古い解釈で読み違えないよう、読めるバージョンでなければ中身は見ない。
            if (!root.TryGetValue(_versionKey, out var versionValue) || !(versionValue is long version)
                || version < 1 || version > CurrentVersion)
            {
                found.Add($"unsupported version (expected 1 to {Format(CurrentVersion)})");
                return defaults;
            }

            foreach (var key in root.Keys)
            {
                if (Array.IndexOf(_keys, key) < 0)
                {
                    found.Add($"ignored unknown key '{key}'");
                }
            }

            var resolution = ReadResolution(root, defaults.Resolution, found);
            var quality = ReadQualityLevel(root, defaults.QualityLevel, found);
            var vSync = ReadFlag(root, _vSyncKey, defaults.VSync, found);
            var frameRate = ReadFrameRateLimit(root, defaults.FrameRateLimit, found);
            return new GraphicsSettings(resolution, quality, vSync, frameRate);
        }

        // 範囲より小さい正の値を「無制限」に読むと絞りたかった意図と逆になるので、0 以下だけを無制限にし、他は近い端にする。
        public static int ClampFrameRateLimit(long limit)
        {
            if (limit <= NoFrameRateLimit)
            {
                return NoFrameRateLimit;
            }

            return (int)Math.Max(MinFrameRateLimit, Math.Min(MaxFrameRateLimit, limit));
        }

        public bool Equals(GraphicsSettings other)
        {
            return other != null
                && Resolution == other.Resolution
                && QualityLevel == other.QualityLevel
                && VSync == other.VSync
                && FrameRateLimit == other.FrameRateLimit;
        }

        public override bool Equals(object obj)
        {
            return obj is GraphicsSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Resolution, QualityLevel, VSync, FrameRateLimit);
        }

        public override string ToString()
        {
            return "resolution=" + Resolution
                + " quality=" + (QualityLevel == ProjectQualityLevel ? "project" : Format(QualityLevel))
                + " vSync=" + (VSync ? "on" : "off")
                + " frameRateLimit=" + (HasFrameRateLimit ? Format(FrameRateLimit) : "none");
        }

        private static ScreenResolution ReadResolution(Dictionary<string, object> root, ScreenResolution fallback, List<string> problems)
        {
            var hasWidth = root.TryGetValue(_widthKey, out var widthValue);
            var hasHeight = root.TryGetValue(_heightKey, out var heightValue);
            if (!hasWidth && !hasHeight)
            {
                problems.Add($"missing '{_widthKey}' and '{_heightKey}'");
                return fallback;
            }

            if (!(widthValue is long width) || !(heightValue is long height))
            {
                problems.Add($"'{_widthKey}' and '{_heightKey}' must both be integers");
                return fallback;
            }

            if (width > int.MaxValue || height > int.MaxValue || !ScreenResolution.IsValid((int)width, (int)height))
            {
                problems.Add(
                    $"resolution {Format(width)}x{Format(height)} is not 0x0 or within {Format(ScreenResolution.MinSide)} to {Format(ScreenResolution.MaxSide)}; using the display's own");
                return ScreenResolution.Native;
            }

            return new ScreenResolution((int)width, (int)height);
        }

        private static int ReadQualityLevel(Dictionary<string, object> root, int fallback, List<string> problems)
        {
            if (!root.TryGetValue(_qualityKey, out var raw))
            {
                problems.Add($"missing '{_qualityKey}'");
                return fallback;
            }

            if (!(raw is long level))
            {
                problems.Add($"'{_qualityKey}' must be an integer");
                return fallback;
            }

            if (level < int.MinValue || level > int.MaxValue || !IsValidQualityLevel((int)level))
            {
                problems.Add(
                    $"'{_qualityKey}' {Format(level)} is not {Format(ProjectQualityLevel)} or 0 to {Format(MaxQualityLevel)}");
                return fallback;
            }

            return (int)level;
        }

        private static int ReadFrameRateLimit(Dictionary<string, object> root, int fallback, List<string> problems)
        {
            if (!root.TryGetValue(_frameRateKey, out var raw))
            {
                problems.Add($"missing '{_frameRateKey}'");
                return fallback;
            }

            if (!(raw is long limit))
            {
                problems.Add($"'{_frameRateKey}' must be an integer");
                return fallback;
            }

            if (limit < int.MinValue || limit > int.MaxValue || !IsValidFrameRateLimit((int)limit))
            {
                var clamped = ClampFrameRateLimit(limit);
                problems.Add(
                    $"'{_frameRateKey}' {Format(limit)} is not {Format(NoFrameRateLimit)} or {Format(MinFrameRateLimit)} to {Format(MaxFrameRateLimit)}; using {Format(clamped)}");
                return clamped;
            }

            return (int)limit;
        }

        private static bool ReadFlag(Dictionary<string, object> root, string key, bool fallback, List<string> problems)
        {
            if (!root.TryGetValue(key, out var raw))
            {
                problems.Add($"missing '{key}'");
                return fallback;
            }

            if (!(raw is bool flag))
            {
                problems.Add($"'{key}' must be true or false");
                return fallback;
            }

            return flag;
        }

        private static StringBuilder AppendKey(StringBuilder builder, string key)
        {
            SaveJson.AppendString(builder, key);
            return builder.Append(':');
        }

        private static string Format(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
