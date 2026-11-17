using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Start/Update が走らないので、Bot の口 (BeginHold) と Tick・Advance を直接呼ぶ。
    public sealed class CoopInteractableDefinitionTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ANewDefinitionHasTheDefaults()
        {
            var definition = CreateDefinition("Defaults");
            var spec = definition.ToSpec();

            Assert.That(spec.RequiredCount, Is.EqualTo(CoopHoldSpec.DefaultRequiredCount));
            Assert.That(spec.HoldSeconds, Is.EqualTo(CoopHoldSpec.DefaultHoldSeconds));
            Assert.That(spec.ResetOnInterrupt, Is.EqualTo(CoopHoldSpec.DefaultResetOnInterrupt));
            Assert.That(definition.OpenOffset, Is.EqualTo(CoopDoor.DefaultOpenOffset));
            Assert.That(definition.OpenSeconds, Is.EqualTo(CoopDoor.DefaultOpenSeconds));
        }

        [Test]
        public void TheCountAndSecondsGoIntoTheSpec()
        {
            var definition = CreateDefinition("Vault");
            definition.RequiredCount = 3;
            definition.HoldSeconds = 4.5f;
            definition.ResetOnInterrupt = true;

            var spec = definition.ToSpec();

            Assert.That(spec.RequiredCount, Is.EqualTo(3));
            Assert.That(spec.HoldSeconds, Is.EqualTo(4.5f));
            Assert.That(spec.ResetOnInterrupt, Is.True);
            Assert.That(definition.ToSpec(), Is.Not.SameAs(spec), "each call makes a new spec");
        }

        [Test]
        public void AnInvalidDefinitionNamesTheAsset()
        {
            var definition = CreateDefinition("BrokenVault");
            definition.RequiredCount = 0;

            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec());
            Assert.That(error.Message, Does.Contain("BrokenVault"));

            definition.RequiredCount = 2;
            definition.OpenSeconds = -1f;
            Assert.That(Assert.Throws<InvalidOperationException>(() => definition.ValidateDoor()).Message, Does.Contain("BrokenVault"));

            definition.OpenSeconds = 1f;
            definition.OpenOffset = new Vector3(float.NaN, 0f, 0f);
            Assert.Throws<InvalidOperationException>(() => definition.ValidateDoor());
        }

        [Test]
        public void CopyFromWritesTheCountAndSecondsAndKeepsTheDoor()
        {
            var definition = CreateDefinition("Copied");
            definition.OpenOffset = new Vector3(2f, 0f, 0f);

            definition.CopyFrom(new CoopHoldSpec(4, 6f, resetOnInterrupt: true));

            Assert.That(definition.RequiredCount, Is.EqualTo(4));
            Assert.That(definition.HoldSeconds, Is.EqualTo(6f));
            Assert.That(definition.ResetOnInterrupt, Is.True);
            Assert.That(definition.OpenOffset, Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void ConfigureWithADefinitionUsesItsCountAndSeconds()
        {
            var interactable = CreateArea("CoopArea");
            var definition = CreateDefinition("Trio");
            definition.RequiredCount = 3;
            definition.HoldSeconds = 2f;

            interactable.Configure(definition);

            Assert.That(interactable.Definition, Is.SameAs(definition));
            Assert.That(interactable.RequiredCount, Is.EqualTo(3));
            Assert.That(interactable.HoldSeconds, Is.EqualTo(2f));
            Assert.Throws<ArgumentNullException>(() => interactable.Configure((CoopInteractableDefinition)null));
        }

        [Test]
        public void ConfigureWithAnInvalidDefinitionKeepsTheCurrentSpec()
        {
            var interactable = CreateArea("CoopArea");
            var definition = CreateDefinition("BadDoor");
            definition.OpenSeconds = float.PositiveInfinity;

            Assert.Throws<InvalidOperationException>(() => interactable.Configure(definition));

            Assert.That(interactable.Definition, Is.Null);
            Assert.That(interactable.RequiredCount, Is.EqualTo(CoopHoldSpec.DefaultRequiredCount));
        }

        [Test]
        public void AWiredDefinitionIsReadInsteadOfTheInspectorValues()
        {
            var definition = CreateDefinition("Wired");
            definition.RequiredCount = 1;
            definition.HoldSeconds = 0.5f;
            var interactable = CreateArea("CoopArea", definition);
            var bot = CreateBot("Bot");

            Assert.That(interactable.RequiredCount, Is.EqualTo(1));
            Assert.That(interactable.HoldSeconds, Is.EqualTo(0.5f));

            interactable.BeginHold(bot);
            interactable.Tick(0.5f);
            Assert.That(interactable.IsCompleted, Is.True, "one holder is enough for the wired definition");
        }

        [Test]
        public void WithoutADefinitionTheInspectorValuesAreUsed()
        {
            var interactable = CreateArea("CoopArea");

            Assert.That(interactable.Definition, Is.Null);
            Assert.That(interactable.RequiredCount, Is.EqualTo(CoopHoldSpec.DefaultRequiredCount));
            Assert.That(interactable.HoldSeconds, Is.EqualTo(CoopHoldSpec.DefaultHoldSeconds));
        }

        [Test]
        public void TheDoorOpensByTheDefinitionInsteadOfItsOwnValues()
        {
            var definition = CreateDefinition("SideDoor");
            definition.RequiredCount = 1;
            definition.HoldSeconds = 0f;
            definition.OpenOffset = new Vector3(2f, 0f, 0f);
            definition.OpenSeconds = 2f;
            var interactable = CreateArea("CoopArea");
            interactable.Configure(definition);
            var door = interactable.gameObject.AddComponent<CoopDoor>();
            var panel = Track(new GameObject("DoorPanel"));
            door.Panel = panel.transform;
            door.OpenOffset = new Vector3(0f, 3f, 0f);
            door.OpenSeconds = 1f;
            Assert.That(door.Bind(), Is.True);

            interactable.BeginHold(CreateBot("Bot"));
            interactable.Tick(0f);
            Assert.That(door.IsOpening, Is.True);

            door.Advance(1f);
            Assert.That(door.IsOpen, Is.False, "the definition's 2 seconds, not the door's 1 second");
            Assert.That(panel.transform.localPosition, Is.EqualTo(new Vector3(1f, 0f, 0f)));

            door.Advance(1f);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(panel.transform.localPosition, Is.EqualTo(new Vector3(2f, 0f, 0f)));
        }

        [Test]
        public void TheDoorUsesItsOwnValuesWithoutADefinition()
        {
            var interactable = CreateArea("CoopArea");
            var door = interactable.gameObject.AddComponent<CoopDoor>();
            var panel = Track(new GameObject("DoorPanel"));
            door.Panel = panel.transform;
            door.OpenOffset = new Vector3(0f, 3f, 0f);
            door.OpenSeconds = 0f;
            Assert.That(door.Bind(), Is.True);

            Assert.That(door.Open(), Is.True);
            Assert.That(panel.transform.localPosition, Is.EqualTo(new Vector3(0f, 3f, 0f)));
        }

        [Test]
        public void ADoorBehindAWiredInvalidDefinitionRefusesToBindAndOpen()
        {
            var definition = CreateDefinition("BrokenDoor");
            definition.OpenSeconds = -1f;
            var interactable = CreateArea("CoopArea", definition);
            var door = interactable.gameObject.AddComponent<CoopDoor>();
            var panel = Track(new GameObject("DoorPanel"));
            door.Panel = panel.transform;

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(CoopDoor.LogTag) + ".*CoopArea.*BrokenDoor"));
            Assert.That(door.Bind(), Is.False);

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(CoopDoor.LogTag) + ".*CoopArea.*BrokenDoor"));
            Assert.That(door.Open(), Is.False);
            Assert.That(door.IsOpening, Is.False);
            Assert.That(panel.transform.localPosition, Is.EqualTo(Vector3.zero));
        }

        private CoopInteractableDefinition CreateDefinition(string definitionName)
        {
            var definition = ScriptableObject.CreateInstance<CoopInteractableDefinition>();
            definition.name = definitionName;
            return Track(definition);
        }

        // Configure を使わず SerializedObject で書くのは、Inspector で配線したときと同じく最初に値を読むときに定義が読まれることを確かめるため。
        private CoopInteractable CreateArea(string areaName, CoopInteractableDefinition definition = null)
        {
            var area = Track(new GameObject(areaName));
            var trigger = area.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4f, 2.5f, 4f);
            trigger.center = new Vector3(0f, 1.25f, 0f);
            var interactable = area.AddComponent<CoopInteractable>();
            if (definition != null)
            {
                var serialized = new SerializedObject(interactable);
                serialized.FindProperty("_definition").objectReferenceValue = definition;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Physics.SyncTransforms();
            return interactable;
        }

        private GameObject CreateBot(string botName)
        {
            var bot = Track(new GameObject(botName));
            bot.transform.position = Vector3.zero;
            Physics.SyncTransforms();
            return bot;
        }

        private T Track<T>(T obj)
            where T : Object
        {
            _created.Add(obj);
            return obj;
        }
    }
}
