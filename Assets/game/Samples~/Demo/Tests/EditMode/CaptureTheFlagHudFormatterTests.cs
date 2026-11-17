using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class CaptureTheFlagHudFormatterTests
    {
        private const int Alpha1 = 1;
        private const int Alpha2 = 2;
        private const int Bravo1 = 3;
        private const int Bravo2 = 4;

        private static readonly string[] _playedFlagLines = { "ALPHA の旗: 落ちている (戻るまで 16 秒)", "BRAVO の旗: 運ばれている" };

        [Test]
        public void EachFlagStateHasItsOwnLine()
        {
            Assert.That(Localizer.Resolve(CaptureTheFlagHudFormatter.Flag(Side.Alpha, FlagState.AtBase, null)), Is.EqualTo("ALPHA の旗: 台にある"));
            Assert.That(Localizer.Resolve(CaptureTheFlagHudFormatter.Flag(Side.Bravo, FlagState.Carried, null)), Is.EqualTo("BRAVO の旗: 運ばれている"));
            Assert.That(
                Localizer.Resolve(CaptureTheFlagHudFormatter.Flag(Side.Alpha, FlagState.Dropped, 15.2f)),
                Is.EqualTo("ALPHA の旗: 落ちている (戻るまで 16 秒)"));
            Assert.That(
                Localizer.Resolve(CaptureTheFlagHudFormatter.Flag(Side.Bravo, FlagState.Dropped, 0f)),
                Is.EqualTo("BRAVO の旗: 落ちている (戻るまで 0 秒)"));
        }

        [Test]
        public void ADroppedFlagNeedsItsReturnSecondsAndAnUnknownStateIsRejected()
        {
            Assert.Throws<ArgumentException>(() => CaptureTheFlagHudFormatter.Flag(Side.Alpha, FlagState.Dropped, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => CaptureTheFlagHudFormatter.Flag(Side.Alpha, (FlagState)99, null));
        }

        [Test]
        public void TheCarryingHintDependsOnWhetherTheOwnFlagIsAtBase()
        {
            Assert.That(Localizer.Resolve(CaptureTheFlagHudFormatter.Carrying(Side.Bravo, true)), Is.EqualTo("BRAVO の旗を運んでいる: 自陣の台へ"));
            Assert.That(
                Localizer.Resolve(CaptureTheFlagHudFormatter.Carrying(Side.Bravo, false)),
                Is.EqualTo("BRAVO の旗を運んでいる: 自陣の旗が台に戻ると得点できる"));
        }

        [Test]
        public void AtTheStartBothFlagsAreAtBaseAndThereIsNoHint()
        {
            var status = new CaptureTheFlagStatus(Started(), Alpha1);

            Assert.That(
                CaptureTheFlagHudFormatter.HudLines(status).Select(Localizer.Resolve),
                Is.EqualTo(new[] { "ALPHA の旗: 台にある", "BRAVO の旗: 台にある" }));
            Assert.Throws<ArgumentNullException>(() => CaptureTheFlagHudFormatter.HudLines(null));
        }

        [Test]
        public void TheCarrierIsToldToBringTheFlagHomeWhileTheOwnFlagIsAtBase()
        {
            var game = Started();
            game.Touch(Side.Bravo, Alpha1);

            Assert.That(
                CaptureTheFlagHudFormatter.HudLines(new CaptureTheFlagStatus(game, Alpha1)).Select(Localizer.Resolve),
                Is.EqualTo(new[] { "ALPHA の旗: 台にある", "BRAVO の旗: 運ばれている", "BRAVO の旗を運んでいる: 自陣の台へ" }));
        }

        [Test]
        public void TheCarrierIsToldToWaitWhileTheOwnFlagIsAway()
        {
            var game = Played();

            Assert.That(
                CaptureTheFlagHudFormatter.HudLines(new CaptureTheFlagStatus(game, Alpha1)).Select(Localizer.Resolve),
                Is.EqualTo(_playedFlagLines.Concat(new[] { "BRAVO の旗を運んでいる: 自陣の旗が台に戻ると得点できる" })));
        }

        [Test]
        public void APlayerWhoIsNotCarryingSeesOnlyTheFlags()
        {
            var game = Played();

            Assert.That(CaptureTheFlagHudFormatter.HudLines(new CaptureTheFlagStatus(game, Bravo2)).Select(Localizer.Resolve), Is.EqualTo(_playedFlagLines));
            Assert.That(CaptureTheFlagHudFormatter.HudLines(new CaptureTheFlagStatus(game, null)).Select(Localizer.Resolve), Is.EqualTo(_playedFlagLines));
        }

        [Test]
        public void TheRespawnCountdownComesBeforeTheFlagLines()
        {
            var game = Played();
            var status = new ModeStatus(120f, null, Side.Bravo, 0.5f, false, null, new CaptureTheFlagStatus(game, Bravo1));

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<CaptureTheFlagHudFormatter>());
            var lines = new List<LocalizedText>();
            ModeHudFormatters.For(status).AppendLines(status, lines);

            Assert.That(lines.Select(Localizer.Resolve), Is.EqualTo(new[] { "リスポーンまで 1 秒" }.Concat(_playedFlagLines)));
        }

        private static CaptureTheFlag Started()
        {
            var game = new CaptureTheFlag(new CaptureTheFlagSpec(3, 600f, 5f, 1.5f, 2f, 20f, 1));
            game.Session.AddPlayer(Alpha1, Side.Alpha);
            game.Session.AddPlayer(Alpha2, Side.Alpha);
            game.Session.AddPlayer(Bravo1, Side.Bravo);
            game.Session.AddPlayer(Bravo2, Side.Bravo);
            game.Session.Start();
            return game;
        }

        // 旗は 20 秒で戻るので、落ちてから 4.5 秒たった Alpha の旗は戻るまで 15.5 秒。
        private static CaptureTheFlag Played()
        {
            var game = Started();
            game.Touch(Side.Bravo, Alpha1);
            game.Touch(Side.Alpha, Bravo1);
            game.ReportKill(Alpha1, Bravo1);
            game.Session.Tick(4.5f);
            return game;
        }
    }
}
