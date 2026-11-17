using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class KillConfirmedHudFormatterTests
    {
        private const int Alpha1 = 1;
        private const int Alpha2 = 2;
        private const int Bravo1 = 3;
        private const int Bravo2 = 4;

        private static readonly string[] _expectedHudLines = { "確定 ALPHA 1 / BRAVO 0", "阻止 ALPHA 0 / BRAVO 1", "落ちているタグ 1" };

        [Test]
        public void EachLineNamesBothSides()
        {
            Assert.That(Localizer.Resolve(KillConfirmedHudFormatter.Confirmed(3, 2)), Is.EqualTo("確定 ALPHA 3 / BRAVO 2"));
            Assert.That(Localizer.Resolve(KillConfirmedHudFormatter.Denied(1, 0)), Is.EqualTo("阻止 ALPHA 1 / BRAVO 0"));
            Assert.That(Localizer.Resolve(KillConfirmedHudFormatter.TagsOnField(2)), Is.EqualTo("落ちているタグ 2"));
        }

        [Test]
        public void TheHudShowsConfirmsDenialsAndTagsOnTheField()
        {
            var mode = Played();

            Assert.That(KillConfirmedHudFormatter.HudLines(mode).Select(Localizer.Resolve), Is.EqualTo(_expectedHudLines));
            Assert.Throws<ArgumentNullException>(() => KillConfirmedHudFormatter.HudLines(null));
        }

        [Test]
        public void TheRespawnCountdownComesBeforeTheTagLines()
        {
            var mode = Played();
            var status = new ModeStatus(120f, null, Side.Alpha, 0.5f, false, null, mode);

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<KillConfirmedHudFormatter>());
            var lines = new List<LocalizedText>();
            ModeHudFormatters.For(status).AppendLines(status, lines);

            Assert.That(lines.Select(Localizer.Resolve), Is.EqualTo(new[] { "リスポーンまで 1 秒" }.Concat(_expectedHudLines)));
        }

        private static KillConfirmed Played()
        {
            var mode = new KillConfirmed(new KillConfirmedSpec(10, 300f, 3f, 10f));
            mode.Session.AddPlayer(Alpha1, Side.Alpha);
            mode.Session.AddPlayer(Alpha2, Side.Alpha);
            mode.Session.AddPlayer(Bravo1, Side.Bravo);
            mode.Session.AddPlayer(Bravo2, Side.Bravo);
            mode.Session.Start();
            var confirmed = mode.DropTag(Bravo1);
            mode.Collect(confirmed.Id, Alpha1);
            var denied = mode.DropTag(Bravo2);
            mode.Collect(denied.Id, Bravo1);
            mode.DropTag(Alpha1);
            return mode;
        }
    }
}
