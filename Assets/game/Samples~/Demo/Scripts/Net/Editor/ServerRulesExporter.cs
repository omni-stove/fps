using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Net;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// Unity 抜きのサーバー (dotnet/Fps.Server) はアセットとシーンを読めないので、NetMatch.prefab に焼いてある試合の設定とルールのアセットを
    /// <c>server-data/rules/&lt;ルール名&gt;.json</c> に、各マップの出現地点と拠点を <c>server-data/maps/&lt;マップ&gt;.points.json</c> に、
    /// 印の調整値を <c>server-data/tuning/ping.json</c> に書き出す。サーバーは <c>-rules &lt;ルール名&gt;</c> でルールを選ぶ。
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

        // ルールの設定のうち、シリアライズにしか無いもの
        private const string _maxShotOriginOffsetField = "_maxShotOriginOffset";

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? ".";

        /// <summary>試合の設定は NetMatch.prefab から、ルールはそれに配線したアセットと、拠点で勝敗を決めるドミネーションのアセットから取る。</summary>
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
            Debug.Log(LogTag + " wrote " + written.Count + " file(s) under " + Path.Combine(root, DataFolder) + "; commit them");
        }

        public static IReadOnlyList<string> ExportRules(string directory)
        {
            var match = LoadMatch();
            Directory.CreateDirectory(directory);
            var written = new List<string>();
            foreach (var path in RulesAssetPaths())
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path);
                if (asset == null)
                {
                    throw new InvalidOperationException(LogTag + " " + path + " is missing; run Fps.Editor.GameRulesAssetBuilder.Build");
                }

                var file = ToFile(match, asset);
                var output = Path.Combine(directory, asset.name + NetMatchFile.Extension);
                File.WriteAllText(output, NetMatchFile.Write(file));
                written.Add(output);
            }

            return written;
        }

        public static NetMatchFile ToFile(NetTeamDeathmatch match, GameRulesDefinition asset)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (!NetTeamDeathmatch.TryLoadRules(asset, out var rules, out _, out var error))
            {
                throw new InvalidOperationException(LogTag + " invalid game rules: " + error);
            }

            var loadout = match.Loadout;
            if (loadout == null)
            {
                throw new InvalidOperationException(LogTag + " " + NetSceneBuilder.MatchPrefabPath + " has no loadout; run Fps.Editor.NetSceneBuilder.Build");
            }

            var offset = new SerializedObject(match).FindProperty(_maxShotOriginOffsetField);
            var settings = new NetMatchSettings(
                match.ScoreLimit,
                match.TimeLimitSeconds,
                match.RespawnSeconds,
                match.RestartDelaySeconds,
                match.MaxHealth,
                match.MinEnemySpawnDistance,
                match.ReconnectGraceSeconds,
                offset != null ? offset.floatValue : ShotValidator.DefaultMaxOriginOffset,
                match.DominationScoreLimit,
                match.CaptureSeconds,
                match.PointsPerSecond,
                match.MaxCapturers);
            var catalog = match.Catalog;
            return new NetMatchFile(
                asset.name,
                rules,
                settings,
                loadout.name,
                loadout.ToRule(),
                loadout.ToSpecs(),
                loadout.SwitchSeconds,
                catalog != null ? catalog.name : null,
                catalog != null ? catalog.ToCatalog() : null);
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

                // 並びは NetTeamDeathmatch.CollectSpawnPoints と同じ階層順にする (SpawnSelector の同点の決め手になる)
                foreach (var rootObject in scene.GetRootGameObjects())
                {
                    foreach (var marker in rootObject.GetComponentsInChildren<NetSpawnPoint>())
                    {
                        spawns.Add(marker.ToSpawnPoint());
                    }

                    foreach (var point in rootObject.GetComponentsInChildren<NetControlPoint>(false))
                    {
                        if (point.Zone == null)
                        {
                            throw new InvalidOperationException(LogTag + " control point " + point.name + " in " + scenePath + " has no ControlPointZone");
                        }

                        areas.Add(point.Zone.Area);
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

        private static NetTeamDeathmatch LoadMatch()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetSceneBuilder.MatchPrefabPath);
            var match = prefab != null ? prefab.GetComponent<NetTeamDeathmatch>() : null;
            if (match == null)
            {
                throw new InvalidOperationException(LogTag + " " + NetSceneBuilder.MatchPrefabPath + " has no NetTeamDeathmatch; run Fps.Editor.NetSceneBuilder.Build");
            }

            return match;
        }

        // まだ生成していないマップがあっても書き出せるよう、サーバーのビルドと同じく無いシーンを作ってから読む
        private static void EnsureScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NetSceneBuilder.ScenePath) == null)
            {
                NetSceneBuilder.Build();
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaSceneBuilder.ScenePath) == null)
            {
                ArenaSceneBuilder.BuildAt(ArenaSceneBuilder.ScenePath, ArenaSceneBuilder.NavMeshDataPath);
            }
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
