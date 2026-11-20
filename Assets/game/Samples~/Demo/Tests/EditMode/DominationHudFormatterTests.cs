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
    public sealed class DominationHudFormatterTests
    {
        private static readonly string[] _expectedPointLines = { "A ALPHA", "B --", "C BRAVO" };

        [Test]
        public void TheHudLinesNameEveryPointAndItsOwner()
        {
            Assert.That(DominationHudFormatter.HudLines(Detail()).Select(Localizer.Resolve), Is.EqualTo(_expectedPointLines));
            Assert.Throws<ArgumentNullException>(() => DominationHudFormatter.HudLines(null));
        }

        [Test]
        public void TheRespawnCountdownComesBeforeThePointLines()
        {
            var lines = new List<LocalizedText>();
            new DominationHudFormatter().AppendLines(Status(respawn: 2.5f), lines);

            Assert.That(lines.Select(Localizer.Resolve), Is.EqualTo(new[] { "リスポーンまで 3 秒" }.Concat(_expectedPointLines)));
        }

        [Test]
        public void TheModeHudShowsTheFlooredScoresAndThePointLines()
        {
            var owner = new GameObject(nameof(DominationHudFormatterTests));
            try
            {
                var view = owner.AddComponent<ModeHudView>();
                view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
                var status = Status();

                Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<DominationHudFormatter>());
                view.Show(status);

                Assert.That(view.TimeText, Is.EqualTo("01:30"));
                Assert.That(view.ScoreTexts, Is.EqualTo(new[] { "ALPHA 12", "BRAVO 3" }));
                Assert.That(view.Lines, Is.EqualTo(_expectedPointLines));
                Assert.That(view.ResultText, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static DominationStatus Detail()
        {
            var points = new[]
            {
                new ControlPointStatus(Team.Alpha, 100, false),
                new ControlPointStatus(null, -40, true),
                new ControlPointStatus(Team.Bravo, -100, false),
            };
            return new DominationStatus(12.9f, 3f, 90f, points, false, null);
        }

        private static ModeStatus Status(float? respawn = null)
        {
            var detail = Detail();
            var sideScores = new[] { new SideScore(Side.Alpha, detail.AlphaScore), new SideScore(Side.Bravo, detail.BravoScore) };
            return new ModeStatus(detail.RemainingSeconds, sideScores, Side.Alpha, respawn, false, null, detail);
        }
    }
}
