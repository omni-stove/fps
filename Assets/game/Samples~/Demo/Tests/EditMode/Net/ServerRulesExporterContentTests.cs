using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class ServerRulesExporterContentTests
    {
        private readonly List<Object> _created = new List<Object>();
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            // プロジェクトの Resources/ShooterKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
            GameContent.Use(Create<GameContent>("Empty"));
            _directory = Path.Combine(Path.GetTempPath(), "ServerRulesExporterContentTests-" + Guid.NewGuid().ToString("N"));
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
        public void ThePingOfGameContentIsWrittenInsteadOfResources()
        {
            const string json = "{ \"lifetimeSeconds\": 12 }";
            var content = Create<GameContent>("Game");
            content.PingTuning = Text("GamePing", json);
            GameContent.Use(content);

            var output = ServerRulesExporter.ExportTuningFile(
                _directory, ServerRulesExporter.PingFileName, c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing);

            Assert.That(output, Is.EqualTo(Path.Combine(_directory, "ping.json")));
            Assert.That(File.ReadAllText(output), Is.EqualTo(json));
            Assert.That(TuningJson.ParsePing(File.ReadAllText(output), output).LifetimeSeconds, Is.EqualTo(12f), "Resources/Tuning/ping.json has 6");
        }

        [Test]
        public void WithoutGameContentThePingOfResourcesIsWritten()
        {
            var output = ServerRulesExporter.ExportTuningFile(
                _directory, ServerRulesExporter.PingFileName, c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing);

            var resources = Resources.Load<TextAsset>(KitTuning.PingResourcePath);
            Assert.That(File.ReadAllText(output), Is.EqualTo(resources.text));
            Assert.That(TuningJson.ParsePing(File.ReadAllText(output), output).LifetimeSeconds, Is.EqualTo(6f));
        }

        [Test]
        public void TheKnockbackAndBotsOfGameContentAreWrittenWithTheOtherTuning()
        {
            const string knockback = "{ \"explosionSpeed\": 11 }";
            const string bots = "{ \"sightRange\": 30 }";
            var content = Create<GameContent>("Game");
            content.KnockbackTuning = Text("GameKnockback", knockback);
            content.BotsTuning = Text("GameBots", bots);
            GameContent.Use(content);

            var written = ServerRulesExporter.ExportTuning(_directory);

            Assert.That(written, Is.EqualTo(new[]
            {
                Path.Combine(_directory, "ping.json"),
                Path.Combine(_directory, "grenade.json"),
                Path.Combine(_directory, "inventory.json"),
                Path.Combine(_directory, "knockback.json"),
                Path.Combine(_directory, "bots.json"),
            }));
            Assert.That(File.ReadAllText(Path.Combine(_directory, "knockback.json")), Is.EqualTo(knockback));
            Assert.That(File.ReadAllText(Path.Combine(_directory, "bots.json")), Is.EqualTo(bots));
            Assert.That(TuningJson.ParseKnockback(knockback, "knockback.json").Explosion.Speed, Is.EqualTo(11f));
            Assert.That(TuningJson.ParseBots(bots, "bots.json").Brain.SightRange, Is.EqualTo(30f));
        }

        [Test]
        public void WithoutGameContentTheKnockbackOfResourcesIsWritten()
        {
            ServerRulesExporter.ExportTuning(_directory);

            var resources = Resources.Load<TextAsset>(KitTuning.KnockbackResourcePath);
            Assert.That(File.ReadAllText(Path.Combine(_directory, "knockback.json")), Is.EqualTo(resources.text));
        }

        [Test]
        public void WithoutGameContentTheBotsOfResourcesIsWritten()
        {
            ServerRulesExporter.ExportTuning(_directory);

            var resources = Resources.Load<TextAsset>(KitTuning.BotsResourcePath);
            Assert.That(File.ReadAllText(Path.Combine(_directory, "bots.json")), Is.EqualTo(resources.text));
        }

        [Test]
        public void TuningInNeitherGameContentNorResourcesIsWrittenAsTheDefaults()
        {
            const string missingPath = "Tuning/ServerRulesExporterContentTests-missing";
            Assert.That(Resources.Load<TextAsset>(missingPath), Is.Null);

            var output = ServerRulesExporter.ExportTuningFile(
                _directory, ServerRulesExporter.BotsFileName, c => c.BotsTuning, missingPath, TuningJson.ParseBots);

            Assert.That(File.ReadAllText(output), Is.EqualTo("{}\n"));
            var spec = TuningJson.ParseBots(File.ReadAllText(output), output);
            Assert.That(spec.Brain.SightRange, Is.EqualTo(NetBotSpec.DefaultSightRange));
            Assert.That(spec.StuckProgress, Is.EqualTo(NetBotSpec.DefaultStuckProgress));
        }

        [Test]
        public void BrokenGameContentTuningThrowsAndKeepsTheFileWrittenBefore()
        {
            Directory.CreateDirectory(_directory);
            var output = Path.Combine(_directory, "ping.json");
            File.WriteAllText(output, "{ \"maxMarkers\": 4 }");
            var content = Create<GameContent>("Broken");
            content.PingTuning = Text("BrokenPing", "{ \"maxMarkers\": 0 }");
            GameContent.Use(content);

            var e = Assert.Throws<TuningJsonException>(() => ServerRulesExporter.ExportTuningFile(
                _directory, ServerRulesExporter.PingFileName, c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing));

            Assert.That(e.Message, Does.Contain("GameContent 'Broken' (BrokenPing)").And.Contain("maxMarkers"));
            Assert.That(File.ReadAllText(output), Is.EqualTo("{ \"maxMarkers\": 4 }"));
        }

        [Test]
        public void TheRulesOfGameContentComeFirstAndThenTheListedRules()
        {
            var game = Create<GameRulesDefinition>("Game");
            var teamDeathmatch = Create<GameRulesDefinition>("TeamDeathmatch");
            var domination = Create<GameRulesDefinition>("Domination");
            var content = Create<GameContent>("Game");
            content.Rules = game;
            GameContent.Use(content);
            var match = Create<NetMatchDefinition>("NetMatch");
            match.SetRules(new[] { teamDeathmatch, null, game, domination, teamDeathmatch });

            var rules = ServerRulesExporter.RulesToExport(match);

            Assert.That(rules, Is.EqualTo(new[] { game, teamDeathmatch, domination }));
        }

        [Test]
        public void WithoutGameContentRulesOnlyTheListedRulesAreExported()
        {
            var teamDeathmatch = Create<GameRulesDefinition>("TeamDeathmatch");
            var domination = Create<GameRulesDefinition>("Domination");
            var match = Create<NetMatchDefinition>("NetMatch");
            match.SetRules(new[] { teamDeathmatch, domination });

            Assert.That(ServerRulesExporter.RulesToExport(match), Is.EqualTo(new[] { teamDeathmatch, domination }));
        }

        [Test]
        public void ANewNetMatchListsNoRules()
        {
            var match = Create<NetMatchDefinition>("NetMatch");

            Assert.That(match.Rules, Is.Empty);
            Assert.That(ServerRulesExporter.RulesToExport(match), Is.Empty);
        }

        [Test]
        public void TheLoadoutAndCatalogOfGameContentAreChosenBeforeTheNetMatch()
        {
            var match = Create<NetMatchDefinition>("NetMatch");
            match.Loadout = Create<LoadoutDefinition>("MatchLoadout");
            match.Catalog = Create<WeaponCatalogAsset>("MatchCatalog");
            var content = Create<GameContent>("Game");
            content.Loadout = Create<LoadoutDefinition>("GameLoadout");
            content.WeaponCatalog = Create<WeaponCatalogAsset>("GameCatalog");
            GameContent.Use(content);

            Assert.That(NetMatchDefinition.PickLoadout(match), Is.SameAs(content.Loadout));
            Assert.That(NetMatchDefinition.PickCatalog(match), Is.SameAs(content.WeaponCatalog));
            Assert.That(NetMatchDefinition.PickLoadout(null), Is.SameAs(content.Loadout), "the client has no net match");
            Assert.That(NetMatchDefinition.PickCatalog(null), Is.SameAs(content.WeaponCatalog), "the client has no net match");
        }

        [Test]
        public void WithoutGameContentTheLoadoutAndCatalogOfTheNetMatchAreChosen()
        {
            var match = Create<NetMatchDefinition>("NetMatch");
            match.Loadout = Create<LoadoutDefinition>("MatchLoadout");
            match.Catalog = Create<WeaponCatalogAsset>("MatchCatalog");

            Assert.That(NetMatchDefinition.PickLoadout(match), Is.SameAs(match.Loadout));
            Assert.That(NetMatchDefinition.PickCatalog(match), Is.SameAs(match.Catalog));
            Assert.That(NetMatchDefinition.PickLoadout(null), Is.Null);
            Assert.That(NetMatchDefinition.PickCatalog(null), Is.Null);
        }

        [Test]
        public void WithoutAnyLoadoutTheNetMatchCannotCreateOne()
        {
            var match = Create<NetMatchDefinition>("NetMatch");

            Assert.That(match.TryCreateLoadout(out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Is.EqualTo("neither GameContent nor net match 'NetMatch' has a loadout"));
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
