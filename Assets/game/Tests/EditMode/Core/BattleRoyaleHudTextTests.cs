using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BattleRoyaleHudTextTests
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
            Assert.That(GameText.Resolve(BattleRoyaleHudText.SquadsLeft(3, 4)), Is.EqualTo("残り分隊 3/4"));
            Assert.That(GameText.Resolve(BattleRoyaleHudText.Placement(3, 2)), Is.EqualTo("順位 3 位"));
            Assert.That(GameText.Resolve(BattleRoyaleHudText.Placement(null, 2)), Is.EqualTo("順位 2 位以内"));
            Assert.That(GameText.Resolve(BattleRoyaleHudText.Squad(2, 1)), Is.EqualTo("分隊 生存 2 (ダウン 1)"));
            Assert.That(GameText.Resolve(BattleRoyaleHudText.Squad(2, 0)), Is.EqualTo("分隊 生存 2"));
            Assert.That(BattleRoyaleHudText.SquadsLeft(3, 4), Is.EqualTo(new LocalizedText("mode.br.squadsLeft", 3, 4)));
            Assert.That(BattleRoyaleHudText.Squad(2, 1).Key, Is.EqualTo("hud.mode.withDowned"));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudText.SquadsLeft(5, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudText.Placement(0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudText.Placement(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleHudText.Squad(-1, 0));
        }

        [Test]
        public void TheLinesShowSquadsLeftPlacementZoneAndTheOwnSquad()
        {
            var br = Create();
            br.Start();
            br.ReportLethal(_squadmate, _enemy);

            var zoneLine = new LocalizedText("hud.zone.waiting", "00:12");

            var lines = BattleRoyaleHudText.Lines(br, _player, _squadmate, zoneLine, unarmed: true);

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

            var lines = BattleRoyaleHudText.Lines(br, _player, null, default, unarmed: false);

            Assert.That(lines, Does.Contain(HudFormat.Downed(15, 0)));
        }

        [Test]
        public void AnEliminatedSquadSeesItsPlacement()
        {
            var br = Create();
            br.Start();
            br.ReportLethal(_player, _enemy);
            br.ReportLethal(_squadmate, _enemy);
            br.Tick(0f);

            var lines = Resolve(BattleRoyaleHudText.Lines(br, _player, null, default, unarmed: false));
            var scores = BattleRoyaleHudText.Squads(br, new Side(0));

            Assert.That(lines, Does.Contain("残り分隊 2/3"));
            Assert.That(lines, Does.Contain("順位 3 位"));
            Assert.That(GameText.Resolve(scores[0].Text), Is.EqualTo("ALPHA 3 位"));
            Assert.That(scores[0].IsOwn, Is.True);
            Assert.That(GameText.Resolve(scores[1].Text), Is.EqualTo("BRAVO 残り 1"));
            Assert.That(GameText.Resolve(scores[2].Text), Is.EqualTo("SIDE 2 残り 1"));
            Assert.That(scores.Count, Is.EqualTo(3), "the empty fourth squad is not listed");
        }

        [Test]
        public void TheResultNamesTheWinningSquad()
        {
            var br = Create();
            br.Start();
            Assert.That(BattleRoyaleHudText.Result(br).IsEmpty, Is.True);

            br.ReportLethal(_player, _enemy);
            br.ReportLethal(_squadmate, _enemy);
            br.ReportLethal(_otherEnemy, _enemy);
            br.Tick(0f);

            Assert.That(GameText.Resolve(BattleRoyaleHudText.Result(br)), Is.EqualTo("BRAVO の勝ち"));
            Assert.That(BattleRoyaleLog.Placements(br), Is.EqualTo("[Mode] placements winner=1 1:squad=1 2:squad=0 2:squad=2"));
        }

        [Test]
        public void FormatsTheLogLines()
        {
            Assert.That(BattleRoyaleLog.Downed(2, 0), Is.EqualTo("[Mode] downed player=2 by=0"));
            Assert.That(BattleRoyaleLog.Downed(2, null), Is.EqualTo("[Mode] downed player=2 by=-"));
            Assert.That(BattleRoyaleLog.Revived(3, 2), Is.EqualTo("[Mode] revived player=2 by=3"));
            Assert.That(BattleRoyaleLog.BledOut(2, null), Is.EqualTo("[Mode] bledout player=2 credit=-"));
            Assert.That(BattleRoyaleLog.SquadWiped(3, 0), Is.EqualTo("[Mode] squad-wiped player=3 credit=0"));
            Assert.That(BattleRoyaleLog.Eliminated(new Side(2), 3, 2), Is.EqualTo("[Mode] eliminated squad=2 place=3 remaining=2"));
            Assert.That(
                BattleRoyaleLog.Errand(3, BotErrandKind.Collect, new Vector3(4f, 0f, 12f)),
                Is.EqualTo("[Mode] errand player=3 kind=Collect at=(4.00, 0.00, 12.00)"));
            Assert.That(BattleRoyaleLog.Errand(3, BotErrandKind.None, Vector3.Zero), Is.EqualTo("[Mode] errand player=3 kind=None"));
            Assert.That(
                BattleRoyaleLog.Drop(0, 60f, ParachuteOpenVerdict.Open),
                Is.EqualTo("[Mode] drop player=0 height=60.00 parachute=Open"));
            Assert.That(
                BattleRoyaleLog.Drop(0, 60f, ParachuteOpenVerdict.RuleOff),
                Is.EqualTo("[Mode] drop player=0 height=60.00 parachute=RuleOff"));
        }

        /// <summary>4 分隊のうち 3 分隊に人がいる。分隊 0 にプレイヤーと味方、分隊 1 と 2 に 1 人ずつ。</summary>
        private static BattleRoyaleSession Create()
        {
            var br = new BattleRoyaleSession(4, 2, float.PositiveInfinity, ActionRules.Default.WithDowned(Downed.Revive));
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
                resolved[i] = GameText.Resolve(lines[i]);
            }

            return resolved;
        }
    }
}
