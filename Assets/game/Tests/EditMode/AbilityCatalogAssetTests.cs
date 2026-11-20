using System.Collections.Generic;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AbilityCatalogAssetTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private RecordingAbilityDefinition Ability(string assetName, string id = null)
        {
            var ability = ScriptableObject.CreateInstance<RecordingAbilityDefinition>();
            ability.name = assetName;
            ability.Id = id;
            _created.Add(ability);
            return ability;
        }

        private AbilityCatalogAsset Catalog(params AbilityDefinition[] abilities)
        {
            var catalog = ScriptableObject.CreateInstance<AbilityCatalogAsset>();
            catalog.name = "Abilities";
            catalog.SetAbilities(abilities);
            _created.Add(catalog);
            return catalog;
        }

        [Test]
        public void ListsTheIdsInCatalogOrderAndFallsBackToTheAssetName()
        {
            var catalog = Catalog(Ability("Grapple", "grapple"), Ability("Beacon"));

            Assert.That(catalog.ToIds(), Is.EqualTo(new[] { "grapple", "Beacon" }));
            Assert.That(catalog.Abilities.Count, Is.EqualTo(2));
        }

        [Test]
        public void FindsADefinitionById()
        {
            var grapple = Ability("Grapple", "grapple");
            var beacon = Ability("Beacon", "beacon");
            var catalog = Catalog(grapple, beacon);

            Assert.That(catalog.TryFind("beacon", out var found), Is.True);
            Assert.That(found, Is.SameAs(beacon));
            Assert.That(catalog.TryFind("grapple", out found), Is.True);
            Assert.That(found, Is.SameAs(grapple));
        }

        [Test]
        public void DoesNotFindAnUnknownOrEmptyId()
        {
            var catalog = Catalog(Ability("Grapple", "grapple"));

            Assert.That(catalog.TryFind("Grapple", out var found), Is.False, "the asset name is not the id once an id is set");
            Assert.That(found, Is.Null);
            Assert.That(catalog.TryFind(string.Empty, out found), Is.False);
            Assert.That(catalog.TryFind(null, out found), Is.False);
            Assert.That(found, Is.Null);
        }

        [Test]
        public void SetAbilitiesWithNullEmptiesTheCatalog()
        {
            var catalog = Catalog(Ability("Grapple"));

            catalog.SetAbilities(null);

            Assert.That(catalog.Abilities, Is.Empty);
        }

        [Test]
        public void RejectsAnEmptyCatalog()
        {
            var e = Assert.Throws<AbilityDefinitionException>(() => Catalog().ToIds());
            Assert.That(e.Message, Is.EqualTo("ability catalog 'Abilities' has no abilities"));
        }

        [Test]
        public void RejectsAnEmptyEntry()
        {
            var catalog = Catalog(Ability("Grapple"), null);

            var e = Assert.Throws<AbilityDefinitionException>(() => catalog.ToIds());
            Assert.That(e.Message, Is.EqualTo("ability catalog 'Abilities' ability #1 is empty"));
            Assert.Throws<AbilityDefinitionException>(() => catalog.TryFind("Grapple", out _), "a broken catalog finds nothing");
        }

        [Test]
        public void RejectsADestroyedEntry()
        {
            var destroyed = Ability("Grapple");
            var catalog = Catalog(destroyed);
            Object.DestroyImmediate(destroyed);

            var e = Assert.Throws<AbilityDefinitionException>(() => catalog.ToIds());
            Assert.That(e.Message, Is.EqualTo("ability catalog 'Abilities' ability #0 is empty"));
        }

        [Test]
        public void RejectsAnAbilityWithoutAnIdBecauseItWouldReadAsNone()
        {
            var catalog = Catalog(Ability(string.Empty));

            var e = Assert.Throws<AbilityDefinitionException>(() => catalog.ToIds());
            Assert.That(e.Message, Is.EqualTo("ability catalog 'Abilities' ability #0 has no id"));
        }

        [Test]
        public void RejectsTheSameIdTwice()
        {
            var catalog = Catalog(Ability("GrappleA", "grapple"), Ability("GrappleB", "grapple"));

            var e = Assert.Throws<AbilityDefinitionException>(() => catalog.ToIds());
            Assert.That(e.Message, Is.EqualTo("ability catalog 'Abilities' ability #1 ('GrappleB') has the id 'grapple' already used by 'GrappleA'"));
        }

        [Test]
        public void RejectsAnInvalidAbilityAndNamesItsPlace()
        {
            var broken = Ability("Grapple");
            broken.MaxCharges = 0;
            var catalog = Catalog(broken);

            var e = Assert.Throws<AbilityDefinitionException>(() => catalog.ToIds());
            Assert.That(e.Message, Does.StartWith("ability catalog 'Abilities' ability #0: ability 'Grapple' is invalid: "));
            Assert.That(e.InnerException, Is.InstanceOf<AbilityDefinitionException>());
        }

        [Test]
        public void GameContentHoldsTheAbilityCatalog()
        {
            var content = ScriptableObject.CreateInstance<GameContent>();
            _created.Add(content);
            Assert.That(content.AbilityCatalog, Is.Null);

            var catalog = Catalog(Ability("Grapple"));
            content.AbilityCatalog = catalog;

            Assert.That(content.AbilityCatalog, Is.SameAs(catalog));
        }
    }
}
