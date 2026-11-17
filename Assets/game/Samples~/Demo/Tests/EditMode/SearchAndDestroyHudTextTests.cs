using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class SearchAndDestroyHudTextTests
    {
        private const int _alphaOne = 0;
        private const int _alphaTwo = 1;
        private const int _bravoOne = 10;
        private const int _bravoTwo = 11;

        private const float _roundSeconds = 60f;
        private const float _plantSeconds = 3f;
        private const float _defuseSeconds = 5f;
        private const float _fuseSeconds = 20f;

        private static readonly string[] _expectedCarrierPlantingLines =
        {
            "ラウンド 1 (3 勝先取)",
            "あなたは攻撃側: 爆弾を設置地点に設置する",
            "爆弾を持っている: 設置地点で長押しして設置",
            "設置中 B 33%",
        };

        private static readonly string[] _expectedStartScores = { "ALPHA 0 (攻撃)", "BRAVO 0 (防衛)" };

        private GameObject _owner;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (_owner != null)
            {
                Object.DestroyImmediate(_owner);
            }

            GameSignals.ResetAll();
        }

        [Test]
        public void ClockShowsTheFuseAfterThePlant()
        {
            var game = Create(preparationSeconds: 5f);
            game.Start();
            Assert.That(SearchAndDestroyHudText.Clock(game), Is.EqualTo(LocalizedText.Literal("00:05")));

            game.Tick(5f);
            Assert.That(SearchAndDestroyHudText.Clock(game), Is.EqualTo(LocalizedText.Literal("01:00")));

            Plant(game, 0);
            game.Tick(0.5f);
            Assert.That(SearchAndDestroyHudText.Clock(game), Is.EqualTo(LocalizedText.Literal("00:20")));
            Assert.That(SearchAndDestroyHudText.Lines(game, _bravoOne), Has.Member(SearchAndDestroyHudText.Fuse(0, _fuseSeconds - 0.5f)));
        }

        [Test]
        public void LinesShowTheRoleAndThePlantProgressOfTheCarrier()
        {
            var game = CarrierPlantingOnB();

            Assert.That(Resolved(SearchAndDestroyHudText.Lines(game, _alphaOne)), Is.EqualTo(_expectedCarrierPlantingLines));
        }

        [Test]
        public void TextFormatsTheFuseAndTheRoundResult()
        {
            Assert.That(SearchAndDestroyHudText.Fuse(1, 12.3f), Is.EqualTo(new LocalizedText("mode.snd.fuse", "B", 13)));
            Assert.That(GameText.Resolve(SearchAndDestroyHudText.Fuse(1, 12.3f)), Is.EqualTo("爆弾 B に設置: 起爆まで 13 秒"));
            Assert.That(GameText.Resolve(SearchAndDestroyHudText.RoundResult(Side.Bravo, RoundEndReason.Detonated)), Is.EqualTo("BRAVO の勝ち (起爆)"));
            Assert.That(GameText.Resolve(SearchAndDestroyHudText.RoundResult(null, RoundEndReason.BothEliminated)), Is.EqualTo("引き分け (同時に全滅)"));
        }

        [Test]
        public void ScoresShowRoundWinsAndRolesWithTheOwnSideFirst()
        {
            var game = Create();
            game.Start();
            Plant(game, 0);
            game.Tick(_fuseSeconds);

            var lines = SearchAndDestroyHudText.Scores(game, Side.Bravo);

            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(GameText.Resolve(lines[0].Text), Is.EqualTo("BRAVO 0 (防衛)"));
            Assert.That(lines[0].IsOwn, Is.True);
            Assert.That(lines[0].IsLeading, Is.False);
            Assert.That(GameText.Resolve(lines[1].Text), Is.EqualTo("ALPHA 1 (攻撃)"));
            Assert.That(lines[1].IsLeading, Is.True);
        }

        [Test]
        public void TheResultAppearsWhenASideReachesWinsToWin()
        {
            var game = Create(winsToWin: 2);
            game.Start();
            Plant(game, 0);
            game.Tick(_fuseSeconds);
            Assert.That(SearchAndDestroyHudText.Result(game).IsEmpty, Is.True, "one round is not the match");

            Plant(game, 1);
            game.Tick(_fuseSeconds);

            Assert.That(GameText.Resolve(SearchAndDestroyHudText.Result(game)), Is.EqualTo("ALPHA の勝ち"));
        }

        [Test]
        public void TheFormatterIsRegisteredForTheSearchAndDestroyStatus()
        {
            var status = Status(Create(), Side.Alpha, _alphaOne);

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<SearchAndDestroyHudFormatter>());
        }

        [Test]
        public void TheFormatterReadsTheGameAndTheLocalPlayer()
        {
            var status = Status(CarrierPlantingOnB(), Side.Alpha, _alphaOne);
            var formatter = new SearchAndDestroyHudFormatter();

            Assert.That(formatter.Time(status), Is.EqualTo(LocalizedText.Literal("00:59")));
            var scores = formatter.Scores(status);
            Assert.That(scores.Select(score => GameText.Resolve(score.Text)).ToArray(), Is.EqualTo(_expectedStartScores));
            Assert.That(scores[0].IsOwn, Is.True);
            var lines = new List<LocalizedText>();
            formatter.AppendLines(status, lines);
            Assert.That(Resolved(lines), Is.EqualTo(_expectedCarrierPlantingLines));
            Assert.That(formatter.Result(status).IsEmpty, Is.True);
        }

        [Test]
        public void TheModeHudShowsTheSearchAndDestroyStatus()
        {
            _owner = new GameObject(nameof(TheModeHudShowsTheSearchAndDestroyStatus));
            var view = _owner.AddComponent<ModeHudView>();
            view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());

            view.Show(Status(CarrierPlantingOnB(), Side.Alpha, _alphaOne));

            Assert.That(view.TimeText, Is.EqualTo("00:59"));
            Assert.That(view.ScoreTexts, Is.EqualTo(_expectedStartScores));
            Assert.That(view.Lines, Is.EqualTo(_expectedCarrierPlantingLines));
            Assert.That(view.ResultText, Is.Empty);
        }

        [Test]
        public void TheFormatterRejectsAnotherDetail()
        {
            var formatter = new SearchAndDestroyHudFormatter();
            var other = new ModeStatus(null, null, null, null, false, null, new object());

            Assert.Throws<ArgumentException>(() => formatter.Time(other));
            Assert.Throws<ArgumentException>(() => formatter.Scores(other));
            Assert.Throws<ArgumentException>(() => formatter.AppendLines(other, new List<LocalizedText>()));
            Assert.Throws<ArgumentException>(() => formatter.Result(other));
            Assert.Throws<ArgumentNullException>(() => formatter.Time(null));
            Assert.Throws<ArgumentNullException>(() => formatter.AppendLines(Status(Create(), null, null), null));
        }

        private static ModeStatus Status(SearchAndDestroy game, Side? ownSide, int? localPlayerId)
        {
            return new ModeStatus(null, null, ownSide, null, false, null, new SearchAndDestroyStatus(game, localPlayerId));
        }

        private static string[] Resolved(IReadOnlyList<LocalizedText> lines)
        {
            return lines.Select(GameText.Resolve).ToArray();
        }

        private static SearchAndDestroy CarrierPlantingOnB()
        {
            var game = Create();
            game.Start();
            game.SetPlanting(_alphaOne, 1);
            game.Tick(1f);
            return game;
        }

        private static void Plant(SearchAndDestroy game, int site)
        {
            var carrier = game.CarrierId ?? throw new InvalidOperationException("nobody carries the bomb");
            game.SetPlanting(carrier, site);
            game.Tick(_plantSeconds);
            game.SetPlanting(carrier, null);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted));
        }

        private static SearchAndDestroy Create(float preparationSeconds = 0f, int winsToWin = 3)
        {
            var game = new SearchAndDestroy(new SearchAndDestroySpec(
                2, _roundSeconds, _plantSeconds, _defuseSeconds, _fuseSeconds, winsToWin, preparationSeconds, 0));
            game.AddPlayer(_alphaOne, Side.Alpha);
            game.AddPlayer(_alphaTwo, Side.Alpha);
            game.AddPlayer(_bravoOne, Side.Bravo);
            game.AddPlayer(_bravoTwo, Side.Bravo);
            return game;
        }
    }
}
