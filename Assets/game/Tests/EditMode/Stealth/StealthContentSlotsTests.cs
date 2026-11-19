using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class StealthContentSlotsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            StealthActionInstaller.RegisterContentSlots();
            _content = Track(ScriptableObject.CreateInstance<GameContent>());
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheAwarenessIndicatorSlotReadsAndWritesTheGameContent()
        {
            var slot = AwarenessIndicatorSlot();
            var indicator = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());

            Assert.That(slot.AssetType, Is.EqualTo(typeof(AwarenessIndicatorCatalog)));
            Assert.That(slot.Get(_content), Is.Null);

            slot.Set(_content, indicator);

            Assert.That(_content.AwarenessIndicator, Is.SameAs(indicator));
            Assert.That(slot.Get(_content), Is.SameAs(indicator));
        }

        [Test]
        public void AnotherTypeInTheAwarenessIndicatorFieldIsNotReadAsAnIndicator()
        {
            var slot = AwarenessIndicatorSlot();
            _content.AwarenessIndicator = Track(ScriptableObject.CreateInstance<MotorSettings>());

            Assert.That(slot.Get(_content), Is.Null);
        }

        [Test]
        public void FillEmptyLeavesTheAwarenessIndicatorSlotWhenTheCandidateIsAmbiguous()
        {
            var indicatorA = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());
            var indicatorB = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(AwarenessIndicatorCatalog), new Object[] { indicatorA, indicatorB } },
            };

            var left = GameContentAssets.FillEmpty(_content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(_content.AwarenessIndicator, Is.Null);
            Assert.That(left, Does.Contain(StealthActionInstaller.AwarenessIndicatorSlotName));
        }

        [Test]
        public void FillEmptyFillsTheAwarenessIndicatorSlotWithTheOnlyCatalog()
        {
            var indicator = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(AwarenessIndicatorCatalog), new Object[] { indicator } },
            };

            var left = GameContentAssets.FillEmpty(_content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(_content.AwarenessIndicator, Is.SameAs(indicator));
            Assert.That(left, Does.Not.Contain(StealthActionInstaller.AwarenessIndicatorSlotName));
        }

        [Test]
        public void TheAwarenessIndicatorComesFromTheEntry()
        {
            _content.AwarenessIndicator = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());
            GameContent.Use(_content);

            Assert.That(AwarenessIndicatorCatalog.Load(), Is.SameAs(_content.AwarenessIndicator));
        }

        private static GameContentSlot AwarenessIndicatorSlot()
        {
            return GameContentSlots.All.Single(s => s.Name == StealthActionInstaller.AwarenessIndicatorSlotName);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
