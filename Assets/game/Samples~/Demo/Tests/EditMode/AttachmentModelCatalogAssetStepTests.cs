using System;
using System.Linq;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class AttachmentModelCatalogAssetStepTests
    {
        private string _folder;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__AttachmentModelCatalogAssetStepTests_" + Guid.NewGuid().ToString("N");
            _path = _folder + "/Resources/" + AttachmentModelCatalog.ResourcePath + ".asset";
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

            var index = steps.ToList().FindIndex(step => step is AttachmentModelCatalogAssetStep);
            Assert.That(index, Is.GreaterThan(0));
            Assert.That(steps[index].Name, Is.EqualTo("AttachmentModelCatalog"));
            Assert.That(steps[index].Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
        }

        [Test]
        public void TheAssetSitsWhereTheRuntimeLoadsIt()
        {
            Assert.That(
                AttachmentModelCatalogAssetStep.AssetPath,
                Does.EndWith("/Resources/Presentation/AttachmentModelCatalog.asset"));
        }

        [Test]
        public void EveryEntryIsAnAttachmentOfTheTuning()
        {
            var entries = AttachmentModelCatalogAssetStep.Entries();

            Assert.That(entries, Has.Count.EqualTo(6));
            Assert.That(entries.Select(e => e.ItemId), Is.Unique);
            foreach (var entry in entries)
            {
                Assert.That(KitTuning.Items.TryFindAttachment(entry.ItemId, out _), Is.True, entry.ItemId);
                Assert.That(entry.Prefab, Is.Null);
            }
        }

        [Test]
        public void BuildAtWritesTheEntriesAndKeepsTheAssetOnASecondRun()
        {
            var first = AttachmentModelCatalogAssetStep.BuildAt(_path);
            var guid = AssetDatabase.AssetPathToGUID(_path);

            var second = AttachmentModelCatalogAssetStep.BuildAt(_path);

            Assert.That(AssetDatabase.LoadAssetAtPath<AttachmentModelCatalog>(_path), Is.SameAs(second));
            Assert.That(second, Is.SameAs(first));
            Assert.That(AssetDatabase.AssetPathToGUID(_path), Is.EqualTo(guid));
            Assert.That(second.Attachments.Select(e => e.ItemId), Is.EqualTo(AttachmentModelCatalogAssetStep.Entries().Select(e => e.ItemId)));
            Assert.That(second.TryGet(AttachmentModelCatalogAssetStep.SuppressorId, out var suppressor), Is.True);
            Assert.That(suppressor.Anchor, Is.EqualTo(AttachmentAnchor.Front));
            Assert.That(suppressor.PlaceholderSize, Is.EqualTo(new Vector3(0.035f, 0.035f, 0.16f)));
        }
    }
}
