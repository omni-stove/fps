using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed partial class ServerCombatTests
    {
        [Test]
        public void ASmokeGrenadeBlocksSightThroughItUntilItClearsAndHurtsNoOne()
        {
            Start(Rules(), grenades: new GrenadeCatalog(new[] { _smoke }));
            Join(1);
            Join(2);
            Step(0.2f);
            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX });
            Step(_smoke.FuseSeconds + 0.1f);

            Assert.That(_combat.AreaEffects.Count, Is.EqualTo(1));
            var center = _combat.AreaEffects.Zones[0].Center;
            var radius = _smoke.Impact.AreaEffect.Radius;
            var across = new Vector3(radius * 3f, 0f, 0f);
            var beside = new Vector3(0f, 0f, radius + 1f);
            Assert.That(_combat.SightBlockers.Blocks(center - across, center + across), Is.True, "a line through the smoke");
            Assert.That(_combat.SightBlockers.Blocks(center - across + beside, center + across + beside), Is.False, "a line beside it");
            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));

            Step(_smoke.Impact.AreaEffect.DurationSeconds);

            Assert.That(_combat.AreaEffects.Count, Is.Zero);
            Assert.That(_combat.SightBlockers.Blocks(center - across, center + across), Is.False, "the smoke has cleared");
        }

        [Test]
        public void AnAreaThatHurtsDoesNotBlockSight()
        {
            Start(Rules());
            var fire = new AreaEffectSpec(4f, 3f, 0.5f, 5, visualId: "fire");
            _combat.AreaEffects.Spread(fire, new Vector3(0f, 1f, 0f), null, string.Empty);

            Assert.That(_combat.SightBlockers.Blocks(new Vector3(-10f, 1f, 0f), new Vector3(10f, 1f, 0f)), Is.False);
        }
    }
}
