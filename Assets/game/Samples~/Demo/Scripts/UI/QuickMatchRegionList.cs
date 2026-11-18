using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 地域の名前はゲームごとに置くサーバーの --region と合わせる値なので、コードでなく Resources のファイルで決める。
    public static class QuickMatchRegionList
    {
        public const string ResourcePath = "Settings/QuickMatchRegions";

        public const string RegionsKey = "regions";

        private const char _byteOrderMark = (char)0xFEFF;

        public static IReadOnlyList<string> BuiltIn { get; } =
            new ReadOnlyCollection<string>(new[] { "asia", "europe", "north-america" });

        public static IReadOnlyList<string> Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning(
                    $"{SettingsView.LogTag} 'Resources/{ResourcePath}.json' was not found; offering {string.Join(", ", BuiltIn)} as the Quick Match regions");
                return BuiltIn;
            }

            return Read($"Resources/{ResourcePath}.json", asset.text, message => Debug.LogWarning($"{SettingsView.LogTag} {message}"));
        }

        // 1 つの名前が読めなくても他の地域は選べるよう、その名前だけ飛ばす。ファイルごと読めないときだけ BuiltIn にする。
        public static IReadOnlyList<string> Read(string name, string json, Action<string> warn)
        {
            if (json == null)
            {
                warn?.Invoke($"{name}: the text is missing; using {string.Join(", ", BuiltIn)}");
                return BuiltIn;
            }

            if (json.Length > 0 && json[0] == _byteOrderMark)
            {
                json = json.Substring(1);
            }

            object parsed;
            try
            {
                parsed = SaveJson.Parse(json);
            }
            catch (FormatException e)
            {
                warn?.Invoke($"{name}: {e.Message}; using {string.Join(", ", BuiltIn)}");
                return BuiltIn;
            }

            if (!(parsed is Dictionary<string, object> root)
                || !root.TryGetValue(RegionsKey, out var rawRegions)
                || !(rawRegions is List<object> rows))
            {
                warn?.Invoke($"{name}: the top level must be an object with a '{RegionsKey}' array; using {string.Join(", ", BuiltIn)}");
                return BuiltIn;
            }

            foreach (var key in root.Keys)
            {
                if (key != RegionsKey)
                {
                    warn?.Invoke($"{name}: unknown key '{key}' is ignored");
                }
            }

            var regions = new List<string>();
            for (var i = 0; i < rows.Count; i++)
            {
                var where = $"{name}: {RegionsKey}[{i}]";
                if (!(rows[i] is string region) || !ControlSettings.IsValidRegion(region))
                {
                    warn?.Invoke($"{where} is not a region name (a-z, 0-9 and '-', up to {ControlSettings.MaxRegionLength}); skipping it");
                    continue;
                }

                if (region == ControlSettings.AutoRegion)
                {
                    warn?.Invoke($"{where} '{region}' is always offered first; skipping it");
                    continue;
                }

                if (regions.Contains(region))
                {
                    warn?.Invoke($"{where} '{region}' is listed twice; skipping it");
                    continue;
                }

                regions.Add(region);
            }

            return new ReadOnlyCollection<string>(regions);
        }
    }
}
