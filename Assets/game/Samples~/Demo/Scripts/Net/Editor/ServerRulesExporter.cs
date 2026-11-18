using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // Unity 抜きのサーバー (dotnet/Shooter.Server) はアセットとシーンを読めないので、試合の設定・ルール・出現地点・調整値を JSON に書き出して渡す。
    // 調整値をオフラインと揃えるため、KitTuning と同じ順 (入口の欄、無ければ Resources) で読む。
    public static class ServerRulesExporter
    {
        public const string MenuPath = "Shooter/Server Data/Export Rules And Points";
        public const string LogTag = "[ServerRulesExporter]";
        public const string DataFolder = "server-data";
        public const string RulesFolder = DataFolder + "/rules";
        public const string MapsFolder = DataFolder + "/maps";
        public const string TuningFolder = DataFolder + "/tuning";

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? ".";

        public const string PingFileName = "ping.json";
        public const string GrenadeFileName = "grenade.json";
        public const string InventoryFileName = "inventory.json";
        public const string KnockbackFileName = "knockback.json";
        public const string BotsFileName = "bots.json";
        public const string AssistFileName = "assist.json";

        public const string AllDefaultsJson = "{}\n";

        public static IReadOnlyList<GameRulesDefinition> RulesToExport(NetMatchDefinition match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            var rules = new List<GameRulesDefinition>();
            void Add(GameRulesDefinition candidate)
            {
                if (candidate != null && !rules.Contains(candidate))
                {
                    rules.Add(candidate);
                }
            }

            Add(GameContent.Pick(c => c.Rules));
            foreach (var candidate in match.Rules)
            {
                Add(candidate);
            }

            return rules;
        }

        public static void Export()
        {
            var root = ProjectRoot;
            var written = new List<string>();
            written.AddRange(ExportRules(Path.Combine(root, RulesFolder)));
            written.AddRange(ExportPoints(Path.Combine(root, MapsFolder)));
            written.AddRange(ExportTuning(Path.Combine(root, TuningFolder)));
            Debug.Log(LogTag + " wrote " + written.Count + " file(s) under " + Path.Combine(root, DataFolder) + "; commit them");
        }

        public static IReadOnlyList<string> ExportRules(string directory)
        {
            var match = NetSceneBuilder.LoadOrBuildMatch();
            var motor = match.ToMotor();
            Directory.CreateDirectory(directory);
            var rules = RulesToExport(match);
            if (rules.Count == 0)
            {
                throw new InvalidOperationException(
                    LogTag + " no rules to export; set Rules in GameContent or in " + AssetDatabase.GetAssetPath(match));
            }

            var written = new List<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in rules)
            {
                // ファイル名がルールの名前なので、同じ名前の 2 つ目は 1 つ目を上書きしてしまう
                if (!names.Add(asset.name))
                {
                    throw new InvalidOperationException(LogTag + " two rules are named " + asset.name + "; rename one of " + AssetDatabase.GetAssetPath(asset));
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

            NetMatchSettings settings;
            try
            {
                settings = match.ToSettings();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is WeaponDefinitionException)
            {
                throw new InvalidOperationException(
                    LogTag + " " + AssetDatabase.GetAssetPath(match) + " has match settings the server cannot use: " + e.Message, e);
            }

            var loadout = NetMatchDefinition.PickLoadout(match);
            var catalog = NetMatchDefinition.PickCatalog(match);
            try
            {
                return new NetMatchFile(
                    asset.name,
                    rules,
                    settings,
                    loadout.name,
                    loadout.ToRule(),
                    loadout.ToSpecs(),
                    loadout.SwitchSeconds,
                    catalog != null ? catalog.name : null,
                    catalog != null ? catalog.ToCatalog() : null,
                    motor);
            }
            catch (ArgumentException e) when (e.ParamName == "settings")
            {
                throw new InvalidOperationException(
                    LogTag + " rules " + AssetDatabase.GetAssetPath(asset) + " cannot be played with " + AssetDatabase.GetAssetPath(match) + ": " + e.Message, e);
            }
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
                var flagBases = new List<FlagBasePoint>();

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

                    foreach (var flagBase in rootObject.GetComponentsInChildren<FlagBase>(false))
                    {
                        if (!flagBase.Side.TryGetTeam(out var team))
                        {
                            throw new InvalidOperationException(
                                LogTag + " FlagBase '" + flagBase.name + "' in " + scenePath + " belongs to side " + flagBase.Side.Id
                                + "; only sides 0 (Alpha) and 1 (Bravo) have flags");
                        }

                        flagBases.Add(new FlagBasePoint(team, flagBase.Position.ToNumerics()));
                    }
                }

                var output = Path.Combine(directory, scene.name + NetMapPoints.Extension);
                File.WriteAllText(output, NetMapPoints.Write(new NetMapPoints(spawns, areas, flagBases)));
                written.Add(output);
            }

            return written;
        }

        // サーバーは Resources も入口も読めないので、オフラインと同じ値で動くよう KitTuning が読むのと同じ JSON を写す
        public static IReadOnlyList<string> ExportTuning(string directory)
        {
            return new[]
            {
                ExportTuningFile(directory, PingFileName, c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing),
                ExportTuningFile(directory, GrenadeFileName, c => c.GrenadeTuning, KitTuning.GrenadeResourcePath, TuningJson.ParseGrenades),
                ExportTuningFile(directory, InventoryFileName, c => c.InventoryTuning, KitTuning.InventoryResourcePath, InventoryJson.Parse),
                ExportTuningFile(directory, KnockbackFileName, c => c.KnockbackTuning, KitTuning.KnockbackResourcePath, TuningJson.ParseKnockback),
                ExportTuningFile(directory, BotsFileName, c => c.BotsTuning, KitTuning.BotsResourcePath, TuningJson.ParseBots),
                ExportTuningFile(directory, AssistFileName, c => c.AssistTuning, KitTuning.AssistResourcePath, AssistJson.Parse),
            };
        }

        public static string ExportTuningFile<T>(
            string directory,
            string fileName,
            Func<GameContent, TextAsset> select,
            string resourcePath,
            Func<string, string, T> parse)
        {
            if (parse == null)
            {
                throw new ArgumentNullException(nameof(parse));
            }

            var text = KitTuning.FindText(select, resourcePath, out var source);
            if (text == null)
            {
                // ファイルを消さずに項目を省いた JSON を置くのは、どの調整値もサーバーへ渡していることを server-data で見せるため (省いた項目は KitTuning が無いときに使うのと同じ既定値になる)
                text = AllDefaultsJson;
                source += " (missing; every item left out, so the defaults KitTuning uses)";
            }

            parse(text, source);
            var output = Path.Combine(directory, fileName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(output, text);
            Debug.Log(LogTag + " wrote " + output + " from " + source);
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
