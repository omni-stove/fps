using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class RagdollCatalogBuilderTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private string _folder;
        private string _catalogPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__RagdollCatalogBuilderTests_" + Guid.NewGuid().ToString("N");
            _catalogPath = _folder + "/Resources/Presentation/RagdollCatalog.asset";
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void TheCatalogIsWrittenNextToThePresentationCatalog()
        {
            Assert.That(RagdollCatalogBuilder.CatalogPath, Is.EqualTo(PresentationAssetBuilder.PresentationFolder + "/Resources/Presentation/RagdollCatalog.asset"));
        }

        [Test]
        public void EntriesCoverEveryBoneOfTheCharacterRigThatHasParts()
        {
            var entries = RagdollCatalogBuilder.Entries();
            var withParts = CharacterRigBuilder.BoneVolumes().Select(v => v.Bone).ToArray();

            Assert.That(entries.Select(e => e.Bone), Is.EqualTo(withParts), "same bones in root-first order");
            Assert.That(withParts, Is.EquivalentTo(CharacterRigBuilder.BoneNames), "every bone of the placeholder rig has a part");
            Assert.That(entries.Sum(e => e.Mass), Is.EqualTo(DeathBodyPresenter.ShellMass).Within(5f), "weighs about as much as the one-piece body");

            var head = entries.Single(e => e.Bone == CharacterRigBuilder.HeadBone);
            Assert.That(head.Shape, Is.EqualTo(RagdollShape.Sphere));
            Assert.That(head.Size.x, Is.EqualTo(0.26f).Within(1e-5f));
        }

        [Test]
        public void BuildAtWritesAValidCatalogAndKeepsItsGuid()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[RagdollCatalogBuilder\] saved 11 bone\(s\) to "));
            var first = RagdollCatalogBuilder.BuildAt(_catalogPath);
            var guid = AssetDatabase.AssetPathToGUID(_catalogPath);

            Assert.That(first, Is.Not.Null);
            Assert.DoesNotThrow(first.Validate);
            Assert.That(first.Bones, Has.Count.EqualTo(RagdollCatalogBuilder.Entries().Count));
            Assert.That(first.SettleSeconds, Is.EqualTo(RagdollCatalog.DefaultSettleSeconds));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[RagdollCatalogBuilder\] saved 11 bone\(s\) to "));
            var second = RagdollCatalogBuilder.BuildAt(_catalogPath);

            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(guid));
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void BuildAtRejectsPathsOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => RagdollCatalogBuilder.BuildAt("RagdollCatalog.asset"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void ThePlaceholderRigFallsAsARagdollWithTheGeneratedCatalog()
        {
            var rigPath = _folder + "/CharacterRig.prefab";
            LogAssert.Expect(LogType.Log, new Regex(@"^\[CharacterRigBuilder\] saved "));
            var rigPrefab = CharacterRigBuilder.BuildAt(rigPath, _folder + "/CharacterRig.controller", _folder + "/Clips");

            var body = new GameObject("rig-body");
            _spawned.Add(body);
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize("rig-body", 100);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.name = CharacterAnimator.RigObjectName;
            rig.transform.SetParent(body.transform, false);

            var catalog = ScriptableObject.CreateInstance<RagdollCatalog>();
            _spawned.Add(catalog);
            catalog.SetBones(RagdollCatalogBuilder.Entries());

            var presenter = DeathBodyPresenter.Attach(body);
            presenter.RagdollCatalog = catalog;
            presenter.Bind(damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(
                $@"^\[DeathBody\] collapse id=rig-body ragdoll bones=11 parts={CharacterRigBuilder.PartNames.Count} "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] push id=rig-body ragdoll push=\(1,0\) "));

            damageable.TakeDamage(100, body.transform.position + new Vector3(-4f, 1.5f, 0f));

            var shell = presenter.Shell;
            _spawned.Add(shell);
            Assert.That(shell.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(11));
            Assert.That(shell.GetComponentsInChildren<CharacterJoint>(true), Has.Length.EqualTo(10), "every bone but the hips hangs from its parent");
            // 体に CharacterAnimator が無いので、抜け殻の根は体そのものを写したもので、リグはその子にある
            var rigPrefix = CharacterAnimator.RigObjectName + "/";
            var shin = shell.transform.Find(rigPrefix + CharacterRigBuilder.BonePath(CharacterRigBuilder.LeftShinBone));
            Assert.That(shin, Is.Not.Null);
            Assert.That(
                shin.GetComponent<CharacterJoint>().connectedBody,
                Is.SameAs(shell.transform.Find(rigPrefix + CharacterRigBuilder.BonePath(CharacterRigBuilder.LeftThighBone)).GetComponent<Rigidbody>()));
        }

        [Test]
        public void DiscoverRunsTheRagdollStepAfterThePresentationCatalog()
        {
            var steps = PresentationAssetSteps.Discover();
            var ragdoll = steps.OfType<RagdollCatalogAssetStep>().Single();

            Assert.That(ragdoll.Name, Is.EqualTo("RagdollCatalog"));
            Assert.That(ragdoll.Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
            Assert.That(steps.ToList().IndexOf(ragdoll), Is.GreaterThan(0));
        }
    }
}
