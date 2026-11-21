using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class MusicCuesTests
    {
        // メニューのシーンにいる間は、前の試合の状態が残っていてもメニューの曲
        [Test]
        public void TheMenuWinsOverAnyModeStatus()
        {
            Assert.That(MusicCues.Pick(true, null), Is.EqualTo(MusicCue.Menu));
            Assert.That(MusicCues.Pick(true, Status(false, Side.Alpha, null)), Is.EqualTo(MusicCue.Menu));
            Assert.That(MusicCues.Pick(true, Status(true, Side.Alpha, Side.Alpha)), Is.EqualTo(MusicCue.Menu));
        }

        // メニューの外で、モードが無い (射撃場など) か試合が進んでいる間は試合の曲
        [Test]
        public void OutsideTheMenuItIsTheMatchUntilTheModeEnds()
        {
            Assert.That(MusicCues.Pick(false, null), Is.EqualTo(MusicCue.Match));
            Assert.That(MusicCues.Pick(false, Status(false, Side.Bravo, null)), Is.EqualTo(MusicCue.Match));
            Assert.That(MusicCues.Pick(false, Status(false, null, null)), Is.EqualTo(MusicCue.Match));
        }

        // 陣営の Id で比べるので、チーム戦でも個人戦 (Id が 2 以上) でも同じ
        [TestCase(0, 0, MusicCue.Victory)]
        [TestCase(1, 1, MusicCue.Victory)]
        [TestCase(0, 1, MusicCue.Defeat)]
        [TestCase(1, 0, MusicCue.Defeat)]
        [TestCase(5, 5, MusicCue.Victory)]
        [TestCase(5, 2, MusicCue.Defeat)]
        public void AnEndedMatchPlaysTheOwnSidesResult(int own, int winner, MusicCue expected)
        {
            Assert.That(MusicCues.Pick(false, Status(true, new Side(own), new Side(winner))), Is.EqualTo(expected));
        }

        // 引き分けや観戦 (自分の陣営が無い) は勝ち負けを言わない
        [Test]
        public void ADrawOrASpectatedEndIsTheNeutralEnd()
        {
            Assert.That(MusicCues.Pick(false, Status(true, Side.Alpha, null)), Is.EqualTo(MusicCue.MatchEnd));
            Assert.That(MusicCues.Pick(false, Status(true, null, Side.Alpha)), Is.EqualTo(MusicCue.MatchEnd));
            Assert.That(MusicCues.Pick(false, Status(true, null, null)), Is.EqualTo(MusicCue.MatchEnd));
        }

        // 試合が終わってまた進み始めたら (再戦・次のラウンド)、試合の曲に戻る
        [Test]
        public void ARestartedMatchGoesBackToTheMatchMusic()
        {
            Assert.That(MusicCues.Pick(false, Status(true, Side.Alpha, Side.Alpha)), Is.EqualTo(MusicCue.Victory));
            Assert.That(MusicCues.Pick(false, Status(false, Side.Alpha, null)), Is.EqualTo(MusicCue.Match));
        }

        [Test]
        public void TheMenuSceneIsTheConfiguredMainMenu()
        {
            Assert.That(MusicCues.IsMenuScene("MainMenu", MenuScenes.Default), Is.True);
            Assert.That(MusicCues.IsMenuScene("Bootstrap", MenuScenes.Default), Is.False);
            Assert.That(MusicCues.IsMenuScene("mainmenu", MenuScenes.Default), Is.False, "scene names are matched exactly");
            Assert.That(MusicCues.IsMenuScene(string.Empty, MenuScenes.Default), Is.False);
            Assert.That(MusicCues.IsMenuScene(null, MenuScenes.Default), Is.False);

            var custom = new MenuScenes("Title", null, null);
            Assert.That(MusicCues.IsMenuScene("Title", custom), Is.True);
            Assert.That(MusicCues.IsMenuScene("MainMenu", custom), Is.False, "a game that renames its menu scene gets menu music there only");
        }

        [Test]
        public void TheMenuSceneNeedsTheScenes()
        {
            Assert.Throws<ArgumentNullException>(() => MusicCues.IsMenuScene("MainMenu", null));
        }

        private static ModeStatus Status(bool ended, Side? own, Side? winner)
        {
            return new ModeStatus(60f, Array.Empty<SideScore>(), own, null, ended, winner, null);
        }
    }
}
