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
    public sealed class ExecutionHudTextTests
    {
        private const int _alphaOne = 0;
        private const int _bravoOne = 1;
        private const int _bravoTwo = 2;

        private static readonly string[] _expectedPreparingLines = { "ラウンド 1 (2 本先取)", "開始まで 3 秒", "ALPHA 残り 1", "BRAVO 残り 2" };

        private static readonly string[] _expectedOwnSideFirst = { "ラウンド 1 (2 本先取)", "BRAVO 残り 2 (ダウン 1)", "ALPHA 残り 1" };

        private static readonly string[] _expectedLocalDowned =
        {
            "ラウンド 1 (2 本先取)", "ALPHA 残り 1 (ダウン 1)", "BRAVO 残り 2", "ダウン中 あと 15 秒",
        };

        private static readonly string[] _expectedExecuting =
        {
            "ラウンド 1 (2 本先取)", "ALPHA 残り 1", "BRAVO 残り 2 (ダウン 1)", "とどめ 66%",
        };

        private static readonly string[] _expectedAfterRound =
        {
            "ラウンド 2 (2 本先取)", "前のラウンド: ALPHA の勝ち", "開始まで 3 秒", "ALPHA 残り 1", "BRAVO 残り 2",
        };

        private static readonly string[] _expectedFromView =
        {
            "ラウンド 2 (2 本先取)", "BRAVO 残り 2 (ダウン 1)", "ALPHA 残り 1",
        };

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
        public void TheSmallTextsReadNaturally()
        {
            Assert.That(GameText.Resolve(ExecutionHudText.Round(2, 3)), Is.EqualTo("ラウンド 2 (3 本先取)"));
            Assert.That(GameText.Resolve(ExecutionHudText.Preparation(3)), Is.EqualTo("開始まで 3 秒"));
            Assert.That(GameText.Resolve(ExecutionHudText.RoundResult(Side.Bravo)), Is.EqualTo("前のラウンド: BRAVO の勝ち"));
            Assert.That(GameText.Resolve(ExecutionHudText.RoundResult(null)), Is.EqualTo("前のラウンド: 引き分け"));
            Assert.That(GameText.Resolve(ExecutionHudText.Remaining(Side.Bravo, 3, 1)), Is.EqualTo("BRAVO 残り 3 (ダウン 1)"));
            Assert.That(GameText.Resolve(ExecutionHudText.Remaining(Side.Alpha, 2, 0)), Is.EqualTo("ALPHA 残り 2"));
            Assert.That(GameText.Resolve(ExecutionHudText.Executing(40)), Is.EqualTo("とどめ 40%"));
        }

        [Test]
        public void TheTextsAreKeysOfTheRoundModeTable()
        {
            Assert.That(ExecutionHudText.Round(2, 3), Is.EqualTo(new LocalizedText("mode.execution.round", 2, 3)));
            Assert.That(ExecutionHudText.Preparation(3), Is.EqualTo(new LocalizedText("mode.execution.startsIn", 3)));
            Assert.That(ExecutionHudText.RoundResult(null), Is.EqualTo(new LocalizedText("mode.execution.previousRound", HudText.Draw)));
            Assert.That(ExecutionHudText.Executing(40), Is.EqualTo(new LocalizedText("mode.execution.executing", 40)));
        }

        [Test]
        public void TheSmallTextsRejectInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Round(0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Round(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Preparation(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Remaining(Side.Alpha, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Remaining(Side.Alpha, 1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Executing(101));
            Assert.Throws<ArgumentNullException>(() => ExecutionHudText.Lines(null, null, null));
            Assert.Throws<ArgumentNullException>(() => ExecutionHudText.RoundWins(null, null));
            Assert.Throws<ArgumentNullException>(() => ExecutionHudText.Result(null));
        }

        [Test]
        public void WhilePreparingTheLinesCountDownToTheStart()
        {
            var execution = Create();
            execution.Start();

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _alphaOne, null)), Is.EqualTo(_expectedPreparingLines));
        }

        [Test]
        public void TheOwnSideComesFirstWithItsDowned()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            execution.ReportLethal(_bravoOne, _alphaOne);

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _bravoTwo, null)), Is.EqualTo(_expectedOwnSideFirst));
        }

        [Test]
        public void ADownedLocalPlayerSeesTheBleedout()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            execution.ReportLethal(_alphaOne, _bravoOne);

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _alphaOne, null)), Is.EqualTo(_expectedLocalDowned));
        }

        [Test]
        public void TheLocalPlayerSeesTheExecuteProgress()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            execution.ReportLethal(_bravoOne, _alphaOne);
            execution.Interact(_alphaOne, _bravoOne, 1f);

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _alphaOne, _bravoOne)), Is.EqualTo(_expectedExecuting));
        }

        [Test]
        public void TheNextPreparationShowsThePreviousRound()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            WinRoundForAlpha(execution);

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _alphaOne, null)), Is.EqualTo(_expectedAfterRound));
        }

        [Test]
        public void TheScoresAreRoundWinsWithTheOwnSideFirst()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            WinRoundForAlpha(execution);

            var scores = ExecutionHudText.RoundWins(execution, Side.Bravo);

            Assert.That(scores.Count, Is.EqualTo(2));
            Assert.That(GameText.Resolve(scores[0].Text), Is.EqualTo("BRAVO 0"));
            Assert.That(scores[0].IsOwn, Is.True);
            Assert.That(scores[0].IsLeading, Is.False);
            Assert.That(GameText.Resolve(scores[1].Text), Is.EqualTo("ALPHA 1"));
            Assert.That(scores[1].IsLeading, Is.True);
        }

        [Test]
        public void TheResultAppearsOnlyWhenTheMatchEnds()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            WinRoundForAlpha(execution);

            Assert.That(ExecutionHudText.Result(execution).IsEmpty, Is.True, "one round is not the match");

            execution.Tick(3f);
            WinRoundForAlpha(execution);

            Assert.That(GameText.Resolve(ExecutionHudText.Result(execution)), Is.EqualTo("ALPHA の勝ち"));
        }

        [Test]
        public void TheFormatterIsRegisteredForTheExecutionStatus()
        {
            var status = Status(Create(), Side.Alpha, _alphaOne, null);

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<ExecutionHudFormatter>());
        }

        [Test]
        public void TheFormatterReadsTheSessionTheLocalPlayerAndTheInteractTarget()
        {
            var execution = ExecutingAtFiftySeconds();
            var status = Status(execution, Side.Alpha, _alphaOne, _bravoOne);
            var formatter = new ExecutionHudFormatter();

            Assert.That(formatter.Time(status), Is.EqualTo(LocalizedText.Literal("00:50")));
            var scores = formatter.Scores(status);
            Assert.That(scores.Select(score => GameText.Resolve(score.Text)).ToArray(), Is.EqualTo(new[] { "ALPHA 0", "BRAVO 0" }));
            Assert.That(scores[0].IsOwn, Is.True);
            var lines = new List<LocalizedText>();
            formatter.AppendLines(status, lines);
            Assert.That(Resolved(lines), Is.EqualTo(_expectedExecuting));
            Assert.That(formatter.Result(status).IsEmpty, Is.True);
        }

        [Test]
        public void TheModeHudShowsTheExecutionStatus()
        {
            var execution = ExecutingAtFiftySeconds();
            _owner = new GameObject(nameof(TheModeHudShowsTheExecutionStatus));
            var view = _owner.AddComponent<ModeHudView>();
            view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());

            view.Show(Status(execution, Side.Alpha, _alphaOne, _bravoOne));

            Assert.That(view.TimeText, Is.EqualTo("00:50"));
            Assert.That(view.ScoreTexts, Is.EqualTo(new[] { "ALPHA 0", "BRAVO 0" }));
            Assert.That(view.Lines, Is.EqualTo(_expectedExecuting));
            Assert.That(view.ResultText, Is.Empty);
        }

        // ネット対戦のクライアント (ClientExecutionStatus) は ExecutionSession を持たず、スナップショットから作った IExecutionView を渡す
        [Test]
        public void TheFormatterReadsAViewWithoutASession()
        {
            var view = new FixedView();
            var status = new ModeStatus(null, null, Side.Bravo, null, false, null, ExecutionStatus.FromView(view, _bravoTwo, null));
            var formatter = new ExecutionHudFormatter();

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<ExecutionHudFormatter>());
            Assert.That(formatter.Time(status), Is.EqualTo(LocalizedText.Literal("00:42")));
            var scores = formatter.Scores(status);
            Assert.That(scores.Select(score => GameText.Resolve(score.Text)).ToArray(), Is.EqualTo(new[] { "BRAVO 0", "ALPHA 1" }));
            var lines = new List<LocalizedText>();
            formatter.AppendLines(status, lines);
            Assert.That(Resolved(lines), Is.EqualTo(_expectedFromView));
            Assert.That(formatter.Result(status).IsEmpty, Is.True);

            view.MatchWinner = Side.Alpha;
            Assert.That(GameText.Resolve(formatter.Result(status)), Is.EqualTo("ALPHA の勝ち"));
        }

        // ネット対戦のクライアントは、スナップショットに載ったとどめの進み具合と自分が刺している相手 (InteractTargetId) を渡す
        [Test]
        public void TheExecuteProgressFromAViewIsShown()
        {
            var view = new FixedView { ExecuteProgress = 0.455f };
            var status = new ModeStatus(null, null, Side.Alpha, null, false, null, ExecutionStatus.FromView(view, _alphaOne, _bravoOne));
            var lines = new List<LocalizedText>();

            new ExecutionHudFormatter().AppendLines(status, lines);

            Assert.That(Resolved(lines), Is.EqualTo(new[] { "ラウンド 2 (2 本先取)", "ALPHA 残り 1", "BRAVO 残り 2 (ダウン 1)", "とどめ 45%" }));
        }

        [Test]
        public void TheStatusFromAViewHasNoSession()
        {
            var view = new FixedView();
            var status = ExecutionStatus.FromView(view, _bravoTwo, _bravoOne);

            Assert.That(status.View, Is.SameAs(view));
            Assert.That(status.Session, Is.Null);
            Assert.That(status.LocalPlayerId, Is.EqualTo(_bravoTwo));
            Assert.That(status.InteractTargetId, Is.EqualTo(_bravoOne));
            Assert.Throws<ArgumentNullException>(() => ExecutionStatus.FromView(null, null, null));

            var execution = Create();
            Assert.That(ExecutionStatus.FromView(execution, null, null).Session, Is.SameAs(execution));
            Assert.That(new ExecutionStatus(execution, null, null).View, Is.SameAs(execution));
        }

        [Test]
        public void TheFormatterRejectsAnotherDetail()
        {
            var formatter = new ExecutionHudFormatter();
            var other = new ModeStatus(null, null, null, null, false, null, new object());

            Assert.Throws<ArgumentException>(() => formatter.Time(other));
            Assert.Throws<ArgumentException>(() => formatter.Scores(other));
            Assert.Throws<ArgumentException>(() => formatter.AppendLines(other, new List<LocalizedText>()));
            Assert.Throws<ArgumentException>(() => formatter.Result(other));
            Assert.Throws<ArgumentNullException>(() => formatter.Time(null));
            Assert.Throws<ArgumentNullException>(() => formatter.AppendLines(Status(Create(), null, null, null), null));
        }

        private static string[] Resolved(IReadOnlyList<LocalizedText> lines)
        {
            return lines.Select(GameText.Resolve).ToArray();
        }

        private static ModeStatus Status(ExecutionSession execution, Side? ownSide, int? localPlayerId, int? interactTargetId)
        {
            return new ModeStatus(
                null, null, ownSide, null, false, null, new ExecutionStatus(execution, localPlayerId, interactTargetId));
        }

        private static ExecutionSession ExecutingAtFiftySeconds()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            execution.Tick(10f);
            execution.ReportLethal(_bravoOne, _alphaOne);
            execution.Interact(_alphaOne, _bravoOne, 1f);
            return execution;
        }

        private static ExecutionSession Create()
        {
            var execution = new ExecutionSession(2, 3f, 60f, ActionRules.Default.WithDowned(Downed.Revive), 1.5f);
            execution.Mode.AddPlayer(_alphaOne, Side.Alpha);
            execution.Mode.AddPlayer(_bravoOne, Side.Bravo);
            execution.Mode.AddPlayer(_bravoTwo, Side.Bravo);
            return execution;
        }

        // ラウンド 2 の Live で 42 秒残り。Alpha が 1 勝、Bravo の 1 がダウン中
        private sealed class FixedView : IExecutionView
        {
            public Side? MatchWinner { get; set; }

            public RoundPhase Phase => RoundPhase.Live;

            public int RoundNumber => 2;

            public int WinsToWin => 2;

            public float PreparationRemaining => 0f;

            public float RoundRemaining => 42f;

            public bool HasFinishedRound => true;

            public Side? LastRoundWinner => Side.Alpha;

            public Side? Winner => MatchWinner;

            public int GetRoundWins(Side side) => side == Side.Alpha ? 1 : 0;

            public bool HasPlayer(int playerId) => playerId == _alphaOne || playerId == _bravoOne || playerId == _bravoTwo;

            public Side GetSide(int playerId) => playerId == _alphaOne ? Side.Alpha : Side.Bravo;

            public int CountRemaining(Side side) => side == Side.Alpha ? 1 : 2;

            public int CountDowned(Side side) => side == Side.Bravo ? 1 : 0;

            public bool IsDowned(int playerId) => playerId == _bravoOne;

            public float? GetBleedoutRemaining(int playerId) => playerId == _bravoOne ? 10f : (float?)null;

            public float GetReviveProgress(int playerId) => 0f;

            public float ExecuteProgress { get; set; }

            public float GetExecuteProgress(int playerId) => playerId == _bravoOne ? ExecuteProgress : 0f;
        }

        private static void WinRoundForAlpha(ExecutionSession execution)
        {
            execution.ReportLethal(_bravoOne, _alphaOne);
            execution.Interact(_alphaOne, _bravoOne, 1.5f);
            execution.ReportLethal(_bravoTwo, _alphaOne);
            execution.Interact(_alphaOne, _bravoTwo, 1.5f);
            execution.Tick(0f);
        }
    }
}
