using System;
using System.Linq;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GameContentSlotsTests
    {
        private const string TestSlotName = "GameContentSlotsTests.Slot";

        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
        }

        [TearDown]
        public void TearDown()
        {
            GameContentSlots.Unregister(TestSlotName);
            Object.DestroyImmediate(_content);
        }

        [Test]
        public void ARegisteredSlotAppearsOnceInAll()
        {
            GameContentSlots.Register(TestSlotName, typeof(ScriptableObject), c => c.Missions, (c, v) => c.Missions = (ScriptableObject)v);
            GameContentSlots.Register(TestSlotName, typeof(ScriptableObject), c => c.Missions, (c, v) => c.Missions = (ScriptableObject)v);

            var found = GameContentSlots.All.Where(s => s.Name == TestSlotName).ToList();

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].AssetType, Is.EqualTo(typeof(ScriptableObject)));
        }

        [Test]
        public void TheSameNameReplacesTheSlotInItsPlace()
        {
            GameContentSlots.Register(TestSlotName, typeof(ScriptableObject), c => c.Missions, (c, v) => c.Missions = (ScriptableObject)v);
            var index = GameContentSlots.All.ToList().FindIndex(s => s.Name == TestSlotName);

            GameContentSlots.Register(
                TestSlotName, typeof(MotorSettings), c => c.AwarenessIndicator, (c, v) => c.AwarenessIndicator = (ScriptableObject)v);

            var all = GameContentSlots.All;
            Assert.That(all.Count(s => s.Name == TestSlotName), Is.EqualTo(1));
            Assert.That(all[index].Name, Is.EqualTo(TestSlotName));
            Assert.That(all[index].AssetType, Is.EqualTo(typeof(MotorSettings)));

            var asset = ScriptableObject.CreateInstance<MotorSettings>();
            try
            {
                all[index].Set(_content, asset);
                Assert.That(_content.AwarenessIndicator, Is.SameAs(asset));
                Assert.That(_content.Missions, Is.Null, "the replaced slot's setter is not used");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void UnregisterRemovesOnlyThatName()
        {
            GameContentSlots.Register(TestSlotName, typeof(ScriptableObject), c => c.Missions, (c, v) => c.Missions = (ScriptableObject)v);
            var before = GameContentSlots.All.Count;

            Assert.That(GameContentSlots.Unregister(TestSlotName), Is.True);
            Assert.That(GameContentSlots.Unregister(TestSlotName), Is.False);
            Assert.That(GameContentSlots.All.Count, Is.EqualTo(before - 1));
            Assert.That(GameContentSlots.All.Select(s => s.Name), Has.No.Member(TestSlotName));
        }

        [Test]
        public void RegisterRejectsAMissingNameTypeOrAccessor()
        {
            Func<GameContent, Object> get = c => c.Missions;
            Action<GameContent, Object> set = (c, v) => c.Missions = (ScriptableObject)v;

            Assert.Throws<ArgumentException>(() => GameContentSlots.Register(" ", typeof(ScriptableObject), get, set));
            Assert.Throws<ArgumentNullException>(() => GameContentSlots.Register(TestSlotName, null, get, set));
            Assert.Throws<ArgumentException>(() => GameContentSlots.Register(TestSlotName, typeof(string), get, set));
            Assert.Throws<ArgumentNullException>(() => GameContentSlots.Register(TestSlotName, typeof(ScriptableObject), null, set));
            Assert.Throws<ArgumentNullException>(() => GameContentSlots.Register(TestSlotName, typeof(ScriptableObject), get, null));
            Assert.That(GameContentSlots.All.Select(s => s.Name), Has.No.Member(TestSlotName), "a rejected slot is not added");
        }
    }
}
