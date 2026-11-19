using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class CampaignContentSlotsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            CampaignContentSlots.Register();
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
        public void TheMissionsSlotReadsAndWritesTheGameContent()
        {
            var slot = MissionsSlot();
            var missions = Track(ScriptableObject.CreateInstance<MissionCatalog>());

            Assert.That(slot.AssetType, Is.EqualTo(typeof(MissionCatalog)));
            Assert.That(slot.Get(_content), Is.Null);

            slot.Set(_content, missions);

            Assert.That(_content.Missions, Is.SameAs(missions));
            Assert.That(slot.Get(_content), Is.SameAs(missions));
        }

        [Test]
        public void AnotherTypeInTheMissionsFieldIsNotReadAsMissions()
        {
            var slot = MissionsSlot();
            _content.Missions = Track(ScriptableObject.CreateInstance<MotorSettings>());

            Assert.That(slot.Get(_content), Is.Null);
        }

        [Test]
        public void FillEmptyFillsTheMissionsSlotWithTheOnlyCatalog()
        {
            var missions = Track(ScriptableObject.CreateInstance<MissionCatalog>());
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(MissionCatalog), new Object[] { missions } },
            };

            var left = GameContentAssets.FillEmpty(_content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(_content.Missions, Is.SameAs(missions));
            Assert.That(left, Does.Not.Contain(CampaignContentSlots.MissionsSlotName));
        }

        [Test]
        public void TheRunnerReadsTheMissionsFromTheEntry()
        {
            _content.Missions = Track(ScriptableObject.CreateInstance<MissionCatalog>());
            GameContent.Use(_content);
            var runner = Track(new GameObject("Campaign")).AddComponent<CampaignRunner>();

            Assert.That(runner.EffectiveMissions, Is.SameAs(_content.Missions));
        }

        private static GameContentSlot MissionsSlot()
        {
            return GameContentSlots.All.Single(s => s.Name == CampaignContentSlots.MissionsSlotName);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
