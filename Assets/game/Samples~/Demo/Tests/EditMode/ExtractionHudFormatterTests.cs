using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class ExtractionHudFormatterTests
    {
        private const string _ammo = "00000000-0000-4000-8000-000000000301";
        private const string _north = "north";

        private static readonly string[] _waitingLines = { "脱出地点が開くまで 10 秒", "所持品 0" };
        private static readonly string[] _holdingLines = { "開いている脱出地点 1", "脱出まで 3 秒", "所持品 4", "[E] 箱を開ける" };
        private static readonly string[] _closedLines = { "開いている脱出地点 1", "この脱出地点は閉じている", "所持品 0" };

        [Test]
        public void TheSingleLinesReadAsDocumented()
        {
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.ExitsOpenIn(9.2f)), Is.EqualTo("脱出地点が開くまで 10 秒"));
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.ExitsOpen(2)), Is.EqualTo("開いている脱出地点 2"));
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.Extracting(0.4f)), Is.EqualTo("脱出まで 1 秒"));
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.Carrying(-1)), Is.EqualTo("所持品 0"));
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.ClosedExit), Is.EqualTo("この脱出地点は閉じている"));
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.OpenPrompt(" E ")), Is.EqualTo("[E] 箱を開ける"));
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.OpenPrompt(null)), Is.EqualTo("箱を開ける"));
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.OutcomeTitle(ExtractionOutcome.Extracted)), Is.EqualTo("脱出成功"));
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.OutcomeTitle(ExtractionOutcome.Died)), Is.EqualTo("撃破"));
            Assert.That(Localizer.Resolve(ExtractionHudFormatter.OutcomeTitle(ExtractionOutcome.TimedOut)), Is.EqualTo("時間切れ"));

            var english = Localizer.TableOf(GameLanguage.English);
            Assert.That(english.Resolve(ExtractionHudFormatter.Extracting(2.5f)), Is.EqualTo("Extracting in 3s"));
            Assert.That(english.Resolve(ExtractionHudFormatter.OpenPrompt("E")), Is.EqualTo("[E] Open container"));
            Assert.That(english.Resolve(ExtractionHudFormatter.OutcomeTitle(ExtractionOutcome.Died)), Is.EqualTo("KILLED IN ACTION"));
        }

        [Test]
        public void BeforeTheExitsOpenTheLinesCountDown()
        {
            var session = Session(openAfterSeconds: 10f);
            var status = Status(new ExtractionStatus(session, false, false, "E"));

            Assert.That(Resolve(Lines(status)), Is.EqualTo(_waitingLines));
            Assert.That(Localizer.Resolve(new ExtractionHudFormatter().Time(status)), Is.EqualTo("01:00"), "the clock counts the raid down");
            Assert.That(new ExtractionHudFormatter().Scores(status), Is.Empty, "the raid has no side scores");
        }

        [Test]
        public void HoldingTheExitShowsTheTimeLeftAndTheOffer()
        {
            var session = Session();
            session.Pick(_ammo, 4);
            session.Tick(2f, _north);

            Assert.That(Resolve(Lines(Status(new ExtractionStatus(session, false, true, "E")))), Is.EqualTo(_holdingLines));
        }

        [Test]
        public void StandingAtAClosedExitSaysSo()
        {
            var session = Session();
            session.Tick(1f, "south");

            Assert.That(Resolve(Lines(Status(new ExtractionStatus(session, true, false, "E")))), Is.EqualTo(_closedLines));
        }

        [Test]
        public void TheEndSummarisesWhatWasKeptAndLost()
        {
            var extracted = Session();
            extracted.Pick(_ammo, 4);
            extracted.Tick(5f, _north);
            var extractedStatus = Status(new ExtractionStatus(extracted, false, false, "E"), true, Side.Alpha);
            Assert.That(Resolve(Lines(extractedStatus)), Is.EqualTo(new[] { "脱出成功: 4 個を持ち帰った" }));
            Assert.That(Localizer.Resolve(new ExtractionHudFormatter().Result(extractedStatus)), Is.EqualTo("脱出成功"));

            var died = Session();
            died.Pick(_ammo, 4);
            died.Die();
            var diedStatus = Status(new ExtractionStatus(died, false, true, "E"), true, Side.Bravo);
            Assert.That(Resolve(Lines(diedStatus)), Is.EqualTo(new[] { "撃破された: 4 個を失った" }), "a finished raid offers no crate");
            Assert.That(Localizer.Resolve(new ExtractionHudFormatter().Result(diedStatus)), Is.EqualTo("撃破"));

            var timedOut = Session();
            timedOut.Tick(60f, null);
            Assert.That(Resolve(Lines(Status(new ExtractionStatus(timedOut, false, false, "E"), true, Side.Bravo))), Is.EqualTo(new[] { "時間切れ: 0 個を失った" }));
        }

        [Test]
        public void AFullStashIsNamedInTheSummary()
        {
            var session = new ExtractionSession(Spec(0f), new Stash(1, Catalog()), new[] { _north });
            session.Pick(_ammo, 70);
            session.Tick(5f, _north);

            var lines = Resolve(Lines(Status(new ExtractionStatus(session, false, false, null), true, Side.Alpha)));

            Assert.That(lines, Is.EqualTo(new[] { "脱出成功: 60 個を持ち帰り、10 個を失った (倉庫が一杯)" }));
        }

        [Test]
        public void ADeathWithAFullStashNamesWhatWasLost()
        {
            var spec = new ExtractionSpec(60f, 5f, 1, 0f, true);
            var session = new ExtractionSession(spec, new Stash(1, Catalog()), new[] { _north });
            session.Pick(_ammo, 70);
            session.Die();

            var lines = Resolve(Lines(Status(new ExtractionStatus(session, false, false, null), true, Side.Bravo)));

            Assert.That(lines, Is.EqualTo(new[] { "撃破された: 60 個は持ち帰り、10 個を失った" }));
        }

        [Test]
        public void TheFormatterIsRegisteredForTheStatus()
        {
            var status = Status(new ExtractionStatus(Session(), false, false, null));

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<ExtractionHudFormatter>());
        }

        private static ExtractionSpec Spec(float openAfterSeconds)
        {
            return new ExtractionSpec(60f, 5f, 1, openAfterSeconds);
        }

        private static ItemCatalog Catalog()
        {
            return new ItemCatalog(new[] { new ItemSpec(_ammo, maxStack: 60) }, InventoryRule.Default);
        }

        private static ExtractionSession Session(float openAfterSeconds = 0f)
        {
            return new ExtractionSession(Spec(openAfterSeconds), new Stash(10, Catalog()), new[] { _north });
        }

        private static ModeStatus Status(ExtractionStatus detail, bool isEnded = false, Side? winner = null)
        {
            return new ModeStatus(null, null, Side.Alpha, null, isEnded, winner, detail);
        }

        private static List<LocalizedText> Lines(ModeStatus status)
        {
            var lines = new List<LocalizedText>();
            new ExtractionHudFormatter().AppendLines(status, lines);
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
