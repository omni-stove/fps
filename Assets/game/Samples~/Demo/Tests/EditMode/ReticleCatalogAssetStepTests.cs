using System;
using System.Linq;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class ReticleCatalogAssetStepTests
    {
        private string _folder;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__ReticleCatalogAssetStepTests_" + Guid.NewGuid().ToString("N");
            _path = _folder + "/Resources/" + ReticleCatalog.ResourcePath + ".asset";
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void TheBatchEntryRunsTheStepAfterThePresentationCatalog()
        {
            var steps = PresentationAssetSteps.Discover();

            var index = steps.ToList().FindIndex(step => step is ReticleCatalogAssetStep);
            Assert.That(index, Is.GreaterThan(0));
            Assert.That(steps[index].Name, Is.EqualTo("ReticleCatalog"));
            Assert.That(steps[index].Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
        }

        [Test]
        public void TheAssetSitsWhereTheRuntimeLoadsIt()
        {
            Assert.That(ReticleCatalogAssetStep.AssetPath, Does.EndWith("/Resources/Presentation/ReticleCatalog.asset"));
        }

        [Test]
        public void EveryReticleIdOfTheWeaponsAndAttachmentsHasAnEntry()
        {
            var ids = ReticleCatalogAssetStep.Entries().Select(e => e.Id).ToList();

            Assert.That(ids, Is.Unique);
            foreach (var (name, spec) in WeaponAssetBuilder.AllWeaponSources())
            {
                if (spec.Aim.Sight.HasReticle)
                {
                    Assert.That(ids, Does.Contain(spec.Aim.Sight.ReticleId), name);
                }
            }

            foreach (var item in KitTuning.Items.Items.Where(i => i.IsAttachment && i.Attachment.Sight != null && i.Attachment.Sight.HasReticle))
            {
                Assert.That(ids, Does.Contain(item.Attachment.Sight.ReticleId), item.NameKey);
            }
        }

        [Test]
        public void EachKindOfSightGetsItsOwnShape()
        {
            var shapes = ReticleCatalogAssetStep.Entries().ToDictionary(e => e.Id, e => e.Shape);

            Assert.That(shapes[ReticleCatalogAssetStep.RedDotId], Is.EqualTo(ReticleShape.Dot));
            Assert.That(shapes[ReticleCatalogAssetStep.HoloId], Is.EqualTo(ReticleShape.Circle));
            Assert.That(shapes[ReticleCatalogAssetStep.Scope4xId], Is.EqualTo(ReticleShape.Circle));
            Assert.That(shapes[ReticleCatalogAssetStep.DuplexId], Is.EqualTo(ReticleShape.Crosshair));
            Assert.That(shapes[ReticleCatalogAssetStep.MilDotId], Is.EqualTo(ReticleShape.MilDot));
            foreach (var entry in ReticleCatalogAssetStep.Entries())
            {
                Assert.That(entry.Image, Is.Null, entry.Id);
            }
        }

        [Test]
        public void BuildAtWritesTheEntriesAndKeepsTheAssetOnASecondRun()
        {
            var first = ReticleCatalogAssetStep.BuildAt(_path);
            var guid = AssetDatabase.AssetPathToGUID(_path);

            var second = ReticleCatalogAssetStep.BuildAt(_path);

            Assert.That(AssetDatabase.LoadAssetAtPath<ReticleCatalog>(_path), Is.SameAs(second));
            Assert.That(second, Is.SameAs(first));
            Assert.That(AssetDatabase.AssetPathToGUID(_path), Is.EqualTo(guid));
            Assert.That(second.Reticles.Select(e => e.Id), Is.EqualTo(ReticleCatalogAssetStep.Entries().Select(e => e.Id)));
            Assert.That(second.TryGet(ReticleCatalogAssetStep.MilDotId, out var mildot), Is.True);
            Assert.That(mildot.Shape, Is.EqualTo(ReticleShape.MilDot));
            Assert.That(mildot.Size, Is.EqualTo(1f));
            Assert.That(ReticleCatalog.Resolve(second, ReticleCatalogAssetStep.RedDotId, false).Color, Is.EqualTo(ReticleCatalogAssetStep.RedDotColor));
        }
    }
}
