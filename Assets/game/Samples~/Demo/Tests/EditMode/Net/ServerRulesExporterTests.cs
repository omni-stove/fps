using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class ServerRulesExporterTests
    {
        private readonly List<Object> _created = new List<Object>();
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            // プロジェクトの Resources/ShooterKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
            GameContent.Use(Create<GameContent>("Empty"));
            _directory = Path.Combine(Path.GetTempPath(), "ServerRulesExporterTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void TheBattleRoyaleExtractionAndHordeRulesAreAlwaysExported()
        {
            Assert.That(ServerRulesExporter.ModeRulesToExport, Is.EqualTo(new[]
            {
                (GameRulesAssetBuilder.BattleRoyaleName, MatchObjective.BattleRoyale),
                (GameRulesAssetBuilder.ExtractionName, MatchObjective.Extraction),
                (GameRulesAssetBuilder.HordeName, MatchObjective.Horde),
            }));
        }

        [Test]
        public void TheHordeRulesAreWrittenWithTheirObjectiveAndWaves()
        {
            var horde = Rules(GameRulesAssetBuilder.HordeName, GameRulesAssetBuilder.HordeRules());
            var match = NetSceneBuilder.LoadOrBuildMatch();

            var written = ServerRulesExporter.WriteRules(
                _directory, match, ServerRulesExporter.WithModeRules(Array.Empty<GameRulesDefinition>(), new[] { horde }));

            Assert.That(written, Is.EqualTo(new[] { Path.Combine(_directory, "Horde.json") }));
            var text = File.ReadAllText(written[0]);
            var file = NetMatchFile.Parse(text, written[0]);
            Assert.That(file.Name, Is.EqualTo("Horde"));
            Assert.That(file.Rules.Objective, Is.EqualTo(MatchObjective.Horde));
            Assert.That(text, Does.Contain("\"horde\": {"), "a Horde rules file always shows its waves and currency");
            Assert.That(file.Settings.Horde, Is.EqualTo(HordeSettings.Default));
        }

        [Test]
        public void TheTwoModeRulesAreWrittenWithTheirObjective()
        {
            var battleRoyale = Rules(GameRulesAssetBuilder.BattleRoyaleName, GameRulesAssetBuilder.BattleRoyaleRules());
            var extraction = Rules(GameRulesAssetBuilder.ExtractionName, GameRulesAssetBuilder.ExtractionRules());
            var match = NetSceneBuilder.LoadOrBuildMatch();

            var written = ServerRulesExporter.WriteRules(
                _directory, match, ServerRulesExporter.WithModeRules(Array.Empty<GameRulesDefinition>(), new[] { battleRoyale, extraction }));

            Assert.That(written, Is.EqualTo(new[]
            {
                Path.Combine(_directory, "BattleRoyale.json"),
                Path.Combine(_directory, "Extraction.json"),
            }));
            var battleRoyaleFile = NetMatchFile.Parse(File.ReadAllText(written[0]), written[0]);
            var extractionFile = NetMatchFile.Parse(File.ReadAllText(written[1]), written[1]);
            Assert.That(battleRoyaleFile.Name, Is.EqualTo("BattleRoyale"));
            Assert.That(battleRoyaleFile.Rules.Objective, Is.EqualTo(MatchObjective.BattleRoyale));
            Assert.That(battleRoyaleFile.Rules.RespawnMode, Is.EqualTo(RespawnMode.None), "only the redeploy brings a player back");
            Assert.That(battleRoyaleFile.Rules.Actions.Parachute, Is.EqualTo(Parachute.On), "players drop in by parachute");
            Assert.That(extractionFile.Name, Is.EqualTo("Extraction"));
            Assert.That(extractionFile.Rules.Objective, Is.EqualTo(MatchObjective.Extraction));
            Assert.That(extractionFile.Rules.Actions.Downed, Is.EqualTo(Downed.Off));
        }

        [Test]
        public void ModeRulesAlreadyListedAreNotWrittenTwice()
        {
            var teamDeathmatch = Create<GameRulesDefinition>("TeamDeathmatch");
            var battleRoyale = Create<GameRulesDefinition>("BattleRoyale");
            var extraction = Create<GameRulesDefinition>("Extraction");

            var combined = ServerRulesExporter.WithModeRules(new[] { teamDeathmatch, battleRoyale }, new[] { battleRoyale, null, extraction });

            Assert.That(combined, Is.EqualTo(new[] { teamDeathmatch, battleRoyale, extraction }));
        }

        [Test]
        public void ModeRulesWithAnotherObjectiveAreRefused()
        {
            var stale = Rules(GameRulesAssetBuilder.BattleRoyaleName, GameRulesAssetBuilder.TeamDeathmatchRules());

            var e = Assert.Throws<InvalidOperationException>(() => ServerRulesExporter.RequireObjective(stale, MatchObjective.BattleRoyale));

            Assert.That(e.Message, Does.Contain("has the objective None").And.Contain("plays 'BattleRoyale' as BattleRoyale"));
            Assert.DoesNotThrow(() => ServerRulesExporter.RequireObjective(
                Rules(GameRulesAssetBuilder.ExtractionName, GameRulesAssetBuilder.ExtractionRules()), MatchObjective.Extraction));
        }

        [Test]
        public void TheLootSupplyDropAndRedeployTuningOfResourcesAreWrittenWithoutGameContent()
        {
            ServerRulesExporter.ExportTuning(_directory);

            foreach (var (file, resourcePath) in new[]
            {
                ("loot.json", LootJson.ResourcePath),
                ("supply-drops.json", KitTuning.SupplyDropsResourcePath),
                ("redeploy.json", RedeployJson.ResourcePath),
            })
            {
                var resources = Resources.Load<TextAsset>(resourcePath);
                Assert.That(resources, Is.Not.Null, resourcePath);
                Assert.That(File.ReadAllText(Path.Combine(_directory, file)), Is.EqualTo(resources.text), file);
            }
        }

        [Test]
        public void TheLootOfGameContentIsWrittenInsteadOfResources()
        {
            const string loot = "{ \"tables\": [ { \"id\": \"crates\", \"rolls\": 1, \"entries\": [ { \"itemId\": \"3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13\", \"weight\": 1 } ] } ] }";
            const string redeploy = "{ \"charges\": 3 }";
            var content = Create<GameContent>("Game");
            content.LootTuning = Text("GameLoot", loot);
            content.RedeployTuning = Text("GameRedeploy", redeploy);
            GameContent.Use(content);

            ServerRulesExporter.ExportTuning(_directory);

            Assert.That(File.ReadAllText(Path.Combine(_directory, "loot.json")), Is.EqualTo(loot));
            Assert.That(LootJson.Parse(loot, "loot.json").Find("crates"), Is.Not.Null);
            Assert.That(File.ReadAllText(Path.Combine(_directory, "redeploy.json")), Is.EqualTo(redeploy));
            Assert.That(RedeployJson.Parse(redeploy, "redeploy.json").Charges, Is.EqualTo(3));
        }

        private GameRulesDefinition Rules(string assetName, GameRules rules)
        {
            var asset = Create<GameRulesDefinition>(assetName);
            GameRulesAssetBuilder.Configure(asset, rules);
            return asset;
        }

        private T Create<T>(string assetName)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            _created.Add(asset);
            return asset;
        }

        private TextAsset Text(string assetName, string text)
        {
            var asset = new TextAsset(text) { name = assetName };
            _created.Add(asset);
            return asset;
        }
    }
}
