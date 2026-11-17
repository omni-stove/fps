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
    public sealed class HardpointHudFormatterTests
    {
        private const int AlphaId = 1;
        private const int BravoId = 2;

        private static readonly string[] _expectedHeldHudLines = { "拠点 A (次は B まで 9 秒)", "ALPHA が確保中" };

        [Test]
        public void TheLinesNameThePointsByLetter()
        {
            Assert.That(Localizer.Resolve(HardpointHudFormatter.ActivePoint(1, 2, 22.3f)), Is.EqualTo("拠点 B (次は C まで 23 秒)"));
            Assert.That(Localizer.Resolve(HardpointHudFormatter.Status(HardpointStatus.Held, Side.Bravo)), Is.EqualTo("BRAVO が確保中"));
            Assert.That(HardpointHudFormatter.Status(HardpointStatus.Contested, null), Is.EqualTo(HardpointHudFormatter.Contested));
            Assert.That(Localizer.Resolve(HardpointHudFormatter.Contested), Is.EqualTo("争奪中"));
            Assert.That(HardpointHudFormatter.Status(HardpointStatus.Empty, null), Is.EqualTo(HardpointHudFormatter.Empty));
            Assert.That(Localizer.Resolve(HardpointHudFormatter.Empty), Is.EqualTo("拠点は無人"));
            Assert.Throws<ArgumentException>(() => HardpointHudFormatter.Status(HardpointStatus.Held, null));
        }

        [Test]
        public void TheHudLinesReadTheHardpoint()
        {
            var hardpoint = HeldByAlpha();

            Assert.That(HardpointHudFormatter.HudLines(hardpoint).Select(Localizer.Resolve), Is.EqualTo(_expectedHeldHudLines));
            Assert.Throws<ArgumentNullException>(() => HardpointHudFormatter.HudLines(null));
        }

        [Test]
        public void TheRespawnCountdownComesBeforeThePointLines()
        {
            var lines = new List<LocalizedText>();
            new HardpointHudFormatter().AppendLines(Status(HeldByAlpha(), respawn: 2.5f), lines);

            Assert.That(lines.Select(Localizer.Resolve), Is.EqualTo(new[] { "リスポーンまで 3 秒" }.Concat(_expectedHeldHudLines)));
        }

        [Test]
        public void TheModeHudShowsTheHardpointLinesUnderTheScores()
        {
            var owner = new GameObject(nameof(HardpointHudFormatterTests));
            try
            {
                var view = owner.AddComponent<ModeHudView>();
                view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
                var hardpoint = HeldByAlpha();
                var status = Status(hardpoint);

                Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<HardpointHudFormatter>());
                view.Show(status);

                Assert.That(view.TimeText, Is.EqualTo("04:59"));
                Assert.That(view.ScoreTexts, Is.EqualTo(new[] { "ALPHA 1", "BRAVO 0" }));
                Assert.That(view.Lines, Is.EqualTo(_expectedHeldHudLines));
                Assert.That(view.ResultText, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static Hardpoint HeldByAlpha()
        {
            var hardpoint = new Hardpoint(new HardpointSpec(4, 10f, 1f, 1000, 300f, 1f));
            hardpoint.Session.AddPlayer(AlphaId, Side.Alpha);
            hardpoint.Session.AddPlayer(BravoId, Side.Bravo);
            hardpoint.Session.Start();
            hardpoint.SetPlayerPoint(AlphaId, 0);
            hardpoint.Session.Tick(1f);
            return hardpoint;
        }

        private static ModeStatus Status(Hardpoint hardpoint, float? respawn = null)
        {
            var session = hardpoint.Session;
            var sideScores = session.Sides.Select(side => new SideScore(side, session.GetScore(side))).ToArray();
            return new ModeStatus(session.RemainingSeconds, sideScores, Side.Alpha, respawn, false, null, hardpoint);
        }
    }
}
