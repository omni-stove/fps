using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerUnsupportedActionsTests
    {
        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        private static readonly string[] _stealthOnly = { "stealth" };

        private static readonly string[] _slideAndEmote = { "slide", "emote" };

        private static IEnumerable<TestCaseData> MovedOnline()
        {
            yield return new TestCaseData(ActionRules.Default.WithSlide(Slide.On)).SetName("SlideOn");
            yield return new TestCaseData(ActionRules.Default.WithMantle(Mantle.On)).SetName("MantleOn");
            yield return new TestCaseData(ActionRules.Default.WithZipline(Zipline.On)).SetName("ZiplineOn");
            yield return new TestCaseData(ActionRules.Default.WithStamina(Stamina.On)).SetName("StaminaOn");
            yield return new TestCaseData(ActionRules.Default.WithStagger(Stagger.On)).SetName("StaggerOn");
            yield return new TestCaseData(ActionRules.Default.WithSuppression(Suppression.On)).SetName("SuppressionOn");
            yield return new TestCaseData(ActionRules.Default.WithEmote(Emote.On)).SetName("EmoteOn");
        }

        [Test]
        public void StealthThatIsOnIsReturned()
        {
            var enabled = ServerUnsupportedActions.EnabledIn(GameRules.Default.WithActions(ActionRules.Default.WithStealth(Stealth.On)));

            Assert.That(enabled, Is.EqualTo(_stealthOnly));
        }

        [TestCaseSource(nameof(MovedOnline))]
        public void AnActionTheServerNowHandlesIsNotReturned(ActionRules actions)
        {
            Assert.That(ServerUnsupportedActions.EnabledIn(GameRules.Default.WithActions(actions)), Is.Empty);
        }

        [Test]
        public void TheDefaultRulesHaveNoUnsupportedAction()
        {
            Assert.That(ServerUnsupportedActions.EnabledIn(GameRules.Default), Is.Empty);
        }

        [Test]
        public void TurningAnActionBackOffRemovesIt()
        {
            var actions = ActionRules.Default.WithStealth(Stealth.On).WithStealth(Stealth.Off);

            Assert.That(ServerUnsupportedActions.EnabledIn(GameRules.Default.WithActions(actions)), Is.Empty);
        }

        [Test]
        public void SupportedActionsThatAreOnAreNotReturned()
        {
            var actions = ActionRules.Default
                .WithDowned(Downed.Revive)
                .WithMelee(Melee.On)
                .WithPing(Ping.On)
                .WithProne(Prone.On);

            Assert.That(ServerUnsupportedActions.EnabledIn(GameRules.Default.WithActions(actions)), Is.Empty);
        }

        [Test]
        public void WithEveryActionOnOnlyStealthIsReturned()
        {
            var actions = ActionRules.Default
                .WithStealth(Stealth.On)
                .WithEmote(Emote.On)
                .WithSuppression(Suppression.On)
                .WithStagger(Stagger.On)
                .WithStamina(Stamina.On)
                .WithZipline(Zipline.On)
                .WithMantle(Mantle.On)
                .WithSlide(Slide.On);

            var enabled = ServerUnsupportedActions.EnabledIn(GameRules.Default.WithActions(actions));

            Assert.That(enabled, Is.EqualTo(_stealthOnly));
        }

        [Test]
        public void NullRulesAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ServerUnsupportedActions.EnabledIn(null));
        }

        [Test]
        public void TheWarningNamesTheRulesAndEveryAction()
        {
            var line = ServerUnsupportedActionsLog.Warning("TeamDeathmatch", _slideAndEmote);

            Assert.That(line, Is.EqualTo(
                "[Rules] warning: actions slide, emote are on in rules TeamDeathmatch but the server does not support them; they are ignored"));
        }

        [Test]
        public void NoActionsWriteNoWarning()
        {
            Assert.That(ServerUnsupportedActionsLog.Warning("TeamDeathmatch", Array.Empty<string>()), Is.Null);
        }

        [Test]
        public void TheStartLogWarnsOnceAboutAnActionThatIsOn()
        {
            var log = StartLog(ActionRules.Default.WithStealth(Stealth.On));

            Assert.That(log, Does.Contain("[Rules] warning: actions stealth are on in rules "));
            Assert.That(CountOf(log, "[Rules] warning:"), Is.EqualTo(1));
        }

        [Test]
        public void WithEveryActionOnTheStartLogWarnsOnlyAboutStealth()
        {
            var log = StartLog(ActionRules.Default
                .WithSlide(Slide.On)
                .WithMantle(Mantle.On)
                .WithZipline(Zipline.On)
                .WithStamina(Stamina.On)
                .WithStagger(Stagger.On)
                .WithSuppression(Suppression.On)
                .WithEmote(Emote.On)
                .WithStealth(Stealth.On));

            Assert.That(log, Does.Contain("[Rules] warning: actions stealth are on in rules "));
            Assert.That(CountOf(log, "[Rules] warning:"), Is.EqualTo(1));
        }

        [Test]
        public void TheStartLogHasNoWarningWhenEveryActionIsOff()
        {
            var log = StartLog(ActionRules.Default);

            Assert.That(log, Does.Not.Contain("[Rules] warning:"));
        }

        private static string StartLog(ActionRules actions)
        {
            using (var log = new StringWriter())
            using (var json = new StringWriter())
            using (var transport = new RecordingTransport())
            {
                var rules = GameRules.Default.WithActions(actions);
                var settings = NetMatchSettings.Default;
                var match = new ServerMatch(settings.RestartDelaySeconds, log, new ServerEventWriter(true, json, log), () => _time);
                var context = new ServerContext(
                    new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                    "1.0.0",
                    log,
                    TestMaps.Data("NetTest", TestRules.Points(0)),
                    match,
                    new ServerMessenger(transport),
                    new StubControl(),
                    TestRules.Data(rules, settings));
                var module = new MatchRulesModule();
                module.Start(context);
                module.Stop();
                return log.ToString();
            }
        }

        private static int CountOf(string text, string part)
        {
            var count = 0;
            for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }
    }
}
