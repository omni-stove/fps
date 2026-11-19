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
        private string _modelFolder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__AttachmentModelCatalogAssetStepTests_" + Guid.NewGuid().ToString("N");
            _path = _folder + "/Resources/" + AttachmentModelCatalog.ResourcePath + ".asset";
            _modelFolder = _folder + "/" + AttachmentModelCatalogAssetStep.ModelFolderName;
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

            Assert.That(entries, Has.Count.EqualTo(7));
            Assert.That(entries.Select(e => e.ItemId), Is.Unique);
            foreach (var entry in entries)
            {
                Assert.That(KitTuning.Items.TryFindAttachment(entry.ItemId, out _), Is.True, entry.ItemId);
                Assert.That(entry.Prefab, Is.Null);
            }
        }

        [Test]
        public void EveryAttachmentOfTheTuningHasAModel()
        {
            var listed = AttachmentModelCatalogAssetStep.Entries().Select(e => e.ItemId).ToList();

            foreach (var item in KitTuning.Items.Items.Where(i => i.IsAttachment))
            {
                Assert.That(listed, Does.Contain(item.Id), item.NameKey);
            }
        }

        [Test]
        public void OpticsHaveAnEyePointAndMuzzleDevicesMoveTheMuzzle()
        {
            foreach (var entry in AttachmentModelCatalogAssetStep.Entries())
            {
                Assert.That(KitTuning.Items.TryFindAttachment(entry.ItemId, out var item), Is.True, entry.ItemId);
                var shape = AttachmentModelCatalogAssetStep.ShapeFor(entry.ItemId);
                Assert.That(shape.Parts, Is.Not.Empty, entry.ItemId);
                if (item.Attachment.IsOptic)
                {
                    Assert.That(shape.SocketName, Is.EqualTo(WeaponSockets.SightName), entry.ItemId);
                    Assert.That(shape.SocketPosition.y, Is.GreaterThan(0f), entry.ItemId);
                }
                else if (entry.Anchor == AttachmentAnchor.Front)
                {
                    Assert.That(shape.SocketName, Is.EqualTo(WeaponSockets.MuzzleName), entry.ItemId);
                    Assert.That(shape.SocketPosition.z, Is.GreaterThan(0f), entry.ItemId);
                }
                else
                {
                    Assert.That(shape.SocketName, Is.Null, entry.ItemId);
                }
            }

            Assert.That(AttachmentModelCatalogAssetStep.ShapeFor(AttachmentModelCatalogAssetStep.SuppressorId).SocketPosition, Is.EqualTo(new Vector3(0f, 0f, 0.16f)));
            Assert.That(AttachmentModelCatalogAssetStep.ShapeFor(AttachmentModelCatalogAssetStep.CompensatorId).SocketPosition, Is.EqualTo(new Vector3(0f, 0f, 0.05f)));
        }

        [TestCase(AttachmentModelCatalogAssetStep.RedDotId)]
        [TestCase(AttachmentModelCatalogAssetStep.HoloSightId)]
        public void AReflexSightLeavesTheLineOfSightOpen(string itemId)
        {
            var shape = AttachmentModelCatalogAssetStep.ShapeFor(itemId);
            var eye = shape.SocketPosition;

            foreach (var part in shape.Parts)
            {
                var half = part.Size * 0.5f;
                var blocks = Mathf.Abs(eye.x - part.Center.x) < half.x
                    && Mathf.Abs(eye.y - part.Center.y) < half.y
                    && part.Center.z + half.z > eye.z;
                Assert.That(blocks, Is.False, part.Name);
            }
        }

        [Test]
        public void TheModelsLookDifferentFromEachOther()
        {
            var signatures = AttachmentModelCatalogAssetStep.Entries()
                .Select(e => string.Join("|", AttachmentModelCatalogAssetStep.ShapeFor(e.ItemId).Parts.Select(p => p.Name)))
                .ToList();

            Assert.That(signatures, Is.Unique);
            Assert.That(AttachmentModelCatalogAssetStep.ShapeFor(AttachmentModelCatalogAssetStep.RedDotId).HasPart(WeaponLook.DotSightName), Is.True);
            Assert.That(AttachmentModelCatalogAssetStep.ShapeFor(AttachmentModelCatalogAssetStep.HoloSightId).HasPart(WeaponLook.HoloSightName), Is.True);
            Assert.That(AttachmentModelCatalogAssetStep.ShapeFor(AttachmentModelCatalogAssetStep.Scope4xId).HasPart("ScopeTube"), Is.True);
        }

        [Test]
        public void ShapeForRejectsAnItemWithoutAModel()
        {
            Assert.Throws<ArgumentException>(() => AttachmentModelCatalogAssetStep.ShapeFor("not-an-attachment"));
            Assert.Throws<ArgumentException>(() => AttachmentModelCatalogAssetStep.PrefabName("not-an-attachment"));
        }

        [Test]
        public void BuildAtWritesTheEntriesAndKeepsTheAssetOnASecondRun()
        {
            var first = AttachmentModelCatalogAssetStep.BuildAt(_path, _modelFolder);
            var guid = AssetDatabase.AssetPathToGUID(_path);

            var second = AttachmentModelCatalogAssetStep.BuildAt(_path, _modelFolder);

            Assert.That(AssetDatabase.LoadAssetAtPath<AttachmentModelCatalog>(_path), Is.SameAs(second));
            Assert.That(second, Is.SameAs(first));
            Assert.That(AssetDatabase.AssetPathToGUID(_path), Is.EqualTo(guid));
            Assert.That(second.Attachments.Select(e => e.ItemId), Is.EqualTo(AttachmentModelCatalogAssetStep.Entries().Select(e => e.ItemId)));
            Assert.That(second.TryGet(AttachmentModelCatalogAssetStep.SuppressorId, out var suppressor), Is.True);
            Assert.That(suppressor.Anchor, Is.EqualTo(AttachmentAnchor.Front));
            Assert.That(suppressor.PlaceholderSize, Is.EqualTo(new Vector3(0.035f, 0.035f, 0.16f)));
            Assert.That(suppressor.Prefab, Is.Not.Null);
            Assert.That(WeaponSockets.FindSocket(suppressor.Prefab.transform, WeaponSockets.MuzzleName), Is.Not.Null);
        }

        [Test]
        public void BuildAtSavesAPrefabForEveryAttachmentWithoutColliders()
        {
            var catalog = AttachmentModelCatalogAssetStep.BuildAt(_path, _modelFolder);

            foreach (var entry in catalog.Attachments)
            {
                Assert.That(entry.Prefab, Is.Not.Null, entry.ItemId);
                Assert.That(entry.Prefab.name, Is.EqualTo(AttachmentModelCatalogAssetStep.PrefabName(entry.ItemId)));
                Assert.That(AssetDatabase.GetAssetPath(entry.Prefab), Does.StartWith(_modelFolder + "/"));
                Assert.That(entry.Prefab.GetComponentsInChildren<Collider>(true), Is.Empty, entry.ItemId);
                Assert.That(entry.Prefab.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(AttachmentModelCatalogAssetStep.ShapeFor(entry.ItemId).Parts.Count));
            }

            Assert.That(catalog.TryGet(AttachmentModelCatalogAssetStep.RedDotId, out var redDot), Is.True);
            Assert.That(WeaponSockets.FindSocket(redDot.Prefab.transform, WeaponSockets.SightName), Is.Not.Null);
            Assert.That(catalog.TryGet(AttachmentModelCatalogAssetStep.ForegripId, out var foregrip), Is.True);
            Assert.That(WeaponSockets.FindSocket(foregrip.Prefab.transform, WeaponSockets.SightName), Is.Null);
        }
    }
}
