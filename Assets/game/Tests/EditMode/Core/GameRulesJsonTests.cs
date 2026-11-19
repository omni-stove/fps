using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
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
    ""slowMotion"": ""On"",
    ""aimToFire"": ""On"",
    ""jump"": ""Off"",
    ""emote"": ""On"",
    ""stamina"": ""On""
  },
  ""spawnProtectionSeconds"": 1.5,
  ""fallDamage"": ""On"",
  ""hudDensity"": ""Hardcore"",
  ""objective"": ""Domination"",
  ""minPlayers"": 8,
  ""idleKickSeconds"": 90,
  ""idleWarningSeconds"": 30,
  ""maxWeapons"": 2,
  ""reloadMode"": ""Manual"",
  ""viewMode"": ""ThirdPerson"",
  ""respawnAnchor"": [""Beacon"", ""Squad"", ""Base""]
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
                90f,
                2,
                ReloadMode.Manual,
                ViewMode.ThirdPerson,
                respawnAnchor: _everyAnchor,
                idleWarningSeconds: 30f);

            AssertSameRules(GameRulesJson.Parse(json, _source), expected);
        }

        [Test]
        public void OmittedViewModeIsFirstPerson()
        {
            Assert.That(GameRulesJson.Parse("{}", _source).ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(GameRulesJson.Parse("{\"viewMode\": \"ThirdPerson\"}", _source).ViewMode, Is.EqualTo(ViewMode.ThirdPerson));
        }

        [Test]
        public void OmittedStreaksAreOnAndWrittenStreaksAreRead()
        {
            Assert.That(GameRulesJson.Parse("{}", _source).Streaks, Is.EqualTo(Streaks.On));
            Assert.That(GameRulesJson.Parse("{\"streaks\": \"Off\"}", _source).Streaks, Is.EqualTo(Streaks.Off));
            Assert.That(GameRulesJson.Parse("{}", _source, GameRules.Default.WithStreaks(Streaks.Off)).Streaks, Is.EqualTo(Streaks.Off), "omitted keeps the base");

            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"streaks\": \"Sometimes\"}", _source));
            AssertNames(e, "streaks");
        }

        [Test]
        public void WriteThenParseKeepsStreaksOff()
        {
            var rules = GameRules.Default.WithStreaks(Streaks.Off);

            var json = GameRulesJson.Write(rules);

            Assert.That(json, Does.Contain("\"streaks\": \"Off\""));
            Assert.That(GameRulesJson.Parse(json, _source).Streaks, Is.EqualTo(Streaks.Off));
        }

        [Test]
        public void OmittedTextChatAndSpectatingAreOnAndWrittenOffIsRead()
        {
            var omitted = GameRulesJson.Parse("{}", _source);
            Assert.That(omitted.TextChat, Is.EqualTo(TextChat.On));
            Assert.That(omitted.Spectating, Is.EqualTo(Spectating.On));

            var off = GameRulesJson.Parse("{\"textChat\": \"Off\", \"spectating\": \"Off\"}", _source);
            Assert.That(off.TextChat, Is.EqualTo(TextChat.Off));
            Assert.That(off.Spectating, Is.EqualTo(Spectating.Off));

            var overBase = GameRulesJson.Parse("{}", _source, GameRules.Default.WithTextChat(TextChat.Off).WithSpectating(Spectating.Off));
            Assert.That(overBase.TextChat, Is.EqualTo(TextChat.Off), "omitted keeps the base");
            Assert.That(overBase.Spectating, Is.EqualTo(Spectating.Off), "omitted keeps the base");

            AssertNames(Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"textChat\": \"Muted\"}", _source)), "textChat");
            AssertNames(Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"spectating\": \"Muted\"}", _source)), "spectating");
        }

        [Test]
        public void WriteThenParseKeepsTextChatAndSpectating()
        {
            var defaults = GameRulesJson.Write(GameRules.Default);
            Assert.That(defaults, Does.Contain("\"textChat\": \"On\""));
            Assert.That(defaults, Does.Contain("\"spectating\": \"On\""));

            var parsedDefaults = GameRulesJson.Parse(defaults, _source);
            Assert.That(parsedDefaults.TextChat, Is.EqualTo(TextChat.On));
            Assert.That(parsedDefaults.Spectating, Is.EqualTo(Spectating.On));

            var written = GameRulesJson.Write(GameRules.Default.WithTextChat(TextChat.Off).WithSpectating(Spectating.Off));
            Assert.That(written, Does.Contain("\"textChat\": \"Off\""));
            Assert.That(written, Does.Contain("\"spectating\": \"Off\""));

            var parsed = GameRulesJson.Parse(written, _source);
            Assert.That(parsed.TextChat, Is.EqualTo(TextChat.Off));
            Assert.That(parsed.Spectating, Is.EqualTo(Spectating.Off));
        }

        [Test]
        public void OmittedEmoteStaminaAndRespawnAnchorKeepTheDefaults()
        {
            var omitted = GameRulesJson.Parse("{\"actions\": {\"cover\": \"On\"}}", _source);

            Assert.That(omitted.Actions.Emote, Is.EqualTo(Emote.Off));
            Assert.That(omitted.Actions.Stamina, Is.EqualTo(Stamina.Off));
            Assert.That(omitted.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Base }));
            Assert.That(GameRules.Default.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Base }));

            var baseRules = GameRules.Default
                .WithActions(ActionRules.Default.WithEmote(Emote.On).WithStamina(Stamina.On))
                .WithRespawnAnchors(new[] { RespawnAnchor.Squad, RespawnAnchor.Base });
            var overBase = GameRulesJson.Parse("{}", _source, baseRules);
            Assert.That(overBase.Actions.Emote, Is.EqualTo(Emote.On), "omitted keeps the base");
            Assert.That(overBase.Actions.Stamina, Is.EqualTo(Stamina.On), "omitted keeps the base");
            Assert.That(overBase.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Squad, RespawnAnchor.Base }), "omitted keeps the base");
        }

        [Test]
        public void WrittenEmoteStaminaAndRespawnAnchorAreRead()
        {
            var rules = GameRulesJson.Parse(
                "{\"actions\": {\"emote\": \"On\", \"stamina\": \"On\"}, \"respawnAnchor\": [\"Squad\", \"Beacon\", \"Base\"]}", _source);

            Assert.That(rules.Actions.Emote, Is.EqualTo(Emote.On));
            Assert.That(rules.Actions.Stamina, Is.EqualTo(Stamina.On));
            Assert.That(rules.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Squad, RespawnAnchor.Beacon, RespawnAnchor.Base }), "kept in the written order");
            Assert.That(rules.Actions.Cover, Is.EqualTo(Cover.Off), "the other actions keep their defaults");
        }

        [Test]
        public void WriteThenParseKeepsEmoteStaminaAndRespawnAnchor()
        {
            var defaults = GameRulesJson.Write(GameRules.Default);
            Assert.That(defaults, Does.Contain("\"emote\": \"Off\""));
            Assert.That(defaults, Does.Contain("\"stamina\": \"Off\""));
            Assert.That(defaults, Does.Contain("\"respawnAnchor\": [\"Base\"]"));

            var rules = GameRules.Default
                .WithActions(ActionRules.Default.WithEmote(Emote.On).WithStamina(Stamina.On))
                .WithRespawnAnchors(new[] { RespawnAnchor.Beacon, RespawnAnchor.Base });
            var written = GameRulesJson.Write(rules);
            Assert.That(written, Does.Contain("\"emote\": \"On\""));
            Assert.That(written, Does.Contain("\"stamina\": \"On\""));
            Assert.That(written, Does.Contain("\"respawnAnchor\": [\"Beacon\", \"Base\"]"));

            AssertSameRules(GameRulesJson.Parse(written, _source), rules);
        }

        [TestCase("{\"actions\": {\"emote\": \"Maybe\"}}", "actions.emote")]
        [TestCase("{\"actions\": {\"stamina\": \"Maybe\"}}", "actions.stamina")]
        [TestCase("{\"actions\": {\"emote\": true}}", "actions.emote")]
        [TestCase("{\"emote\": \"On\"}", "emote")]
        [TestCase("{\"respawnAnchor\": \"Base\"}", "respawnAnchor")]
        [TestCase("{\"respawnAnchor\": [\"Home\"]}", "respawnAnchor")]
        [TestCase("{\"respawnAnchor\": [\"base\"]}", "respawnAnchor")]
        [TestCase("{\"respawnAnchor\": [0]}", "respawnAnchor")]
        [TestCase("{\"respawnAnchor\": []}", "respawnAnchor")]
        [TestCase("{\"respawnAnchor\": [\"Base\", \"Base\"]}", "respawnAnchor")]
        [TestCase("{\"respawnAnchor\": null}", "respawnAnchor")]
        public void BadEmoteStaminaOrRespawnAnchorIsRejectedWithItsName(string json, string item)
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse(json, _source));

            AssertNames(e, item);
        }

        [Test]
        public void TheReadLogNamesEmoteStaminaAndTheAnchorsInOrder()
        {
            var rules = GameRulesJson.Parse("{\"actions\": {\"emote\": \"On\"}, \"respawnAnchor\": [\"Squad\", \"Base\"]}", _source);

            Assert.That(
                GameRulesLog.Read(_source, rules),
                Is.EqualTo("[GameRules] read 'Rules/Test.json' emote=On stamina=Off respawnAnchor=Squad,Base reloadInterruptedBy=Melee,Grenade idleWarningSeconds=15"));
            Assert.That(
                GameRulesLog.Read(null, GameRules.Default),
                Is.EqualTo("[GameRules] read '-' emote=Off stamina=Off respawnAnchor=Base reloadInterruptedBy=Melee,Grenade idleWarningSeconds=15"));
            Assert.That(
                GameRulesLog.Read(_source, GameRules.Default.WithIdleWarningSeconds(22.5f)),
                Is.EqualTo("[GameRules] read 'Rules/Test.json' emote=Off stamina=Off respawnAnchor=Base reloadInterruptedBy=Melee,Grenade idleWarningSeconds=22.5"));
            Assert.Throws<ArgumentNullException>(() => GameRulesLog.Read(_source, null));
        }

        [Test]
        public void OmittedIdleWarningSecondsIsFifteenAndWrittenSecondsAreRead()
        {
            Assert.That(GameRules.DefaultIdleWarningSeconds, Is.EqualTo(15f));
            Assert.That(GameRulesJson.Parse("{}", _source).IdleWarningSeconds, Is.EqualTo(15f));
            Assert.That(GameRulesJson.Parse("{\"idleWarningSeconds\": 30}", _source).IdleWarningSeconds, Is.EqualTo(30f));
            Assert.That(
                GameRulesJson.Parse("{}", _source, GameRules.Default.WithIdleWarningSeconds(45f)).IdleWarningSeconds,
                Is.EqualTo(45f),
                "omitted keeps the base");
        }

        [Test]
        public void WriteThenParseKeepsIdleWarningSeconds()
        {
            var defaults = GameRulesJson.Write(GameRules.Default);
            Assert.That(defaults, Does.Contain("\"idleWarningSeconds\": 15"));
            Assert.That(GameRulesJson.Parse(defaults, _source).IdleWarningSeconds, Is.EqualTo(15f));

            var rules = GameRules.Default.WithIdleKickSeconds(120f).WithIdleWarningSeconds(30f);
            var written = GameRulesJson.Write(rules);
            Assert.That(written, Does.Contain("\"idleWarningSeconds\": 30"));

            AssertSameRules(GameRulesJson.Parse(written, _source), rules);
        }

        [TestCase("{\"idleWarningSeconds\": 0}")]
        [TestCase("{\"idleWarningSeconds\": -5}")]
        [TestCase("{\"idleWarningSeconds\": 1e39}")]
        [TestCase("{\"idleWarningSeconds\": \"15\"}")]
        [TestCase("{\"idleWarningSeconds\": null}")]
        public void BadIdleWarningSecondsIsRejectedWithItsName(string json)
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse(json, _source));

            AssertNames(e, "idleWarningSeconds");
        }

        [Test]
        public void TheRespawnAnchorsCannotBeChangedThroughTheGivenArray()
        {
            var anchors = new[] { RespawnAnchor.Squad, RespawnAnchor.Base };
            var rules = GameRules.Default.WithRespawnAnchors(anchors);

            anchors[0] = RespawnAnchor.Beacon;

            Assert.That(rules.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Squad, RespawnAnchor.Base }));
            Assert.Throws<ArgumentNullException>(() => GameRules.Default.WithRespawnAnchors(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameRules.Default.WithRespawnAnchors(new[] { (RespawnAnchor)99 }));
        }

        [Test]
        public void ParseOverBaseRulesChangesOnlyTheWrittenItems()
        {
            var baseRules = GameRules.Default
                .WithRegenPerSecond(15f)
                .WithInfiniteReserve(true)
                .WithActions(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2));

            var rules = GameRulesJson.Parse("{\"viewMode\": \"ThirdPerson\", \"actions\": {\"cover\": \"On\"}}", _source, baseRules);

            var expected = baseRules
                .WithViewMode(ViewMode.ThirdPerson)
                .WithActions(baseRules.Actions.WithCover(Cover.On));
            AssertSameRules(rules, expected);
        }

        [Test]
        public void ParseOverBaseRulesOfAnEmptyObjectIsTheBase()
        {
            var baseRules = GameRules.Default.WithViewMode(ViewMode.ThirdPerson).WithMaxWeapons(3);

            AssertSameRules(GameRulesJson.Parse("{}", _source, baseRules), baseRules);
        }

        [Test]
        public void ParseOverBaseRulesRejectsNullBaseAndBadValuesWithTheirNames()
        {
            Assert.Throws<ArgumentNullException>(() => GameRulesJson.Parse("{}", _source, null));

            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"viewMode\": \"Overhead\"}", _source, GameRules.Default));
            AssertNames(e, "viewMode");
        }

        [Test]
        public void ItemNamesMatchTheRulesConstructors()
        {
            Assert.That(GameRulesJson.ItemNames.Count, Is.EqualTo(27));
            Assert.That(GameRulesJson.ActionItemNames.Count, Is.EqualTo(30));
            Assert.That(GameRulesJson.ItemNames, Does.Contain(GameRulesJson.ActionsItem));
        }

        [Test]
        public void OmittedReloadInterruptedByIsMeleeAndGrenadeAndAWrittenListIsRead()
        {
            var omitted = GameRulesJson.Parse("{}", _source).Actions;
            Assert.That(omitted.ReloadInterruptedBy, Is.EqualTo(new[] { ReloadInterrupt.Melee, ReloadInterrupt.Grenade }));
            Assert.That(omitted.InterruptsReload(ReloadInterrupt.Melee), Is.True);
            Assert.That(omitted.InterruptsReload(ReloadInterrupt.Grenade), Is.True);

            var meleeOnly = GameRulesJson.Parse("{\"actions\": {\"reloadInterruptedBy\": [\"Melee\"]}}", _source).Actions;
            Assert.That(meleeOnly.InterruptsReload(ReloadInterrupt.Melee), Is.True);
            Assert.That(meleeOnly.InterruptsReload(ReloadInterrupt.Grenade), Is.False);

            var none = GameRulesJson.Parse("{\"actions\": {\"reloadInterruptedBy\": []}}", _source).Actions;
            Assert.That(none.ReloadInterruptedBy, Is.Empty, "a game may let nothing interrupt a reload");
            Assert.That(none.InterruptsReload(ReloadInterrupt.Melee), Is.False);
        }

        [Test]
        public void WriteThenParseKeepsReloadInterruptedBy()
        {
            var rules = GameRules.Default.WithActions(GameRules.Default.Actions.WithReloadInterruptedBy(new[] { ReloadInterrupt.Grenade }));

            var json = GameRulesJson.Write(rules);

            Assert.That(GameRulesJson.Parse(json, _source).Actions.ReloadInterruptedBy, Is.EqualTo(new[] { ReloadInterrupt.Grenade }));
            Assert.That(
                GameRulesJson.Parse(GameRulesJson.Write(GameRules.Default), _source).Actions.ReloadInterruptedBy,
                Is.EqualTo(new[] { ReloadInterrupt.Melee, ReloadInterrupt.Grenade }));
        }

        [TestCase("{\"actions\": {\"reloadInterruptedBy\": [\"Sprint\"]}}")]
        [TestCase("{\"actions\": {\"reloadInterruptedBy\": [\"melee\"]}}")]
        [TestCase("{\"actions\": {\"reloadInterruptedBy\": [\"Melee\", \"Melee\"]}}")]
        [TestCase("{\"actions\": {\"reloadInterruptedBy\": \"Melee\"}}")]
        public void BadReloadInterruptedByIsRejectedWithItsName(string json)
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse(json, _source));

            AssertNames(e, "actions.reloadInterruptedBy");
        }

        [Test]
        public void TheReadLogShowsADashWhenNothingInterruptsAReload()
        {
            var rules = GameRules.Default.WithActions(GameRules.Default.Actions.WithReloadInterruptedBy(Array.Empty<ReloadInterrupt>()));

            Assert.That(
                GameRulesLog.Read(null, rules),
                Is.EqualTo("[GameRules] read '-' emote=Off stamina=Off respawnAnchor=Base reloadInterruptedBy=- idleWarningSeconds=15"));
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
            Assert.That(actions.AimToFire, Is.EqualTo(AimToFire.Off));
        }

        [Test]
        public void OmittedJumpIsOnAndOffIsReadAndWritten()
        {
            var omitted = GameRulesJson.Parse("{\"actions\": {\"cover\": \"On\"}}", _source).Actions;
            Assert.That(omitted.Jump, Is.EqualTo(Jump.On));

            var off = GameRulesJson.Parse("{\"actions\": {\"jump\": \"Off\"}}", _source);
            Assert.That(off.Actions.Jump, Is.EqualTo(Jump.Off));
            Assert.That(GameRulesJson.Write(off), Does.Contain("\"jump\": \"Off\""));
            Assert.That(GameRulesJson.Write(GameRules.Default), Does.Contain("\"jump\": \"On\""));

            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse("{\"actions\": {\"jump\": \"Maybe\"}}", _source));
            AssertNames(e, "actions.jump");
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
        [TestCase("aimToFire")]
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
                0.1f,
                2,
                ReloadMode.Manual,
                ViewMode.ThirdPerson,
                respawnAnchor: _everyAnchor);

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
                SlowMotion.On,
                AimToFire.On,
                // 既定が On なので、既定と違う値を書き戻せるかを見るには Off にする
                Jump.Off,
                emote: Emote.On,
                stamina: Stamina.On);
        }

        private static readonly RespawnAnchor[] _everyAnchor = { RespawnAnchor.Beacon, RespawnAnchor.Squad, RespawnAnchor.Base };

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
        [TestCase("{\"maxWeapons\": -1}", "maxWeapons")]
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
        [TestCase("{\"viewMode\": \"Overhead\"}", "viewMode")]
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
            Assert.That(actual.IdleWarningSeconds, Is.EqualTo(expected.IdleWarningSeconds), "idleWarningSeconds");
            Assert.That(actual.MaxWeapons, Is.EqualTo(expected.MaxWeapons), "maxWeapons");
            Assert.That(actual.ReloadMode, Is.EqualTo(expected.ReloadMode), "reloadMode");
            Assert.That(actual.ViewMode, Is.EqualTo(expected.ViewMode), "viewMode");
            Assert.That(actual.Streaks, Is.EqualTo(expected.Streaks), "streaks");
            Assert.That(actual.TextChat, Is.EqualTo(expected.TextChat), "textChat");
            Assert.That(actual.Spectating, Is.EqualTo(expected.Spectating), "spectating");
            Assert.That(actual.RespawnAnchors, Is.EqualTo(expected.RespawnAnchors), "respawnAnchor");

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
            Assert.That(a.AimToFire, Is.EqualTo(b.AimToFire), "actions.aimToFire");
            Assert.That(a.Jump, Is.EqualTo(b.Jump), "actions.jump");
            Assert.That(a.Emote, Is.EqualTo(b.Emote), "actions.emote");
            Assert.That(a.Stamina, Is.EqualTo(b.Stamina), "actions.stamina");
            Assert.That(a.ReloadInterruptedBy, Is.EqualTo(b.ReloadInterruptedBy), "actions.reloadInterruptedBy");
        }
    }
}
