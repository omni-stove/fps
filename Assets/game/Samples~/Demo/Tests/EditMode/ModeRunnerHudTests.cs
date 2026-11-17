using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ModeRunnerHudTests
    {
        private const float RespawnSeconds = 1f;

        private readonly List<Object> _objects = new List<Object>();
        private RecordingModeRunner _runner;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _runner = Track(new GameObject("Runner")).AddComponent<RecordingModeRunner>();
            _runner.Factory = () => TeamDeathmatchRunner.CreateSession(3, 4, 60f, 2, RespawnSeconds);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            ModeHudFormatters.Unregister(typeof(FakeDetail));
            GameSignals.ResetAll();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void TheHudNamesTheWinnerWhenTheScoreLimitIsReached()
        {
            var player = Member("Player", 0, Vector3.zero);
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            var hud = BoundHudView();
            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);

            KillBy(player, a);
            KillBy(player, b);

            Assert.That(hud.ResultText, Is.EqualTo("ALPHA の勝ち"));
        }

        [Test]
        public void TheHudShowsTheOwnSideFirstAndTheRespawnCountdown()
        {
            var player = Member("Player", 1, Vector3.zero);
            var a = Member("Bot-A", 0, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            var hud = BoundHudView();
            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);

            Assert.That(hud.IsShown, Is.True);
            Assert.That(hud.TimeText, Is.EqualTo("01:00"));
            Assert.That(hud.ScoreTexts[0], Is.EqualTo("BRAVO 0"));
            Assert.That(hud.Scores[0].Text, Is.EqualTo(ModeHudText.ScoreText(Side.Bravo, 0d)));
            Assert.That(hud.Scores[0].IsOwn, Is.True);
            Assert.That(hud.Lines, Is.Empty);

            KillBy(b, player);
            _runner.Tick(0f);

            Assert.That(hud.Lines, Is.EqualTo(new[] { "リスポーンまで 1 秒" }));
            Assert.That(hud.ScoreTexts[2], Is.EqualTo("SIDE 2 1"));
            Assert.That(hud.Scores[2].IsLeading, Is.True);
        }

        [Test]
        public void StoppingTheRunnerHidesTheHud()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var hud = BoundHudView();
            Begin(new[] { player, bot }, Array.Empty<SideSpawnPoint>(), player);

            _runner.Stop();

            Assert.That(hud.IsShown, Is.False);
        }

        [Test]
        public void AFormatterRegisteredForTheDetailWritesTheHud()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var hud = BoundHudView();
            ModeHudFormatters.Register<FakeDetail>(new FakeDetailFormatter());
            _runner.Detail = new FakeDetail();

            Begin(new[] { player, bot }, Array.Empty<SideSpawnPoint>(), player);

            Assert.That(hud.TimeText, Is.EqualTo("fake time"));
            Assert.That(hud.ScoreTexts, Is.EqualTo(new[] { "fake score" }));
            Assert.That(hud.Lines, Is.EqualTo(new[] { "fake line" }), "the formatter writes the lines");
            Assert.That(hud.ResultText, Is.Empty);
        }

        private void Begin(IReadOnlyList<SideMember> members, IReadOnlyList<SideSpawnPoint> spawns, SideMember localPlayer)
        {
            _runner.Begin(_runner.Factory(), members, spawns, localPlayer);
        }

        private static void KillBy(SideMember killer, SideMember victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
        }

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        private ModeHudView BoundHudView()
        {
            var view = Track(new GameObject("ModeHud")).AddComponent<ModeHudView>();
            view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
            return view;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }

        private sealed class FakeDetail
        {
        }

        private sealed class FakeDetailFormatter : IModeHudFormatter
        {
            public LocalizedText Time(ModeStatus status) => LocalizedText.Literal("fake time");

            public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) =>
                new[] { new ModeHudScoreLine(Side.Alpha, LocalizedText.Literal("fake score"), true, false) };

            public void AppendLines(ModeStatus status, List<LocalizedText> lines) => lines.Add(LocalizedText.Literal("fake line"));

            public LocalizedText Result(ModeStatus status) => default;
        }
    }
}
