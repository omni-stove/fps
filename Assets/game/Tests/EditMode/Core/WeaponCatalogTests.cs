using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponCatalogTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";
        private const string _handgunId = "00000000-0000-4000-8000-000000000002";
        private const string _smgId = "00000000-0000-4000-8000-000000000003";

        private static WeaponSpec Spec(string id, FireMode fireMode = FireMode.Auto)
        {
            return new WeaponSpec(id, null, WeaponCategory.AssaultRifle, fireMode, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None);
        }

        [Test]
        public void KeepsTheDefinitionOrder()
        {
            var rifle = Spec(_rifleId);
            var handgun = Spec(_handgunId, FireMode.Semi);
            var smg = Spec(_smgId);

            var catalog = new WeaponCatalog(new[] { rifle, handgun, smg });

            Assert.That(catalog.Count, Is.EqualTo(3));
            Assert.That(catalog.All, Is.EqualTo(new[] { rifle, handgun, smg }));
        }

        [Test]
        public void IsNotChangedByTheSourceListLater()
        {
            var rifle = Spec(_rifleId);
            var source = new List<WeaponSpec> { rifle };
            var catalog = new WeaponCatalog(source);

            source.Add(Spec(_handgunId));

            Assert.That(catalog.All, Is.EqualTo(new[] { rifle }));
            Assert.That(catalog.Contains(_handgunId), Is.False);
        }

        [Test]
        public void FindsAWeaponById()
        {
            var rifle = Spec(_rifleId);
            var handgun = Spec(_handgunId);
            var catalog = new WeaponCatalog(new[] { rifle, handgun });

            Assert.That(catalog.TryGet(_handgunId, out var found), Is.True);
            Assert.That(found, Is.SameAs(handgun));
            Assert.That(catalog.Get(_rifleId), Is.SameAs(rifle));
            Assert.That(catalog.Contains(_rifleId), Is.True);
        }

        [TestCase(_smgId)]
        [TestCase("00000000-0000-4000-8000-00000000000A")]
        [TestCase(" " + _rifleId)]
        [TestCase("rifle")]
        [TestCase("")]
        public void DoesNotFindAnUnknownId(string id)
        {
            var catalog = new WeaponCatalog(new[] { Spec(_rifleId), Spec("00000000-0000-4000-8000-00000000000a") });

            Assert.That(catalog.TryGet(id, out var found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(catalog.Contains(id), Is.False);
            Assert.Throws<KeyNotFoundException>(() => catalog.Get(id));
        }

        [Test]
        public void TreatsANullIdAsMissing()
        {
            var catalog = new WeaponCatalog(new[] { Spec(_rifleId) });

            Assert.That(catalog.TryGet(null, out var found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(catalog.Contains(null), Is.False);
            Assert.Throws<ArgumentNullException>(() => catalog.Get(null));
        }

        [Test]
        public void RejectsNullSpecs()
        {
            Assert.Throws<ArgumentNullException>(() => new WeaponCatalog(null));
        }

        [Test]
        public void RejectsEmptySpecs()
        {
            var e = Assert.Throws<ArgumentException>(() => new WeaponCatalog(Array.Empty<WeaponSpec>()));
            Assert.That(e.ParamName, Is.EqualTo("specs"));
        }

        [Test]
        public void RejectsANullElement()
        {
            var e = Assert.Throws<ArgumentException>(() => new WeaponCatalog(new[] { Spec(_rifleId), null }));
            Assert.That(e.ParamName, Is.EqualTo("specs"));
        }

        [Test]
        public void RejectsADuplicateId()
        {
            // 前後の空白は WeaponSpec が除くので、同じ Id として扱われる
            var e = Assert.Throws<ArgumentException>(
                () => new WeaponCatalog(new[] { Spec(_rifleId), Spec(_handgunId), Spec(" " + _rifleId + " ") }));
            Assert.That(e.ParamName, Is.EqualTo("specs"));
            Assert.That(e.Message, Does.Contain(_rifleId));
        }
    }
}
