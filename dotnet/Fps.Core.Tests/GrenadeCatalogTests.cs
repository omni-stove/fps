using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class GrenadeCatalogTests
    {
        private static readonly GrenadeSpec _smoke = new GrenadeSpec(
            "ecaf6e57-cb77-4707-9321-6ea5b659408f", "grenade.smoke", new ImpactSpec(damage: 0, areaEffect: new AreaEffectSpec(5f, 15f, 0.5f, 0)));

        [Test]
        public void TheDefaultCarriesOnlyTheFragGrenade()
        {
            Assert.That(GrenadeCatalog.Default.Types, Is.EqualTo(new[] { GrenadeSpec.Default }));
            Assert.That(GrenadeCatalog.Default.Carried, Is.EqualTo(new[] { GrenadeSpec.Default }));
        }

        [Test]
        public void FindsTypesByIdAndCarriesTheNamedOnesInOrder()
        {
            var catalog = new GrenadeCatalog(new[] { GrenadeSpec.Default, _smoke }, new[] { _smoke.Id, GrenadeSpec.DefaultId });

            Assert.That(catalog.TryFind(_smoke.Id, out var found), Is.True);
            Assert.That(found, Is.SameAs(_smoke));
            Assert.That(catalog.TryFind("5eb3292d-6300-4ec2-b325-6ad793017e49", out found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(catalog.Carried, Is.EqualTo(new[] { _smoke, GrenadeSpec.Default }));
        }

        [Test]
        public void CanCarryNothing()
        {
            Assert.That(new GrenadeCatalog(new[] { GrenadeSpec.Default }, Array.Empty<string>()).Carried, Is.Empty);
        }

        [Test]
        public void RejectsBadLists()
        {
            Assert.That(Assert.Throws<ArgumentException>(() => new GrenadeCatalog(Array.Empty<GrenadeSpec>())).ParamName, Is.EqualTo("types"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => new GrenadeCatalog(new[] { GrenadeSpec.Default, GrenadeSpec.Default })).ParamName,
                Is.EqualTo("types"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => new GrenadeCatalog(new[] { GrenadeSpec.Default }, new[] { _smoke.Id })).ParamName,
                Is.EqualTo("carriedIds"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => new GrenadeCatalog(new[] { GrenadeSpec.Default }, new[] { GrenadeSpec.DefaultId, GrenadeSpec.DefaultId })).ParamName,
                Is.EqualTo("carriedIds"));
            Assert.Throws<ArgumentNullException>(() => new GrenadeCatalog(null));
        }
    }
}
