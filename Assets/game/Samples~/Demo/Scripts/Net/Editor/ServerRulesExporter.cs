using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// Unity 抜きのサーバー (dotnet/Fps.Server) はアセットとシーンを読めないので、試合の設定 (<see cref="NetMatchDefinition"/>、
    /// <see cref="NetSceneBuilder.MatchDefinitionPath"/>) とルールのアセットを <c>server-data/rules/&lt;ルール名&gt;.json</c> に、
    /// 各マップの出現地点と拠点を <c>server-data/maps/&lt;マップ&gt;.points.json</c> に、
    /// 印とグレネードの調整値と持ち物を <c>server-data/tuning/ping.json</c>・<c>grenade.json</c>・<c>inventory.json</c> に書き出す。ルールのファイルには試合の設定の移動の値と体の寸法も入れる。
    /// サーバーは <c>-rules &lt;ルール名&gt;</c> でルールを選ぶ。
    /// </summary>
    /// <remarks>make export-server-rules: unity run . -- -executeMethod Fps.Editor.ServerRulesExporter.Export</remarks>
    public static class ServerRulesExporter
    {
        public const string MenuPath = "Fps/Server Data/Export Rules And Points";
        public const string LogTag = "[ServerRulesExporter]";
        public const string DataFolder = "server-data";
        public const string RulesFolder = DataFolder + "/rules";
        public const string MapsFolder = DataFolder + "/maps";
        public const string TuningFolder = DataFolder + "/tuning";

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? ".";

        /// <summary>ルールはチームデスマッチと、拠点で勝敗を決めるドミネーションのアセットから取る。</summary>
        public static IReadOnlyList<string> RulesAssetPaths()
        {
            return new[] { GameRulesAssetBuilder.TeamDeathmatchPath, GameRulesAssetBuilder.DominationPath };
        }

        public static void Export()
        {
            var root = ProjectRoot;
            var written = new List<string>();
            written.AddRange(ExportRules(Path.Combine(root, RulesFolder)));
            written.AddRange(ExportPoints(Path.Combine(root, MapsFolder)));
            written.Add(ExportPingTuning(Path.Combine(root, TuningFolder)));
            written.Add(ExportGrenadeTuning(Path.Combine(root, TuningFolder)));
            written.Add(ExportInventoryTuning(Path.Combine(root, TuningFolder)));
            Debug.Log(LogTag + " wrote " + written.Count + " file(s) under " + Path.Combine(root, DataFolder) + "; commit them");
        }

        public static IReadOnlyList<string> ExportRules(string directory)
        {
            var match = NetSceneBuilder.LoadOrBuildMatch();
            var motor = match.ToMotor();
            Directory.CreateDirectory(directory);
            var written = new List<string>();
            foreach (var path in RulesAssetPaths())
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path);
                if (asset == null)
                {
                    throw new InvalidOperationException(LogTag + " " + path + " is missing; run Fps.Editor.GameRulesAssetBuilder.Build");
                }

                var file = ToFile(match, asset, motor);
                var output = Path.Combine(directory, asset.name + NetMatchFile.Extension);
                File.WriteAllText(output, NetMatchFile.Write(file));
                written.Add(output);
            }

            return written;
        }

        public static NetMatchFile ToFile(NetMatchDefinition match, GameRulesDefinition asset, NetMotorSettings motor)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (!asset.TryToRules(out var rules, out var error))
            {
                throw new InvalidOperationException(LogTag + " invalid game rules: " + error);
            }

            if (!match.TryCreateLoadout(out _, out error))
            {
                throw new InvalidOperationException(LogTag + " " + AssetDatabase.GetAssetPath(match) + " cannot be used by the match: " + error);
            }

            var loadout = match.Loadout;
            var catalog = match.Catalog;
            return new NetMatchFile(
                asset.name,
                rules,
                match.ToSettings(),
                loadout.name,
                loadout.ToRule(),
                loadout.ToSpecs(),
                loadout.SwitchSeconds,
                catalog != null ? catalog.name : null,
                catalog != null ? catalog.ToCatalog() : null,
                motor);
        }

        public static IReadOnlyList<string> ExportPoints(string directory)
        {
            EnsureScenes();
            Directory.CreateDirectory(directory);
            var written = new List<string>();
            foreach (var scenePath in BuildSceneList.ServerScenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var spawns = new List<SpawnPoint>();
                var areas = new List<ControlPointArea>();

                // 並びは階層順にする (サーバーの SpawnSelector の同点の決め手になる)
                foreach (var rootObject in scene.GetRootGameObjects())
                {
                    foreach (var marker in rootObject.GetComponentsInChildren<NetSpawnPoint>())
                    {
                        spawns.Add(marker.ToSpawnPoint());
                    }

                    foreach (var zone in rootObject.GetComponentsInChildren<ControlPointZone>(false))
                    {
                        areas.Add(zone.Area);
                    }
                }

                var output = Path.Combine(directory, scene.name + NetMapPoints.Extension);
                File.WriteAllText(output, NetMapPoints.Write(new NetMapPoints(spawns, areas)));
                written.Add(output);
            }

            return written;
        }

        // サーバーは Resources を読めないので、オフラインと同じ値で印を動かすよう同じ JSON を写す
        public static string ExportPingTuning(string directory)
        {
            var text = Resources.Load<TextAsset>(KitTuning.PingResourcePath);
            if (text == null)
            {
                throw new InvalidOperationException(LogTag + " Resources/" + KitTuning.PingResourcePath + ".json is missing");
            }

            TuningJson.ParsePing(text.text, KitTuning.PingResourcePath);
            Directory.CreateDirectory(directory);
            var output = Path.Combine(directory, "ping.json");
            File.WriteAllText(output, text.text);
            return output;
        }

        // グレネードも同じく、オフラインと同じ値で投げて爆発させるよう同じ JSON を写す
        public static string ExportGrenadeTuning(string directory)
        {
            var text = Resources.Load<TextAsset>(KitTuning.GrenadeResourcePath);
            if (text == null)
            {
                throw new InvalidOperationException(LogTag + " Resources/" + KitTuning.GrenadeResourcePath + ".json is missing");
            }

            TuningJson.ParseGrenades(text.text, KitTuning.GrenadeResourcePath);
            Directory.CreateDirectory(directory);
            var output = Path.Combine(directory, "grenade.json");
            File.WriteAllText(output, text.text);
            return output;
        }

        // 持ち物も同じく、オフラインと同じアイテムと決め方で持たせるよう同じ JSON を写す
        public static string ExportInventoryTuning(string directory)
        {
            var text = Resources.Load<TextAsset>(KitTuning.InventoryResourcePath);
            if (text == null)
            {
                throw new InvalidOperationException(LogTag + " Resources/" + KitTuning.InventoryResourcePath + ".json is missing");
            }

            InventoryJson.Parse(text.text, KitTuning.InventoryResourcePath);
            Directory.CreateDirectory(directory);
            var output = Path.Combine(directory, "inventory.json");
            File.WriteAllText(output, text.text);
            return output;
        }

        // まだ生成していないマップがあっても書き出せるよう、無いシーンを作ってから読む
        private static void EnsureScenes()
        {
            ServerDataMaps.EnsureScenes();
        }

        [MenuItem(MenuPath)]
        private static void ExportFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Export();
            }
            finally
            {
                if (setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }
        }
    }
}
