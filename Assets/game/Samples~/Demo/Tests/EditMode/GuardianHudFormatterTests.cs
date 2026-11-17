using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class GuardianHudFormatterTests
    {
        private static readonly string[] _leaderLinesOwnBravo = { "BRAVO リーダー 撃破 (復活不可)", "ALPHA リーダー 生存" };
        private static readonly string[] _leaderLinesOwnAlpha = { "あなたがリーダー", "ALPHA リーダー 生存", "BRAVO リーダー 生存" };
        private static readonly string[] _fallenLinesOwnBravo = { "リーダーが倒れたため復活なし", "BRAVO リーダー 撃破 (復活不可)", "ALPHA リーダー 生存" };

        [Test]
        public void TheLeaderLinesListTheOwnLeaderFirst()
        {
            var session = Playing(out var leaders);
            session.ReportKill(0, 1);

            var ownBravo = GuardianHudFormatter.Leaders(leaders, Side.Bravo);

            Assert.That(Resolve(ownBravo), Is.EqualTo(_leaderLinesOwnBravo));
            Assert.That(ownBravo[0], Is.EqualTo(new LocalizedText("mode.guardian.leader", ModeHudText.SideName(Side.Bravo), GuardianHudFormatter.Fallen)));
            Assert.That(Localizer.Resolve(GuardianHudFormatter.Leaders(leaders, null)[0]), Is.EqualTo("ALPHA リーダー 生存"));
            Assert.That(Localizer.Resolve(GuardianHudFormatter.YouAreLeader), Is.EqualTo("あなたがリーダー"));
            Assert.That(Localizer.Resolve(GuardianHudFormatter.NoRespawnLeaderDown), Is.EqualTo("リーダーが倒れたため復活なし"));
        }

        [Test]
        public void TheLocalLeaderIsToldTheyAreTheLeader()
        {
            Playing(out var leaders);

            var lines = Lines(new GuardianStatus(leaders, 0, false), Side.Alpha);

            Assert.That(Resolve(lines), Is.EqualTo(_leaderLinesOwnAlpha));
        }

        [Test]
        public void AnEliminatedPlayerWhoseLeaderFellIsToldTheyWillNotComeBack()
        {
            var session = Playing(out var leaders);
            session.ReportKill(0, 1);

            var lines = Lines(new GuardianStatus(leaders, 3, true), Side.Bravo);

            Assert.That(Resolve(lines), Is.EqualTo(_fallenLinesOwnBravo));
            Assert.That(Resolve(Lines(new GuardianStatus(leaders, 3, false), Side.Bravo)), Is.EqualTo(_leaderLinesOwnBravo), "a standing player is not told");
        }

        [Test]
        public void TheResultNamesTheWinnerOnceTheMatchEnds()
        {
            Playing(out var leaders);
            var formatter = new GuardianHudFormatter();
            var ended = new ModeStatus(0f, null, Side.Alpha, null, true, Side.Bravo, new GuardianStatus(leaders, 0, false));
            var drawn = new ModeStatus(0f, null, Side.Alpha, null, true, null, new GuardianStatus(leaders, 0, false));

            Assert.That(Localizer.Resolve(formatter.Result(ended)), Is.EqualTo("BRAVO の勝ち"));
            Assert.That(Localizer.Resolve(formatter.Result(drawn)), Is.EqualTo(Localizer.Resolve(HudText.Draw)));
            Assert.That(Localizer.Resolve(formatter.Time(ended)), Is.EqualTo("00:00"));
        }

        private static ModeSession Playing(out GuardianRespawn leaders)
        {
            var session = GuardianMode.CreateSession(60f, 3f, _ => 0, out leaders);
            for (var i = 0; i < 6; i++)
            {
                session.AddPlayer(i, i % 2 == 0 ? Side.Alpha : Side.Bravo);
            }

            session.Start();
            return session;
        }

        private static List<LocalizedText> Lines(GuardianStatus detail, Side ownSide)
        {
            var status = new ModeStatus(30f, null, ownSide, null, false, null, detail);
            var lines = new List<LocalizedText>();
            new GuardianHudFormatter().AppendLines(status, lines);
            return lines;
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
