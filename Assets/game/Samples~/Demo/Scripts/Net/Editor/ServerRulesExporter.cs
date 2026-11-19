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

        // サーバーの ServerRulesData.PlaylistsDirectoryName と同じ名前にする
        public const string PlaylistsFolder = DataFolder + "/playlists";

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? ".";

        public const string PingFileName = "ping.json";
        public const string GrenadeFileName = "grenade.json";
        public const string InventoryFileName = "inventory.json";
        public const string KnockbackFileName = "knockback.json";
        public const string BotsFileName = "bots.json";
        public const string AssistFileName = "assist.json";

        // 財布の額は試合の設定 (match.economy) に乗せて書き出すので、このファイルには買える品と値段の一覧だけを置く
        public const string EconomyFileName = "economy.json";

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
                var carts = new List<PayloadCart>();
                var lootContainers = new List<LootContainer>();
                var extractionZones = new List<ExtractionZone>();
                var shrinkingZones = new List<ShrinkingZone>();
                var battleRoyaleRunners = new List<BattleRoyaleRunner>();
                foreach (var rootObject in scene.GetRootGameObjects())
                {
                    carts.AddRange(rootObject.GetComponentsInChildren<PayloadCart>(false));
                    lootContainers.AddRange(rootObject.GetComponentsInChildren<LootContainer>(false));
                    extractionZones.AddRange(rootObject.GetComponentsInChildren<ExtractionZone>(false));
                    shrinkingZones.AddRange(rootObject.GetComponentsInChildren<ShrinkingZone>(false));
                    battleRoyaleRunners.AddRange(rootObject.GetComponentsInChildren<BattleRoyaleRunner>(false));
                }

                // 並びは階層順にする (サーバーの SpawnSelector の同点の決め手になる)
                foreach (var rootObject in scene.GetRootGameObjects())
                {
                    foreach (var marker in rootObject.GetComponentsInChildren<NetSpawnPoint>())
                    {
                        spawns.Add(marker.ToSpawnPoint());
                    }

                    foreach (var zone in rootObject.GetComponentsInChildren<ControlPointZone>(false))
                    {
                        // 台車と一緒に動く範囲は拠点ではないので、拠点の並びに入れない
                        if (!IsCartZone(zone, carts))
                        {
                            areas.Add(zone.Area);
                        }
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

                var payload = ToPayload(scenePath, carts, attackerSideId: FindAttackerSideId(scene));
                var output = Path.Combine(directory, scene.name + NetMapPoints.Extension);
                var points = new NetMapPoints(
                    spawns,
                    areas,
                    flagBases,
                    payload,
                    ServerDataMaps.ToLootContainers(scenePath, lootContainers),
                    ServerDataMaps.ToExtractionPoints(scenePath, extractionZones),
                    ServerDataMaps.ToZone(scenePath, shrinkingZones),
                    ServerDataMaps.ToDropHeight(scenePath, battleRoyaleRunners));
                File.WriteAllText(output, NetMapPoints.Write(points));
                written.Add(output);
            }

            return written;
        }

        // 台車の無いシーンは null。サーバーは Payload のルールで道筋の無いマップを起動のときに断る
        public static NetMapPayload ToPayload(string scenePath, IReadOnlyList<PayloadCart> carts, int attackerSideId)
        {
            if (carts == null)
            {
                throw new ArgumentNullException(nameof(carts));
            }

            if (carts.Count == 0)
            {
                return null;
            }

            // サーバーは 1 台だけを押すので、どれを書き出すかを黙って選ばない
            if (carts.Count > 1)
            {
                throw new InvalidOperationException(
                    LogTag + " " + scenePath + " has " + carts.Count + " PayloadCarts; the server pushes one, so keep one");
            }

            var cart = carts[0];
            if (!new Side(attackerSideId).TryGetTeam(out var attackers))
            {
                throw new InvalidOperationException(
                    LogTag + " the PayloadRunner in " + scenePath + " makes side " + attackerSideId + " attack; only sides 0 (Alpha) and 1 (Bravo) can push");
            }

            var points = cart.Points;
            var positions = new System.Numerics.Vector3[points.Count];
            for (var i = 0; i < positions.Length; i++)
            {
                if (points[i] == null)
                {
                    throw new InvalidOperationException(LogTag + " PayloadCart '" + cart.name + "' in " + scenePath + " route point #" + i + " is not set");
                }

                positions[i] = points[i].position.ToNumerics();
            }

            var zone = CartZone(cart);
            var radius = zone != null ? zone.Radius : ControlPointZone.DefaultRadius;
            var height = zone != null ? zone.Height : ControlPointZone.DefaultHeight;
            try
            {
                return new NetMapPayload(attackers, positions, cart.Checkpoints, radius, height);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(LogTag + " PayloadCart '" + cart.name + "' in " + scenePath + " has an invalid route: " + e.Message, e);
            }
        }

        // シーンの PayloadRunner の攻め側 (アセットがあればそちら)。Runner を置かないネット用のシーンは Runner の既定と同じ側にする
        public static int FindAttackerSideId(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                var runner = rootObject.GetComponentInChildren<PayloadRunner>(false);
                if (runner != null)
                {
                    return runner.Definition != null ? runner.Definition.AttackerSideId : runner.AttackerSideId;
                }
            }

            return PayloadRunner.DefaultAttackerSideId;
        }

        // PayloadCart.BuildRoute と同じ順 (欄、子) で範囲を探す。どちらも無ければ遊ぶときに既定の半径で足される
        private static ControlPointZone CartZone(PayloadCart cart)
        {
            return cart.Zone != null ? cart.Zone : cart.GetComponentInChildren<ControlPointZone>(true);
        }

        private static bool IsCartZone(ControlPointZone zone, IReadOnlyList<PayloadCart> carts)
        {
            foreach (var cart in carts)
            {
                if (CartZone(cart) == zone)
                {
                    return true;
                }
            }

            return false;
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
