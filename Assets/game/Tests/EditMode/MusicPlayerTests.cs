using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update が回らないので、時間は Tick で進める。
    public sealed class MusicPlayerTests
    {
        private const float CatalogVolume = 0.5f;

        private readonly List<Object> _created = new List<Object>();
        private ControlSettings _appliedBefore;
        private MusicCatalog _catalog;
        private AudioClip _menu;
        private AudioClip _match;
        private AudioClip _victory;
        private AudioClip _defeat;
        private AudioClip _end;
        private MusicPlayer _player;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            AppliedControlSettings.Apply(ControlSettings.Default);
            GameSignals.ModeStatus.Clear();

            _menu = Clip("Menu");
            _match = Clip("Match");
            _victory = Clip("Victory");
            _defeat = Clip("Defeat");
            _end = Clip("End");
            _catalog = Track(ScriptableObject.CreateInstance<MusicCatalog>());
            _catalog.name = "MusicPlayerTests";
            _catalog.Menu = _menu;
            _catalog.Match = _match;
            _catalog.Victory = _victory;
            _catalog.Defeat = _defeat;
            _catalog.MatchEnd = _end;
            _catalog.Volume = CatalogVolume;
            _catalog.FadeSeconds = 1f;

            _player = Track(new GameObject("MusicPlayerTests")).AddComponent<MusicPlayer>();
            _player.Catalog = _catalog;
            _player.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            _player.Detach();
            GameSignals.ModeStatus.Clear();
            AppliedControlSettings.Apply(_appliedBefore);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheMenuPlaysTheMenuMusicAndFadesItIn()
        {
            _player.SetInMenu(true);

            Assert.That(_player.Cue, Is.EqualTo(MusicCue.Menu));
            Assert.That(_player.TargetClip, Is.SameAs(_menu));

            _player.Tick(0.5f);
            Assert.That(_player.Source.clip, Is.SameAs(_menu), "nothing was playing, so it starts at once");
            Assert.That(_player.Source.loop, Is.True);
            Assert.That(_player.Level, Is.EqualTo(0f));
            Assert.That(_player.Source.volume, Is.EqualTo(0f));

            _player.Tick(0.5f);
            Assert.That(_player.Level, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(_player.Source.volume, Is.EqualTo(CatalogVolume * 0.5f).Within(1e-5f));

            _player.Tick(5f);
            Assert.That(_player.Level, Is.EqualTo(1f));
            Assert.That(_player.Source.volume, Is.EqualTo(CatalogVolume).Within(1e-5f));
        }

        // メニュー → 試合 → 勝ち → 次の試合 → メニューと場面が移るたびに曲が替わる
        [Test]
        public void EachSceneChangeSwapsTheMusicAfterTheFadeOut()
        {
            PlayUntilSteady(true);
            Assert.That(_player.Source.clip, Is.SameAs(_menu));

            _player.SetInMenu(false);
            Assert.That(_player.Cue, Is.EqualTo(MusicCue.Match));
            Assert.That(_player.TargetClip, Is.SameAs(_match));

            _player.Tick(0.5f);
            Assert.That(_player.Source.clip, Is.SameAs(_menu), "the menu music fades out first");
            Assert.That(_player.Level, Is.EqualTo(0.5f).Within(1e-5f));

            _player.Tick(0.5f);
            Assert.That(_player.Source.clip, Is.SameAs(_match));
            Assert.That(_player.Level, Is.EqualTo(0f));

            _player.Tick(1f);
            Assert.That(_player.Level, Is.EqualTo(1f));

            GameSignals.ModeStatus.Publish(Status(false, Side.Alpha, null));
            Assert.That(_player.Cue, Is.EqualTo(MusicCue.Match), "a running match keeps the match music");

            GameSignals.ModeStatus.Publish(Status(true, Side.Alpha, Side.Alpha));
            Assert.That(_player.Cue, Is.EqualTo(MusicCue.Victory));
            _player.Tick(1f);
            _player.Tick(0f);
            Assert.That(_player.Source.clip, Is.SameAs(_victory));
            Assert.That(_player.Source.loop, Is.False, "the end music plays once by default");

            GameSignals.ModeStatus.Publish(Status(false, Side.Alpha, null));
            Assert.That(_player.Cue, Is.EqualTo(MusicCue.Match), "the next match brings the match music back");

            _player.SetInMenu(true);
            Assert.That(_player.Cue, Is.EqualTo(MusicCue.Menu));
            _player.Tick(1f);
            _player.Tick(0f);
            Assert.That(_player.Source.clip, Is.SameAs(_menu));
        }

        [Test]
        public void ALostMatchPlaysTheDefeatMusic()
        {
            PlayUntilSteady(false);

            GameSignals.ModeStatus.Publish(Status(true, Side.Alpha, Side.Bravo));

            Assert.That(_player.Cue, Is.EqualTo(MusicCue.Defeat));
            Assert.That(_player.TargetClip, Is.SameAs(_defeat));
        }

        [Test]
        public void ADrawPlaysTheMatchEndMusic()
        {
            PlayUntilSteady(false);

            GameSignals.ModeStatus.Publish(Status(true, Side.Alpha, null));

            Assert.That(_player.Cue, Is.EqualTo(MusicCue.MatchEnd));
            Assert.That(_player.TargetClip, Is.SameAs(_end));
        }

        // 下げている途中で元の場面に戻ったら、曲を替えずにそこから上げ直す
        [Test]
        public void GoingBackDuringTheFadeOutKeepsTheSameMusic()
        {
            PlayUntilSteady(true);

            _player.SetInMenu(false);
            _player.Tick(0.75f);
            Assert.That(_player.Level, Is.EqualTo(0.25f).Within(1e-5f));

            _player.SetInMenu(true);
            _player.Tick(0.25f);

            Assert.That(_player.Source.clip, Is.SameAs(_menu));
            Assert.That(_player.Level, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void TheMusicVolumeSettingScalesTheMusicAtOnce()
        {
            PlayUntilSteady(true);

            AppliedControlSettings.Apply(ControlSettings.Default.WithMusicVolume(40));
            Assert.That(_player.Source.volume, Is.EqualTo(CatalogVolume * 0.4f).Within(1e-5f));

            AppliedControlSettings.Apply(ControlSettings.Default.WithMusicVolume(0));
            Assert.That(_player.Source.volume, Is.EqualTo(0f));

            AppliedControlSettings.Apply(ControlSettings.Default.With(ControlValue.EffectsVolume, 0));
            Assert.That(_player.Source.volume, Is.EqualTo(CatalogVolume).Within(1e-5f), "the effects volume does not touch the music");
        }

        // 全体の音量は AudioListener が掛けるので、曲の音量には二重に掛けない
        [Test]
        public void TheMasterVolumeIsLeftToTheListener()
        {
            PlayUntilSteady(true);

            AppliedControlSettings.Apply(ControlSettings.Default.With(ControlValue.MasterVolume, 50).WithMusicVolume(80));

            Assert.That(AudioListener.volume, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(_player.Source.volume, Is.EqualTo(CatalogVolume * 0.8f).Within(1e-5f));
        }

        [Test]
        public void WithoutAFadeTheMusicSwitchesAtOnce()
        {
            _catalog.FadeSeconds = 0f;
            _player.Catalog = _catalog;

            _player.SetInMenu(true);
            _player.Tick(0f);
            Assert.That(_player.Source.clip, Is.SameAs(_menu));
            Assert.That(_player.Level, Is.EqualTo(1f));

            _player.SetInMenu(false);
            _player.Tick(0f);
            Assert.That(_player.Source.clip, Is.SameAs(_match));
            Assert.That(_player.Level, Is.EqualTo(1f));
        }

        [Test]
        public void AnEmptySlotFadesOutToSilence()
        {
            _catalog.Match = null;
            PlayUntilSteady(true);

            _player.SetInMenu(false);
            Assert.That(_player.TargetClip, Is.Null);
            _player.Tick(1f);
            _player.Tick(1f);

            Assert.That(_player.Source.clip, Is.Null);
            Assert.That(_player.Level, Is.EqualTo(0f));
            Assert.That(_player.Source.volume, Is.EqualTo(0f));
        }

        [Test]
        public void WithoutACatalogNothingPlays()
        {
            _player.Catalog = null;

            _player.SetInMenu(true);
            _player.Tick(1f);
            _player.Tick(1f);

            Assert.That(_player.TargetClip, Is.Null);
            Assert.That(_player.Source.clip, Is.Null);
            Assert.That(_player.Source.volume, Is.EqualTo(0f));
        }

        [Test]
        public void TheSourceIsTwoDimensionalAndKeepsPlayingThroughAPause()
        {
            var source = _player.Source;

            Assert.That(source.spatialBlend, Is.EqualTo(0f));
            Assert.That(source.playOnAwake, Is.False);
            Assert.That(source.ignoreListenerPause, Is.True);
        }

        [Test]
        public void DetachedPlayersIgnoreTheModeStatus()
        {
            PlayUntilSteady(false);
            _player.Detach();

            GameSignals.ModeStatus.Publish(Status(true, Side.Alpha, Side.Alpha));

            Assert.That(_player.Cue, Is.EqualTo(MusicCue.Match));
            Assert.That(_player.IsAttached, Is.False);
        }

        [Test]
        public void TheCatalogFallsBackToTheEndClipForTheResults()
        {
            var catalog = Track(ScriptableObject.CreateInstance<MusicCatalog>());
            catalog.MatchEnd = _end;

            Assert.That(catalog.ClipFor(MusicCue.Victory), Is.SameAs(_end));
            Assert.That(catalog.ClipFor(MusicCue.Defeat), Is.SameAs(_end));
            Assert.That(catalog.ClipFor(MusicCue.MatchEnd), Is.SameAs(_end));
            Assert.That(catalog.ClipFor(MusicCue.Menu), Is.Null);
            Assert.That(catalog.ClipFor(MusicCue.None), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.ClipFor((MusicCue)99));

            catalog.Victory = _victory;
            Assert.That(catalog.ClipFor(MusicCue.Victory), Is.SameAs(_victory));
            Assert.That(catalog.ClipFor(MusicCue.Defeat), Is.SameAs(_end));
        }

        [Test]
        public void OnlyTheEndMusicCanBeSetToPlayOnce()
        {
            var catalog = Track(ScriptableObject.CreateInstance<MusicCatalog>());

            Assert.That(catalog.LoopMatchEnd, Is.False);
            Assert.That(catalog.Loops(MusicCue.Menu), Is.True);
            Assert.That(catalog.Loops(MusicCue.Match), Is.True);
            Assert.That(catalog.Loops(MusicCue.Victory), Is.False);
            Assert.That(catalog.Loops(MusicCue.Defeat), Is.False);
            Assert.That(catalog.Loops(MusicCue.MatchEnd), Is.False);

            catalog.LoopMatchEnd = true;
            Assert.That(catalog.Loops(MusicCue.Victory), Is.True);
        }

        [Test]
        public void ANewCatalogStartsFromTheDefaultsAndClampsItsValues()
        {
            var catalog = Track(ScriptableObject.CreateInstance<MusicCatalog>());

            Assert.That(catalog.Volume, Is.EqualTo(MusicCatalog.DefaultVolume));
            Assert.That(catalog.FadeSeconds, Is.EqualTo(MusicCatalog.DefaultFadeSeconds));

            catalog.Volume = 3f;
            catalog.FadeSeconds = -1f;
            Assert.That(catalog.Volume, Is.EqualTo(1f));
            Assert.That(catalog.FadeSeconds, Is.EqualTo(0f));

            catalog.FadeSeconds = 99f;
            Assert.That(catalog.FadeSeconds, Is.EqualTo(MusicCatalog.MaxFadeSeconds));
        }

        [Test]
        public void ValidateNamesEveryMissingClip()
        {
            var catalog = Track(ScriptableObject.CreateInstance<MusicCatalog>());
            catalog.name = "Broken";

            var e = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(e.Message, Does.Contain("'Broken'").And.Contain("menu").And.Contain("match music").And.Contain("match end"));
            Assert.DoesNotThrow(() => _catalog.Validate());

            catalog.Menu = _menu;
            catalog.Match = _match;
            catalog.Victory = _victory;
            catalog.Defeat = _defeat;
            Assert.DoesNotThrow(() => catalog.Validate(), "the results can stand in for the end clip");
        }

        [Test]
        public void TheCatalogLivesUnderThePresentationResources()
        {
            Assert.That(MusicCatalog.ResourcePath, Is.EqualTo("Presentation/MusicCatalog"));
        }

        private void PlayUntilSteady(bool inMenu)
        {
            _player.SetInMenu(inMenu);
            _player.Tick(0f);
            _player.Tick(10f);
            Assert.That(_player.Level, Is.EqualTo(1f));
        }

        private static ModeStatus Status(bool ended, Side? own, Side? winner)
        {
            return new ModeStatus(30f, Array.Empty<SideScore>(), own, null, ended, winner, null);
        }

        private AudioClip Clip(string clipName)
        {
            return Track(AudioClip.Create("MusicPlayerTests." + clipName, 441, 1, 44100, false));
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
