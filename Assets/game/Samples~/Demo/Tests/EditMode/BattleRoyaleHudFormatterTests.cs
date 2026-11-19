using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class BattleRoyaleHudFormatterTests
    {
        private const int _player = 0;
        private const int _squadmate = 1;
        private const int _enemy = 10;
        private const int _otherEnemy = 20;
        private static readonly string[] _expectedLines =
        {
            "残り分隊 3/3",
            "順位 3 位以内",
            "エリア縮小まで 00:12",
            "分隊 生存 1 (ダウン 1)",
            "蘇生中 0%",
            "武器なし: 落ちている武器を拾う",
        };

        private static readonly string[] _expectedFormatterLines =
        {
            "残り分隊 3/3",
            "順位 3 位以内",
            "エリア縮小まで 00:12",
            "分隊 生存 2",
        };

        private static readonly string[] _modeBrKeys =
        {
            "mode.br.squadsLeft",
            "mode.br.placementAtLeast",
            "mode.br.squad",
            "mode.br.noWeapon",
        };

        [Test]
        public void FormatsTheSingleLines()
        {
            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.SquadsLeft(3, 4)), Is.EqualTo("残り分隊 3/4"));
            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.Placement(3, 2)), Is.EqualTo("順位 3 位"));
            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.Placement(null, 2)), Is.EqualTo("順位 2 位以内"));
            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.Squad(2, 1)), Is.EqualTo("分隊 生存 2 (ダウン 1)"));
            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.Squad(2, 0)), Is.EqualTo("分隊 生存 2"));
            Assert.That(BattleRoyaleHudFormatter.SquadsLeft(3, 4), Is.EqualTo(new LocalizedText("mode.br.squadsLeft", 3, 4)));
            Assert.That(BattleRoyaleHudFormatter.Squad(2, 1).Key, Is.EqualTo("hud.mode.withDowned"));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudFormatter.SquadsLeft(5, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudFormatter.Placement(0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudFormatter.Placement(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudFormatter.Squad(-1, 0));
        }

        [Test]
        public void TheLinesShowSquadsLeftPlacementZoneAndTheOwnSquad()
        {
            var br = Create();
            br.Start();
            br.ReportLethal(_squadmate, _enemy);

            var zoneLine = new LocalizedText("hud.zone.waiting", "00:12");

            var lines = BattleRoyaleHudFormatter.Lines(br, _player, _squadmate, zoneLine, unarmed: true);

            Assert.That(Resolve(lines), Is.EqualTo(_expectedLines));
            Assert.That(lines[2], Is.EqualTo(zoneLine));
            Assert.That(new[] { lines[0].Key, lines[1].Key, ((LocalizedText)lines[3].Args[0]).Key, lines[5].Key }, Is.EqualTo(_modeBrKeys));
        }

        [Test]
        public void ADownedPlayerSeesTheBleedout()
        {
            var br = Create();
            br.Start();
            br.ReportLethal(_player, _enemy);

            var lines = BattleRoyaleHudFormatter.Lines(br, _player, null, default, unarmed: false);

            Assert.That(lines, Does.Contain(HudText.Downed(15, 0)));
        }

        [Test]
        public void AnEliminatedSquadSeesItsPlacement()
        {
            var br = Create();
            br.Start();
            br.ReportLethal(_player, _enemy);
            br.ReportLethal(_squadmate, _enemy);
            br.Tick(0f);

            var lines = Resolve(BattleRoyaleHudFormatter.Lines(br, _player, null, default, unarmed: false));
            var scores = BattleRoyaleHudFormatter.Squads(br, new Side(0));

            Assert.That(lines, Does.Contain("残り分隊 2/3"));
            Assert.That(lines, Does.Contain("順位 3 位"));
            Assert.That(Localizer.Resolve(scores[0].Text), Is.EqualTo("ALPHA 3 位"));
            Assert.That(scores[0].IsOwn, Is.True);
            Assert.That(Localizer.Resolve(scores[1].Text), Is.EqualTo("BRAVO 残り 1"));
            Assert.That(Localizer.Resolve(scores[2].Text), Is.EqualTo("SIDE 2 残り 1"));
            Assert.That(scores.Count, Is.EqualTo(3), "the empty fourth squad is not listed");
        }

        [Test]
        public void TheResultNamesTheWinningSquad()
        {
            var br = Create();
            br.Start();
            Assert.That(BattleRoyaleHudFormatter.Result(br).IsEmpty, Is.True);

            br.ReportLethal(_player, _enemy);
            br.ReportLethal(_squadmate, _enemy);
            br.ReportLethal(_otherEnemy, _enemy);
            br.Tick(0f);

            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.Result(br)), Is.EqualTo("BRAVO の勝ち"));
            Assert.That(BattleRoyaleLog.Placements(br), Is.EqualTo("[Mode] placements winner=1 1:squad=1 2:squad=0 2:squad=2"));
        }

        [Test]
        public void TheFormatterCountsUpWithoutATimeLimitAndListsTheSquadsAndTheZone()
        {
            var br = Create();
            br.Start();
            br.Tick(5f);
            var zone = new ZoneSchedule(
                Vector2.Zero, 60f, new[] { new ZonePhase(17f, 20f, 30f, 1f) }, 7).Evaluate(5f);
            var status = Status(br, new BattleRoyaleStatus(br, _player, null, zone, isLocalPlayerUnarmed: false), isEnded: false);
            var formatter = new BattleRoyaleHudFormatter();

            Assert.That(Localizer.Resolve(formatter.Time(status)), Is.EqualTo("00:05"), "without a time limit the HUD counts up");
            var scores = formatter.Scores(status);
            Assert.That(Localizer.Resolve(scores[0].Text), Is.EqualTo("ALPHA 残り 2"));
            Assert.That(scores[0].IsOwn, Is.True);
            var lines = new List<LocalizedText>();
            formatter.AppendLines(status, lines);
            Assert.That(Resolve(lines), Is.EqualTo(_expectedFormatterLines));
            Assert.That(formatter.Result(status).IsEmpty, Is.True);
        }

        [Test]
        public void TheFormatterShowsTheRemainingTimeWhenThereIsALimit()
        {
            var br = new BattleRoyaleSession(4, 2, 90f, ActionRules.Default.WithDowned(Downed.Revive));
            br.Mode.AddPlayer(_player, new Side(0));
            br.Mode.AddPlayer(_enemy, new Side(1));
            br.Start();
            br.Tick(30f);

            var status = Status(br, new BattleRoyaleStatus(br, _player, null, null, isLocalPlayerUnarmed: false), isEnded: false);

            Assert.That(Localizer.Resolve(new BattleRoyaleHudFormatter().Time(status)), Is.EqualTo("01:00"));
        }

        [Test]
        public void TheFormatterNamesTheWinnerOnceTheMatchEnds()
        {
            var br = Create();
            br.Start();
            br.ReportLethal(_squadmate, _enemy);
            br.ReportLethal(_enemy, _player);
            br.ReportLethal(_otherEnemy, _player);
            br.Tick(0f);

            var status = Status(br, new BattleRoyaleStatus(br, _player, null, null, isLocalPlayerUnarmed: false), isEnded: true);
            var formatter = new BattleRoyaleHudFormatter();
            var lines = new List<LocalizedText>();
            formatter.AppendLines(status, lines);

            Assert.That(Resolve(lines), Does.Contain("順位 1 位"));
            Assert.That(Localizer.Resolve(formatter.Result(status)), Is.EqualTo("ALPHA の勝ち"));
        }

        [Test]
        public void TheSupplyLinesAnnounceFallingAndWaitingCratesAndPromptAtOne()
        {
            var br = Create();
            br.Start();
            var drops = new[]
            {
                new SupplyDropMark(1, new Vector3(1f, 0f, 2f), true),
                new SupplyDropMark(2, new Vector3(5f, 20f, 2f), false),
                new SupplyDropMark(3, new Vector3(9f, 0f, 2f), true),
            };
            var detail = new BattleRoyaleStatus(br, _player, null, null, false, drops, isLocalPlayerAtSupplyDrop: true, interactKeyLabel: " E ");

            var lines = BattleRoyaleHudFormatter.SupplyLines(detail);

            Assert.That(Resolve(lines), Is.EqualTo(new[] { "補給物資が投下中 (1)", "未開封の補給物資 2", "[E] 補給物資を開ける" }));
            Assert.That(lines[0], Is.EqualTo(new LocalizedText("mode.br.supplyFalling", 1)));
            Assert.That(lines[1], Is.EqualTo(new LocalizedText("mode.br.supplyWaiting", 2)));
            Assert.That(lines[2].Key, Is.EqualTo("mode.br.supplyOpenKey"));
            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.SupplyOpenPrompt(null)), Is.EqualTo("補給物資を開ける"));
            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.SupplyOpenPrompt("  ")), Is.EqualTo("補給物資を開ける"));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudFormatter.SupplyFalling(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudFormatter.SupplyWaiting(0));
            Assert.Throws<ArgumentNullException>(() => BattleRoyaleHudFormatter.SupplyLines(null));
        }

        [Test]
        public void TheFormatterAddsTheSupplyLinesAfterTheSquadLines()
        {
            var br = Create();
            br.Start();
            var drops = new[] { new SupplyDropMark(1, new Vector3(5f, 20f, 2f), false) };
            var status = Status(br, new BattleRoyaleStatus(br, _player, null, null, false, drops), isEnded: false);
            var lines = new List<LocalizedText>();

            new BattleRoyaleHudFormatter().AppendLines(status, lines);

            Assert.That(Resolve(lines), Is.EqualTo(new[] { "残り分隊 3/3", "順位 3 位以内", "分隊 生存 2", "補給物資が投下中 (1)" }));
        }

        [Test]
        public void NoSupplyLinesWithoutCratesOrAfterTheMatch()
        {
            var br = Create();
            br.Start();
            Assert.That(BattleRoyaleHudFormatter.SupplyLines(new BattleRoyaleStatus(br, _player, null, null, false)), Is.Empty);

            var drops = new[] { new SupplyDropMark(1, Vector3.Zero, true) };
            var spectator = new BattleRoyaleStatus(br, null, null, null, false, drops, isLocalPlayerAtSupplyDrop: true);
            Assert.That(spectator.IsLocalPlayerAtSupplyDrop, Is.False, "without a local player there is no one to open it");
            Assert.That(Resolve(BattleRoyaleHudFormatter.SupplyLines(spectator)), Is.EqualTo(new[] { "未開封の補給物資 1" }));

            br.ReportLethal(_squadmate, _enemy);
            br.ReportLethal(_enemy, _player);
            br.ReportLethal(_otherEnemy, _player);
            br.Tick(0f);

            Assert.That(BattleRoyaleHudFormatter.SupplyLines(new BattleRoyaleStatus(br, _player, null, null, false, drops)), Is.Empty);
        }

        [Test]
        public void TheSupplyTextsExistInEveryLanguage()
        {
            var en = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Localization/en/modes-survival");
            var ja = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Localization/ja/modes-survival");
            Assert.That(en, Is.Not.Null);
            Assert.That(ja, Is.Not.Null);
            foreach (var key in new[] { "mode.br.supplyFalling", "mode.br.supplyWaiting", "mode.br.supplyOpen", "mode.br.supplyOpenKey" })
            {
                Assert.That(en.text, Does.Contain("\"" + key + "\""), key);
                Assert.That(ja.text, Does.Contain("\"" + key + "\""), key);
            }
        }

        [Test]
        public void FormatsTheRedeployLines()
        {
            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.RedeployIn(4)), Is.EqualTo("空から復帰まで 4 秒"));
            Assert.That(Localizer.Resolve(BattleRoyaleHudFormatter.RedeploysLeft(2)), Is.EqualTo("復帰 残り 2 回"));
            Assert.That(BattleRoyaleHudFormatter.RedeployIn(4), Is.EqualTo(new LocalizedText("mode.br.redeployIn", 4)));
            Assert.That(BattleRoyaleHudFormatter.RedeploysLeft(0), Is.EqualTo(new LocalizedText("mode.br.redeploysLeft", 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudFormatter.RedeployIn(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudFormatter.RedeploysLeft(-1));
            Assert.Throws<ArgumentNullException>(() => BattleRoyaleHudFormatter.RedeployLines(null));
        }

        [Test]
        public void AWaitingPlayerSeesTheRedeployInsteadOfTheRespawnLine()
        {
            var br = Create(new RedeploySpec(charges: 1, lastPhaseIndex: 1, delaySeconds: 8f));
            br.Start();
            Assert.That(
                Resolve(BattleRoyaleHudFormatter.RedeployLines(new BattleRoyaleStatus(br, _player, null, null, false))),
                Is.EqualTo(new[] { "復帰 残り 1 回" }));

            br.ReportLethal(_player, _enemy);
            br.Tick(ActionRules.DefaultDownedBleedoutSeconds - 5f);
            br.Tick(5f);

            var detail = new BattleRoyaleStatus(br, _player, null, null, false);
            Assert.That(detail.LocalRedeploySeconds, Is.EqualTo(3f));
            Assert.That(detail.LocalRedeploysLeft, Is.EqualTo(0));
            var status = new ModeStatus(null, Status(br, detail, isEnded: false).SideScores, new Side(0), 3f, false, null, detail);
            var lines = new List<LocalizedText>();

            new BattleRoyaleHudFormatter().AppendLines(status, lines);

            Assert.That(Resolve(lines), Is.EqualTo(new[] { "空から復帰まで 3 秒", "復帰 残り 0 回", "残り分隊 3/3", "順位 3 位以内", "分隊 生存 1" }));
            Assert.That(Resolve(lines), Does.Not.Contain(Localizer.Resolve(HudText.Respawn(3))), "the generic respawn line is not shown twice");
        }

        [Test]
        public void NoRedeployLinesWhenItIsOffPastTheLastPhaseOrAfterTheMatch()
        {
            var off = Create();
            off.Start();
            Assert.That(BattleRoyaleHudFormatter.RedeployLines(new BattleRoyaleStatus(off, _player, null, null, false)), Is.Empty);

            var br = Create(new RedeploySpec(charges: 1, lastPhaseIndex: 0, delaySeconds: 8f));
            br.Start();
            br.SetZonePhase(1);
            Assert.That(BattleRoyaleHudFormatter.RedeployLines(new BattleRoyaleStatus(br, _player, null, null, false)), Is.Empty);
            Assert.That(BattleRoyaleHudFormatter.RedeployLines(new BattleRoyaleStatus(br, null, null, null, false)), Is.Empty, "a spectator has no redeploys");

            var ended = Create(new RedeploySpec(charges: 1, lastPhaseIndex: 1, delaySeconds: 8f));
            ended.Start();
            ended.ReportLethal(_enemy, _player);
            ended.ReportLethal(_otherEnemy, _player);
            ended.Tick(0f);
            Assert.That(ended.Mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(BattleRoyaleHudFormatter.RedeployLines(new BattleRoyaleStatus(ended, _player, null, null, false)), Is.Empty);
        }

        [Test]
        public void NoRedeployLinesForAPlayerWhoseSquadHasNoOneLeftToStand()
        {
            var br = Create(new RedeploySpec(charges: 1, lastPhaseIndex: 1, delaySeconds: 8f));
            br.Start();
            br.ReportLethal(_squadmate, _enemy);
            br.ReportLethal(_player, _enemy);
            br.Tick(ActionRules.DefaultDownedBleedoutSeconds);

            var detail = new BattleRoyaleStatus(br, _player, null, null, false);
            Assert.That(detail.LocalRedeploySeconds, Is.Null);
            Assert.That(detail.LocalRedeploysLeft, Is.Null);
            Assert.That(BattleRoyaleHudFormatter.RedeployLines(detail), Is.Empty);
        }

        [Test]
        public void TheRedeployTextsExistInEveryLanguage()
        {
            var en = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Localization/en/modes-survival");
            var ja = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Localization/ja/modes-survival");
            foreach (var key in new[] { "mode.br.redeployIn", "mode.br.redeploysLeft" })
            {
                Assert.That(en.text, Does.Contain("\"" + key + "\""), key);
                Assert.That(ja.text, Does.Contain("\"" + key + "\""), key);
            }
        }

        [Test]
        public void TheZoneLineShowsTheTimeUntilTheNextShrink()
        {
            var schedule = new ZoneSchedule(
                new Vector2(1f, 2f),
                60f,
                new[] { new ZonePhase(30f, 20f, 35f, 1f), new ZonePhase(10f, 5f, 35f, 2.5f) },
                7);

            Assert.That(ZoneHudFormatter.Line(schedule.Evaluate(17.5f)), Is.EqualTo(new LocalizedText("hud.zone.waiting", "00:13")));
            Assert.That(Localizer.Resolve(ZoneHudFormatter.Line(schedule.Evaluate(17.5f))), Is.EqualTo("エリア縮小まで 00:13"));
            Assert.That(Localizer.Resolve(ZoneHudFormatter.Line(schedule.Evaluate(40f))), Is.EqualTo("エリア縮小中 00:10"));
            Assert.That(Localizer.Resolve(ZoneHudFormatter.Line(schedule.Evaluate(100f))), Is.EqualTo("最終エリア"));
        }

        private static ModeStatus Status(BattleRoyaleSession br, BattleRoyaleStatus detail, bool isEnded)
        {
            var mode = br.Mode;
            var scores = new SideScore[mode.Sides.Count];
            for (var i = 0; i < scores.Length; i++)
            {
                scores[i] = new SideScore(mode.Sides[i], mode.GetScore(mode.Sides[i]));
            }

            return new ModeStatus(null, scores, new Side(0), null, isEnded, isEnded ? mode.Winner : null, detail);
        }

        private static BattleRoyaleSession Create(RedeploySpec redeploy = null)
        {
            var br = new BattleRoyaleSession(4, 2, float.PositiveInfinity, ActionRules.Default.WithDowned(Downed.Revive), redeploy);
            br.Mode.AddPlayer(_player, new Side(0));
            br.Mode.AddPlayer(_squadmate, new Side(0));
            br.Mode.AddPlayer(_enemy, new Side(1));
            br.Mode.AddPlayer(_otherEnemy, new Side(2));
            return br;
        }

        private static string[] Resolve(IReadOnlyList<LocalizedText> lines)
        {
            var resolved = new string[lines.Count];
            for (var i = 0; i < lines.Count; i++)
            {
                resolved[i] = Localizer.Resolve(lines[i]);
            }

            return resolved;
        }
    }
}
