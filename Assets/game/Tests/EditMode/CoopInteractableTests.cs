using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// Edit Mode では Start/Update/OnTrigger* が走らず入力も読まないので、Bot の口 (BeginHold/EndHold) と Tick を直接呼んで確かめる。
    /// 範囲の判定は Collider に問い合わせるので、置いたり動かしたりしたあとに SyncTransforms する。
    /// </summary>
    public sealed class CoopInteractableTests
    {
        private const float HoldSeconds = 1f;

        private readonly List<Object> _created = new List<Object>();
        private CoopInteractable _interactable;
        private GameObject _botA;
        private GameObject _botB;

        [SetUp]
        public void SetUp()
        {
            var area = Track(new GameObject("CoopArea"));
            var trigger = area.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4f, 2.5f, 4f);
            trigger.center = new Vector3(0f, 1.25f, 0f);

            _interactable = area.AddComponent<CoopInteractable>();
            _interactable.Configure(new CoopHoldSpec(2, HoldSeconds));

            _botA = CreateBot("BotA", new Vector3(-1f, 0f, 0f));
            _botB = CreateBot("BotB", new Vector3(1f, 0f, 0f));
            Physics.SyncTransforms();
        }

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
        public void ConfigureRejectsNullAndUsesTheGivenSpec()
        {
            Assert.Throws<ArgumentNullException>(() => _interactable.Configure(null));
            Assert.That(_interactable.RequiredCount, Is.EqualTo(2));
            Assert.That(_interactable.HoldSeconds, Is.EqualTo(HoldSeconds));
        }

        [Test]
        public void TheDefaultsComeFromTheSpec()
        {
            var fresh = Track(new GameObject("Fresh")).AddComponent<CoopInteractable>();

            Assert.That(fresh.RequiredCount, Is.EqualTo(CoopHoldSpec.DefaultRequiredCount));
            Assert.That(fresh.HoldSeconds, Is.EqualTo(CoopHoldSpec.DefaultHoldSeconds));
        }

        [Test]
        public void OneHolderAloneDoesNotProgress()
        {
            ExpectCoop("'CoopArea' hold begin holder=BotA");
            ExpectCoop("'CoopArea' holding 1/2");

            Assert.That(_interactable.BeginHold(_botA), Is.True);
            _interactable.Tick(HoldSeconds * 2f);

            Assert.That(_interactable.HoldingCount, Is.EqualTo(1));
            Assert.That(_interactable.Progress, Is.Zero);
            Assert.That(_interactable.IsRunning, Is.False);
            Assert.That(_interactable.IsCompleted, Is.False);
        }

        [Test]
        public void APlayerInsideWithoutPressingDoesNotCount()
        {
            var player = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            player.tag = CoopInteractable.DefaultPlayerTag;
            _interactable.HandleEnter(player.GetComponent<Collider>());

            _interactable.BeginHold(_botA);
            _interactable.Tick(HoldSeconds);

            Assert.That(_interactable.HoldingCount, Is.EqualTo(1), "nothing reads the Interact input in Edit Mode");
            Assert.That(_interactable.Progress, Is.Zero);
        }

        [Test]
        public void TwoHoldersProgressAndCompleteOnce()
        {
            var completed = 0;
            _interactable.Completed += _ => completed++;

            ExpectCoop("'CoopArea' holding 2/2");
            ExpectCoop("'CoopArea' started holding=2/2 elapsed=0.50/1.00");
            ExpectCoop("'CoopArea' completed holding=2/2 elapsed=1.00/1.00");

            _interactable.BeginHold(_botA);
            _interactable.BeginHold(_botB);
            _interactable.Tick(HoldSeconds / 2f);

            Assert.That(_interactable.IsRunning, Is.True);
            Assert.That(_interactable.Progress, Is.EqualTo(0.5f));
            Assert.That(completed, Is.Zero);

            _interactable.Tick(HoldSeconds / 2f);
            _interactable.Tick(HoldSeconds);

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(_interactable.IsCompleted, Is.True);
            Assert.That(_interactable.IsHeldBy(_botA), Is.False, "holders are released on completion");
            Assert.That(_interactable.BeginHold(_botA), Is.False, "cannot hold a completed one");
        }

        [Test]
        public void TheSameHolderTwiceCountsOnce()
        {
            Assert.That(_interactable.BeginHold(_botA), Is.True);
            Assert.That(_interactable.BeginHold(_botA), Is.True);
            _interactable.Tick(HoldSeconds);

            Assert.That(_interactable.HoldingCount, Is.EqualTo(1));
        }

        [Test]
        public void AHolderOutOfRangeIsRefused()
        {
            _botA.transform.position = new Vector3(10f, 0f, 0f);
            Physics.SyncTransforms();
            ExpectCoop("'CoopArea' hold refused holder=BotA reason=out_of_range");

            Assert.That(_interactable.BeginHold(_botA), Is.False);
            Assert.That(_interactable.IsHeldBy(_botA), Is.False);
            Assert.Throws<ArgumentNullException>(() => _interactable.BeginHold(null));
        }

        [Test]
        public void ReleasingStopsTheProgress()
        {
            _interactable.BeginHold(_botA);
            _interactable.BeginHold(_botB);
            _interactable.Tick(HoldSeconds / 2f);

            ExpectCoop("'CoopArea' hold end holder=BotB");
            ExpectCoop("'CoopArea' holding 1/2");
            ExpectCoop("'CoopArea' interrupted holding=1/2 elapsed=0.50/1.00");

            Assert.That(_interactable.EndHold(_botB), Is.True);
            Assert.That(_interactable.EndHold(_botB), Is.False);
            _interactable.Tick(HoldSeconds);

            Assert.That(_interactable.IsRunning, Is.False);
            Assert.That(_interactable.IsCompleted, Is.False);
            Assert.That(_interactable.Progress, Is.EqualTo(0.5f));
        }

        [Test]
        public void WalkingOutOfRangeReleasesAndStopsTheProgress()
        {
            _interactable.BeginHold(_botA);
            _interactable.BeginHold(_botB);
            _interactable.Tick(HoldSeconds / 2f);

            _botB.transform.position = new Vector3(10f, 0f, 0f);
            Physics.SyncTransforms();
            ExpectCoop("'CoopArea' hold end holder=BotB reason=left");
            ExpectCoop("'CoopArea' interrupted holding=1/2 elapsed=0.50/1.00");

            _interactable.Tick(HoldSeconds);

            Assert.That(_interactable.IsHeldBy(_botB), Is.False);
            Assert.That(_interactable.IsRunning, Is.False);
        }

        [Test]
        public void ResetHoldAllowsAnotherCompletion()
        {
            var completed = 0;
            _interactable.Completed += _ => completed++;
            _interactable.BeginHold(_botA);
            _interactable.BeginHold(_botB);
            _interactable.Tick(HoldSeconds);

            _interactable.ResetHold();
            Assert.That(_interactable.IsCompleted, Is.False);

            _interactable.BeginHold(_botA);
            _interactable.BeginHold(_botB);
            _interactable.Tick(HoldSeconds);

            Assert.That(completed, Is.EqualTo(2));
        }

        [Test]
        public void TheDoorOpensWhenTwoHoldersComplete()
        {
            var door = _interactable.gameObject.AddComponent<CoopDoor>();
            var panel = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            panel.name = "DoorPanel";
            panel.transform.localPosition = new Vector3(0f, 1.5f, 3f);
            door.Panel = panel.transform;
            door.OpenOffset = new Vector3(0f, 3f, 0f);
            door.OpenSeconds = 1f;
            Assert.That(door.Bind(), Is.True);

            _interactable.BeginHold(_botA);
            _interactable.Tick(HoldSeconds);
            Assert.That(door.IsOpening, Is.False, "one holder alone does not open the door");

            ExpectCoop("door 'CoopArea' opening");
            ExpectCoop("door 'CoopArea' opened");

            _interactable.BeginHold(_botB);
            _interactable.Tick(HoldSeconds);
            Assert.That(door.IsOpening, Is.True);
            Assert.That(door.IsOpen, Is.False);

            door.Advance(0.5f);
            Assert.That(panel.transform.localPosition.y, Is.EqualTo(3f).Within(1e-4f));

            door.Advance(0.5f);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(panel.transform.localPosition, Is.EqualTo(new Vector3(0f, 4.5f, 3f)));
            Assert.That(door.Open(), Is.False, "opening twice does nothing");
        }

        [Test]
        public void TheDoorWithoutAPanelRefusesToBind()
        {
            var door = _interactable.gameObject.AddComponent<CoopDoor>();
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(CoopDoor.LogTag) + ".*Panel"));

            Assert.That(door.Bind(), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => door.OpenSeconds = -1f);
        }

        [Test]
        public void TheDoorWithZeroSecondsOpensAtOnce()
        {
            var door = _interactable.gameObject.AddComponent<CoopDoor>();
            var panel = Track(new GameObject("DoorPanel"));
            door.Panel = panel.transform;
            door.OpenOffset = Vector3.up;
            door.OpenSeconds = 0f;

            Assert.That(door.Open(), Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(panel.transform.localPosition, Is.EqualTo(Vector3.up));
        }

        private GameObject CreateBot(string botName, Vector3 position)
        {
            var bot = Track(new GameObject(botName));
            bot.transform.position = position;
            return bot;
        }

        private static void ExpectCoop(string message)
        {
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(CoopInteractable.LogTag + " " + message) + "$"));
        }

        private T Track<T>(T obj)
            where T : Object
        {
            _created.Add(obj);
            return obj;
        }
    }
}
