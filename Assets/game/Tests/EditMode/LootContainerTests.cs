using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class LootContainerTests
    {
        private const string _ammo = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e001";

        private GameObject _go;
        private LootContainer _container;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Crate");
            _go.transform.position = new Vector3(2f, 0f, 0f);
            _container = _go.AddComponent<LootContainer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void ReachIsMeasuredOnTheGround()
        {
            Assert.That(_container.Reach, Is.EqualTo(LootContainer.DefaultReach));
            Assert.That(_container.IsInReach(new Vector3(2f, 10f, 1.5f)), Is.True, "height does not matter");
            Assert.That(_container.IsInReach(new Vector3(2f, 0f, 1.7f)), Is.False);
            Assert.That(_container.HorizontalDistanceTo(new Vector3(5f, 3f, 4f)), Is.EqualTo(5f).Within(1e-5f));

            _container.Reach = 3f;
            Assert.That(_container.IsInReach(new Vector3(2f, 0f, 2.5f)), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => _container.Reach = 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _container.Reach = float.PositiveInfinity);
        }

        [Test]
        public void OpeningRollsTheTableOnce()
        {
            var table = new LootTable("crate", new[] { new LootEntry(_ammo, 1f, 2, 2) }, rolls: 2);

            var contents = _container.Open(table, new System.Random(1));

            Assert.That(_container.IsOpened, Is.True);
            Assert.That(contents.Count, Is.EqualTo(1), "the same item from both rolls is merged");
            Assert.That(contents[0].ItemId, Is.EqualTo(_ammo));
            Assert.That(contents[0].Count, Is.EqualTo(4));
            Assert.That(_container.Contents, Is.EqualTo(contents));
            Assert.Throws<InvalidOperationException>(() => _container.Open(table, new System.Random(1)));
        }

        [Test]
        public void WithoutATableItOpensEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => _container.Open(null, null));

            var contents = _container.Open(null, new System.Random(1));

            Assert.That(contents, Is.Empty);
            Assert.That(_container.IsOpened, Is.True);
        }

        [Test]
        public void ThePlaceholderBoxStaysSolidForCover()
        {
            _container.Build();

            var visual = _go.transform.Find(LootContainer.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(visual.localScale, Is.EqualTo(LootContainer.DefaultVisualSize));

            _container.Open(null, new System.Random(1));
            Assert.That(_go.transform.Find(LootContainer.VisualName), Is.SameAs(visual), "an opened placeholder keeps its box and only changes colour");
        }

        [Test]
        public void ShowingOpenedSwapsToTheOpenedLookWithoutContents()
        {
            var opened = new GameObject("OpenedLook");
            try
            {
                _container.OpenedVisualPrefab = opened;
                _container.Build();

                _container.ShowOpened();
                _container.ShowOpened();

                Assert.That(_container.IsOpened, Is.True);
                Assert.That(_container.Contents, Is.Empty);
                Assert.That(_go.transform.Find(LootContainer.VisualName).gameObject.activeSelf, Is.False);
                Assert.That(_go.transform.Find(LootContainer.VisualName + "-Opened").gameObject.activeSelf, Is.True);
                Assert.Throws<InvalidOperationException>(() => _container.Open(null, new System.Random(1)), "the box was already opened elsewhere");
            }
            finally
            {
                Object.DestroyImmediate(opened);
            }
        }

        [Test]
        public void TheTableIdIsNeverNull()
        {
            Assert.That(_container.TableId, Is.Empty);
            _container.TableId = null;
            Assert.That(_container.TableId, Is.Empty);
        }
    }
}
