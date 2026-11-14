using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PingLogTests
    {
        private static readonly Vector3 _point = new Vector3(1f, 1.1f, 10f);

        [Test]
        public void RuleShowsTheRuleAndTheSpec()
        {
            Assert.That(
                PingLog.Rule(Ping.On, new PingSpec(6f, 3, 150f)),
                Is.EqualTo("[Ping] rule=On life=6.00 max=3 distance=150.0"));
            Assert.Throws<ArgumentNullException>(() => PingLog.Rule(Ping.On, null));
        }

        [Test]
        public void PlacedNamesTheMarkerTheTargetAndWhatItReplaced()
        {
            var board = new PingBoard(new PingSpec(lifetimeSeconds: 6f, maxMarkers: 1));
            var first = board.Place(PingHit.Enemy(0, _point), 42, 0f);
            var second = board.Place(PingHit.Location(_point), null, 1f);

            Assert.That(
                PingLog.Placed(first, "Dummy 0"),
                Is.EqualTo("[Ping] placed id=1 kind=Enemy target=Dummy_0 at=(1.00, 1.10, 10.00) life=6.00 refreshed=false replaced=none"));
            Assert.That(
                PingLog.Placed(second, null),
                Is.EqualTo("[Ping] placed id=2 kind=Location target=none at=(1.00, 1.10, 10.00) life=6.00 refreshed=false replaced=1"));
        }

        [Test]
        public void TheOtherLinesStartWithTheTagAndTheMarker()
        {
            var marker = new PingMarker(3, PingKind.Enemy, 42, _point, false, 0f, 6f);

            Assert.That(PingLog.Refused(PingRefusal.RuleOff), Is.EqualTo("[Ping] refused reason=RuleOff"));
            Assert.That(PingLog.Lost(marker, "Dummy_0"), Is.EqualTo("[Ping] lost id=3 target=Dummy_0 at=(1.00, 1.10, 10.00)"));
            Assert.That(PingLog.Expired(marker), Is.EqualTo("[Ping] expired id=3 kind=Enemy at=(1.00, 1.10, 10.00) tracking=false"));
            Assert.That(PingLog.TargetGone(marker, ""), Is.EqualTo("[Ping] removed id=3 reason=TargetGone target=none"));
            Assert.That(PingLog.Cleared(2), Is.EqualTo("[Ping] cleared count=2"));
            Assert.That(PingLog.AllyTarget("Bot 1", new Side(0), "Enemy_3"), Is.EqualTo("[Ping] ally id=Bot_1 side=0 target=Enemy_3"));
            Assert.That(PingLog.View("PingMarkersView"), Is.EqualTo("[Ping] view=PingMarkersView"));
            Assert.That(PingLog.Describe(marker), Is.EqualTo("id=3 kind=Enemy key=42 at=(1.00, 1.10, 10.00) tracking=false expiresAt=6.00"));
        }

        [Test]
        public void ScreenShowsWhereTheMarkerIsDrawn()
        {
            var placement = new PingScreenPlacement(false, 0.03f, 0.5f, -90f, PingScreenEdge.Left);

            Assert.That(PingLog.Screen(1, placement), Is.EqualTo("[Ping] screen id=1 onScreen=false edge=Left angle=-90.0 at=(0.03, 0.50)"));
        }
    }
}
