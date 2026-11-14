using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GameRulesJsonTests
    {
        private const string _source = "Rules/Test.json";

        [Test]
        public void EmptyObjectIsTheDefault()
        {
            AssertSameRules(GameRulesJson.Parse("{}", _source), GameRules.Default);
        }

        [Test]
        public void OmittedItemsKeepTheDefault()
        {
            var rules = GameRulesJson.Parse("{\"regenPerSecond\": 12.5, \"deathCamera\": \"Killer\"}", _source);

            AssertSameRules(rules, GameRules.Default.WithRegenPerSecond(12.5f).WithDeathCamera(DeathCamera.Killer));
        }

        [Test]
        public void OmittedActionItemsKeepTheDefaultActions()
        {
            var rules = GameRulesJson.Parse("{\"actions\": {\"grenade\": \"On\", \"grenadeCount\": 4}}", _source);

            AssertSameRules(rules, GameRules.Default.WithActions(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(4)));
        }

        [Test]
        public void ReadsEveryItem()
        {
            const string json = @"{
  ""friendlyFire"": ""Reflect"",
  ""respawnMode"": ""Wave"",
  ""waveIntervalSeconds"": 25,
  ""regenDelaySeconds"": 3.5,
  ""regenPerSecond"": 18,
  ""deathBody"": ""Collapse"",
  ""deathCamera"": ""Free"",
  ""infiniteReserve"": true,
  ""reserveAmmoMultiplier"": 0.6,
  ""destruction"": ""On"",
  ""actions"": {
    ""downed"": ""Revive"",
    ""downedBleedoutSeconds"": 12,
    ""downedHealthFraction"": 0.25,
    ""reviveSeconds"": 2.5,
    ""cover"": ""On"",
    ""blindFire"": ""On"",
    ""activeReload"": ""On"",
    ""dodge"": ""On"",
    ""vault"": ""On"",
    ""grenade"": ""On"",
    ""grenadeCount"": 3,
    ""melee"": ""On"",
    ""stagger"": ""On"",
    ""slide"": ""On"",
    ""mantle"": ""On"",
    ""prone"": ""On"",
    ""ladder"": ""On"",
    ""zipline"": ""On"",
    ""parachute"": ""On"",
    ""stealth"": ""On"",
    ""suppression"": ""On"",
    ""ping"": ""On"",
    ""abilities"": ""On"",
    ""slowMotion"": ""On""
  },
  ""spawnProtectionSeconds"": 1.5,
  ""fallDamage"": ""On"",
  ""hudDensity"": ""Hardcore"",
  ""objective"": ""Domination"",
  ""minPlayers"": 8,
  ""idleKickSeconds"": 90
}";
            var expected = new GameRules(
                FriendlyFire.Reflect,
                RespawnMode.Wave,
                25f,
                3.5f,
                18f,
                DeathBody.Collapse,
                DeathCamera.Free,
                true,
                0.6f,
                Destruction.On,
                AllOnActions(),
                1.5f,
                FallDamage.On,
                HudDensity.Hardcore,
                MatchObjective.Domination,
                8,
                90f);

            AssertSameRules(GameRulesJson.Parse(json, _source), expected);
        }

        [Test]
        public void ItemNamesMatchTheRulesConstructors()
        {
            Assert.That(GameRulesJson.ItemNames.Count, Is.EqualTo(17));
            Assert.That(GameRulesJson.ActionItemNames.Count, Is.EqualTo(24));
            Assert.That(GameRulesJson.ItemNames, Does.Contain(GameRulesJson.ActionsItem));
        }

        [Test]
        public void OmittedNewActionItemsAreOff()
        {
            var actions = GameRulesJson.Parse("{\"actions\": {\"cover\": \"On\"}}", _source).Actions;

            Assert.That(actions.Melee, Is.EqualTo(Melee.Off));
            Assert.That(actions.Stagger, Is.EqualTo(Stagger.Off));
            Assert.That(actions.Slide, Is.EqualTo(Slide.Off));
            Assert.That(actions.Mantle, Is.EqualTo(Mantle.Off));
            Assert.That(actions.Prone, Is.EqualTo(Prone.Off));
            Assert.That(actions.Ladder, Is.EqualTo(Ladder.Off));
            Assert.That(actions.Zipline, Is.EqualTo(Zipline.Off));
            Assert.That(actions.Parachute, Is.EqualTo(Parachute.Off));
            Assert.That(actions.Stealth, Is.EqualTo(Stealth.Off));
            Assert.That(actions.Suppression, Is.EqualTo(Suppression.Off));
            Assert.That(actions.Ping, Is.EqualTo(Ping.Off));
            Assert.That(actions.Abilities, Is.EqualTo(Abilities.Off));
            Assert.That(actions.SlowMotion, Is.EqualTo(SlowMotion.Off));
        }

        [TestCase("melee")]
        [TestCase("stagger")]
        [TestCase("slide")]
        [TestCase("mantle")]
        [TestCase("prone")]
        [TestCase("ladder")]
        [TestCase("zipline")]
        [TestCase("parachute")]
        [TestCase("stealth")]
        [TestCase("suppression")]
        [TestCase("ping")]
        [TestCase("abilities")]
        [TestCase("slowMotion")]
        public void EachNewActionItemIsReadAndRejectsUnknownNames(string item)
        {
            var on = GameRulesJson.Parse("{\"actions\": {\"" + item + "\": \"On\"}}", _source);
            var written = GameRulesJson.Write(on);

            Assert.That(written, Does.Contain("\"" + item + "\": \"On\""));
            Assert.That(GameRulesJson.Write(GameRules.Default), Does.Contain("\"" + item + "\": \"Off\""));

            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"actions\": {\"" + item + "\": \"Maybe\"}}", _source));
            AssertNames(e, "actions." + item);
        }

        [Test]
        public void WriteThenParseGivesTheSameDefaultRules()
        {
            AssertSameRules(GameRulesJson.Parse(GameRulesJson.Write(GameRules.Default), _source), GameRules.Default);
        }

        [Test]
        public void WriteThenParseGivesTheSameRulesForEveryItem()
        {
            var rules = new GameRules(
                FriendlyFire.Reflect,
                RespawnMode.Wave,
                25f,
                3.5f,
                18f,
                DeathBody.Collapse,
                DeathCamera.Free,
                true,
                0.6f,
                Destruction.On,
                AllOnActions(),
                1.5f,
                FallDamage.On,
                HudDensity.Hardcore,
                MatchObjective.Domination,
                8,
                0.1f);

            var written = GameRulesJson.Write(rules);

            AssertSameRules(GameRulesJson.Parse(written, _source), rules);
            Assert.That(GameRulesJson.Write(GameRulesJson.Parse(written, _source)), Is.EqualTo(written), "writing is stable");
        }

        [Test]
        public void WriteNamesEveryItem()
        {
            var written = GameRulesJson.Write(GameRules.Default);

            foreach (var item in GameRulesJson.ItemNames)
            {
                Assert.That(written, Does.Contain("\"" + item + "\": "), item);
            }

            foreach (var item in GameRulesJson.ActionItemNames)
            {
                Assert.That(written, Does.Contain("\"" + item + "\": "), "actions." + item);
            }
        }

        [Test]
        public void WriteRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => GameRulesJson.Write(null));
        }

        private static ActionRules AllOnActions()
        {
            return new ActionRules(
                Downed.Revive,
                12f,
                0.25f,
                2.5f,
                Cover.On,
                BlindFire.On,
                ActiveReload.On,
                Dodge.On,
                Vault.On,
                Grenade.On,
                3,
                Melee.On,
                Stagger.On,
                Slide.On,
                Mantle.On,
                Prone.On,
                Ladder.On,
                Zipline.On,
                Parachute.On,
                Stealth.On,
                Suppression.On,
                Ping.On,
                Abilities.On,
                SlowMotion.On);
        }

        [Test]
        public void ReadsFloatsExactlyAsCSharpLiterals()
        {
            var rules = GameRulesJson.Parse("{\"reserveAmmoMultiplier\": 0.6, \"actions\": {\"downedHealthFraction\": 3e-1}}", _source);

            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(0.6f));
            Assert.That(rules.Actions.DownedHealthFraction, Is.EqualTo(0.3f));
        }

        [Test]
        public void SkipsTheByteOrderMark()
        {
            var rules = GameRulesJson.Parse((char)0xFEFF + "{\"minPlayers\": 4}", _source);

            Assert.That(rules.MinPlayers, Is.EqualTo(4));
        }

        [Test]
        public void UnknownItemIsRejectedWithItsName()
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"regenPerSecnd\": 1}", _source));

            AssertNames(e, "regenPerSecnd");
        }

        [Test]
        public void UnknownActionItemIsRejectedWithItsPath()
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"actions\": {\"sprint\": \"On\"}}", _source));

            AssertNames(e, "actions.sprint");
        }

        [Test]
        public void ItemNamesAreCaseSensitive()
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"FriendlyFire\": \"On\"}", _source));

            AssertNames(e, "FriendlyFire");
        }

        [Test]
        public void ActionItemAtTheTopLevelIsUnknown()
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"grenadeCount\": 2}", _source));

            AssertNames(e, "grenadeCount");
        }

        [TestCase("{\"waveIntervalSeconds\": 0}", "waveIntervalSeconds")]
        [TestCase("{\"regenDelaySeconds\": -1}", "regenDelaySeconds")]
        [TestCase("{\"reserveAmmoMultiplier\": -0.5}", "reserveAmmoMultiplier")]
        [TestCase("{\"minPlayers\": 65}", "minPlayers")]
        [TestCase("{\"idleKickSeconds\": 1e39}", "idleKickSeconds")]
        [TestCase("{\"actions\": {\"downedHealthFraction\": 1.5}}", "actions.downedHealthFraction")]
        [TestCase("{\"actions\": {\"reviveSeconds\": 0}}", "actions.reviveSeconds")]
        [TestCase("{\"actions\": {\"grenadeCount\": -1}}", "actions.grenadeCount")]
        [TestCase("{\"actions\": {\"blindFire\": \"On\"}}", "actions.blindFire")]
        public void OutOfRangeValueIsRejectedByTheRulesWithItsName(string json, string item)
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse(json, _source));

            AssertNames(e, item);
        }

        [TestCase("{\"friendlyFire\": \"Sometimes\"}", "friendlyFire")]
        [TestCase("{\"friendlyFire\": \"off\"}", "friendlyFire")]
        [TestCase("{\"friendlyFire\": \"1\"}", "friendlyFire")]
        [TestCase("{\"friendlyFire\": 1}", "friendlyFire")]
        [TestCase("{\"regenPerSecond\": \"20\"}", "regenPerSecond")]
        [TestCase("{\"infiniteReserve\": 1}", "infiniteReserve")]
        [TestCase("{\"infiniteReserve\": \"true\"}", "infiniteReserve")]
        [TestCase("{\"minPlayers\": 2.5}", "minPlayers")]
        [TestCase("{\"minPlayers\": 1e1}", "minPlayers")]
        [TestCase("{\"minPlayers\": 99999999999}", "minPlayers")]
        [TestCase("{\"actions\": \"On\"}", "actions")]
        [TestCase("{\"actions\": [1]}", "actions")]
        [TestCase("{\"deathBody\": null}", "deathBody")]
        [TestCase("{\"actions\": null}", "actions")]
        public void WrongTypeIsRejectedWithItsName(string json, string item)
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse(json, _source));

            AssertNames(e, item);
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"minPlayers\": 1,}")]
        [TestCase("{\"minPlayers\": 01}")]
        [TestCase("{\"minPlayers\": 1} {}")]
        [TestCase("{\"minPlayers\": 1, \"minPlayers\": 2}")]
        [TestCase("{\"minPlayers\": +1}")]
        [TestCase("{\"minPlayers\": 1.}")]
        [TestCase("// comment\n{}")]
        [TestCase("{\"bad\\x\": 1}")]
        public void MalformedJsonIsRejectedWithTheSourceName(string json)
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse(json, _source));

            Assert.That(e.SourceName, Is.EqualTo(_source));
            Assert.That(e.Item, Is.Null);
            Assert.That(e.Message, Does.Contain(_source));
        }

        [Test]
        public void SyntaxErrorReportsTheLineAndColumn()
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\n  \"minPlayers\": 1\n  \"objective\": \"None\"\n}", _source));

            Assert.That(e.Message, Does.Contain("line 3, column 3"));
        }

        [TestCase("[]")]
        [TestCase("\"rules\"")]
        [TestCase("null")]
        public void TopLevelMustBeAnObject(string json)
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse(json, _source));

            Assert.That(e.Message, Does.Contain(_source));
            Assert.That(e.Message, Does.Contain("must be an object"));
        }

        [Test]
        public void NullTextIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => GameRulesJson.Parse(null, _source));
        }

        private static void AssertNames(GameRulesJsonException e, string item)
        {
            Assert.That(e.SourceName, Is.EqualTo(_source));
            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain(_source));
            Assert.That(e.Message, Does.Contain("'" + item + "'"));
        }

        private static void AssertSameRules(GameRules actual, GameRules expected)
        {
            Assert.That(actual.FriendlyFire, Is.EqualTo(expected.FriendlyFire), "friendlyFire");
            Assert.That(actual.RespawnMode, Is.EqualTo(expected.RespawnMode), "respawnMode");
            Assert.That(actual.WaveIntervalSeconds, Is.EqualTo(expected.WaveIntervalSeconds), "waveIntervalSeconds");
            Assert.That(actual.RegenDelaySeconds, Is.EqualTo(expected.RegenDelaySeconds), "regenDelaySeconds");
            Assert.That(actual.RegenPerSecond, Is.EqualTo(expected.RegenPerSecond), "regenPerSecond");
            Assert.That(actual.DeathBody, Is.EqualTo(expected.DeathBody), "deathBody");
            Assert.That(actual.DeathCamera, Is.EqualTo(expected.DeathCamera), "deathCamera");
            Assert.That(actual.InfiniteReserve, Is.EqualTo(expected.InfiniteReserve), "infiniteReserve");
            Assert.That(actual.ReserveAmmoMultiplier, Is.EqualTo(expected.ReserveAmmoMultiplier), "reserveAmmoMultiplier");
            Assert.That(actual.Destruction, Is.EqualTo(expected.Destruction), "destruction");
            Assert.That(actual.SpawnProtectionSeconds, Is.EqualTo(expected.SpawnProtectionSeconds), "spawnProtectionSeconds");
            Assert.That(actual.FallDamage, Is.EqualTo(expected.FallDamage), "fallDamage");
            Assert.That(actual.HudDensity, Is.EqualTo(expected.HudDensity), "hudDensity");
            Assert.That(actual.Objective, Is.EqualTo(expected.Objective), "objective");
            Assert.That(actual.MinPlayers, Is.EqualTo(expected.MinPlayers), "minPlayers");
            Assert.That(actual.IdleKickSeconds, Is.EqualTo(expected.IdleKickSeconds), "idleKickSeconds");

            var a = actual.Actions;
            var b = expected.Actions;
            Assert.That(a.Downed, Is.EqualTo(b.Downed), "actions.downed");
            Assert.That(a.DownedBleedoutSeconds, Is.EqualTo(b.DownedBleedoutSeconds), "actions.downedBleedoutSeconds");
            Assert.That(a.DownedHealthFraction, Is.EqualTo(b.DownedHealthFraction), "actions.downedHealthFraction");
            Assert.That(a.ReviveSeconds, Is.EqualTo(b.ReviveSeconds), "actions.reviveSeconds");
            Assert.That(a.Cover, Is.EqualTo(b.Cover), "actions.cover");
            Assert.That(a.BlindFire, Is.EqualTo(b.BlindFire), "actions.blindFire");
            Assert.That(a.ActiveReload, Is.EqualTo(b.ActiveReload), "actions.activeReload");
            Assert.That(a.Dodge, Is.EqualTo(b.Dodge), "actions.dodge");
            Assert.That(a.Vault, Is.EqualTo(b.Vault), "actions.vault");
            Assert.That(a.Grenade, Is.EqualTo(b.Grenade), "actions.grenade");
            Assert.That(a.GrenadeCount, Is.EqualTo(b.GrenadeCount), "actions.grenadeCount");
            Assert.That(a.Melee, Is.EqualTo(b.Melee), "actions.melee");
            Assert.That(a.Stagger, Is.EqualTo(b.Stagger), "actions.stagger");
            Assert.That(a.Slide, Is.EqualTo(b.Slide), "actions.slide");
            Assert.That(a.Mantle, Is.EqualTo(b.Mantle), "actions.mantle");
            Assert.That(a.Prone, Is.EqualTo(b.Prone), "actions.prone");
            Assert.That(a.Ladder, Is.EqualTo(b.Ladder), "actions.ladder");
            Assert.That(a.Zipline, Is.EqualTo(b.Zipline), "actions.zipline");
            Assert.That(a.Parachute, Is.EqualTo(b.Parachute), "actions.parachute");
            Assert.That(a.Stealth, Is.EqualTo(b.Stealth), "actions.stealth");
            Assert.That(a.Suppression, Is.EqualTo(b.Suppression), "actions.suppression");
            Assert.That(a.Ping, Is.EqualTo(b.Ping), "actions.ping");
            Assert.That(a.Abilities, Is.EqualTo(b.Abilities), "actions.abilities");
            Assert.That(a.SlowMotion, Is.EqualTo(b.SlowMotion), "actions.slowMotion");
        }
    }
}
