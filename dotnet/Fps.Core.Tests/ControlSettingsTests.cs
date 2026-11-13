using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ControlSettingsTests
    {
        private const string _valid =
            "{'version':1,'keyboardMouse':{'sprint':'toggle','crouch':'hold','aim':'toggle'}," +
            "'gamepad':{'sprint':'hold','crouch':'toggle','aim':'toggle'}}";

        private static readonly InputDeviceKind[] _devices = { InputDeviceKind.KeyboardMouse, InputDeviceKind.Gamepad };
        private static readonly HoldAction[] _actions = { HoldAction.Sprint, HoldAction.Crouch, HoldAction.Aim };

        [TestCase(InputDeviceKind.KeyboardMouse, HoldAction.Sprint, HoldMode.Hold)]
        [TestCase(InputDeviceKind.KeyboardMouse, HoldAction.Crouch, HoldMode.Hold)]
        [TestCase(InputDeviceKind.KeyboardMouse, HoldAction.Aim, HoldMode.Hold)]
        [TestCase(InputDeviceKind.Gamepad, HoldAction.Sprint, HoldMode.Toggle)]
        [TestCase(InputDeviceKind.Gamepad, HoldAction.Crouch, HoldMode.Toggle)]
        [TestCase(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Hold)]
        public void DefaultModes(InputDeviceKind device, HoldAction action, HoldMode expected)
        {
            Assert.That(ControlSettings.Default.Get(device, action), Is.EqualTo(expected));
        }

        [Test]
        public void WithChangesOnlyOneEntry()
        {
            var changed = ControlSettings.Default.With(InputDeviceKind.KeyboardMouse, HoldAction.Crouch, HoldMode.Toggle);

            Assert.That(changed.Get(InputDeviceKind.KeyboardMouse, HoldAction.Crouch), Is.EqualTo(HoldMode.Toggle));
            Assert.That(ControlSettings.Default.Get(InputDeviceKind.KeyboardMouse, HoldAction.Crouch), Is.EqualTo(HoldMode.Hold));
            foreach (var device in _devices)
            {
                foreach (var action in _actions)
                {
                    if (device == InputDeviceKind.KeyboardMouse && action == HoldAction.Crouch)
                    {
                        continue;
                    }

                    Assert.That(changed.Get(device, action), Is.EqualTo(ControlSettings.Default.Get(device, action)));
                }
            }
        }

        [Test]
        public void WithTheSameModeReturnsTheSameSettings()
        {
            var same = ControlSettings.Default.With(InputDeviceKind.Gamepad, HoldAction.Sprint, HoldMode.Toggle);

            Assert.That(same, Is.SameAs(ControlSettings.Default));
        }

        [Test]
        public void RejectsUnknownEnumValues()
        {
            var settings = ControlSettings.Default;

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.Get((InputDeviceKind)2, HoldAction.Aim));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.Get(InputDeviceKind.Gamepad, (HoldAction)3));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.With(InputDeviceKind.Gamepad, HoldAction.Aim, (HoldMode)2));
        }

        [Test]
        public void EqualityComparesEveryEntry()
        {
            var changed = ControlSettings.Default.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle);
            var changedBack = changed.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Hold);

            Assert.That(changedBack, Is.Not.SameAs(ControlSettings.Default));
            Assert.That(changedBack, Is.EqualTo(ControlSettings.Default));
            Assert.That(changedBack.GetHashCode(), Is.EqualTo(ControlSettings.Default.GetHashCode()));
            Assert.That(changed, Is.Not.EqualTo(ControlSettings.Default));
        }

        [Test]
        public void WritesTheDefaultsInAFixedFormat()
        {
            Assert.That(
                ControlSettings.Default.ToJson(),
                Is.EqualTo(Json(
                    "{'version':1,'keyboardMouse':{'sprint':'hold','crouch':'hold','aim':'hold'}," +
                    "'gamepad':{'sprint':'toggle','crouch':'toggle','aim':'hold'}}")));
        }

        [Test]
        public void RoundTripsEveryEntry()
        {
            foreach (var device in _devices)
            {
                foreach (var action in _actions)
                {
                    var current = ControlSettings.Default.Get(device, action);
                    var flipped = current == HoldMode.Hold ? HoldMode.Toggle : HoldMode.Hold;
                    var settings = ControlSettings.Default.With(device, action, flipped);

                    var loaded = ControlSettings.FromJson(settings.ToJson(), out var problems);

                    Assert.That(problems, Is.Empty);
                    Assert.That(loaded, Is.EqualTo(settings), $"{device} {action}");
                }
            }
        }

        [Test]
        public void ReadsHandWrittenJsonWithWhitespace()
        {
            var loaded = ControlSettings.FromJson("\n{ " + Json(_valid.Substring(1)).Replace(",", " ,\r\n\t", StringComparison.Ordinal), out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle));
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Crouch), Is.EqualTo(HoldMode.Hold));
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Aim), Is.EqualTo(HoldMode.Toggle));
            Assert.That(loaded.Get(InputDeviceKind.Gamepad, HoldAction.Sprint), Is.EqualTo(HoldMode.Hold));
            Assert.That(loaded.Get(InputDeviceKind.Gamepad, HoldAction.Crouch), Is.EqualTo(HoldMode.Toggle));
            Assert.That(loaded.Get(InputDeviceKind.Gamepad, HoldAction.Aim), Is.EqualTo(HoldMode.Toggle));
        }

        // JSON として壊れている・形が違う・バージョンが違うときはすべて初期値
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("{'version':1,}")]
        [TestCase("{'version':1}{}")]
        [TestCase("{'version':1,'version':1}")]
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("'hold'")]
        [TestCase("{}")]
        [TestCase("{'gamepad':{'sprint':'hold','crouch':'hold','aim':'hold'}}")]
        [TestCase("{'version':'1','gamepad':{'sprint':'hold','crouch':'hold','aim':'hold'}}")]
        [TestCase("{'version':0,'gamepad':{'sprint':'hold','crouch':'hold','aim':'hold'}}")]
        [TestCase("{'version':2,'gamepad':{'sprint':'hold','crouch':'hold','aim':'hold'}}")]
        public void BrokenDataFallsBackToAllDefaults(string text)
        {
            var loaded = ControlSettings.FromJson(Json(text), out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default));
            Assert.That(problems, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReportsTheVersionItRejected()
        {
            ControlSettings.FromJson(Json("{'version':2}"), out var problems);

            Assert.That(problems[0], Does.Contain("version 2"));
        }

        // 1 つの操作の値が欠けている・型違い・知らない名前なら、その操作だけ初期値
        [TestCase("'sprint':'hold',", "")]
        [TestCase("'sprint':'hold'", "'sprint':'Hold'")]
        [TestCase("'sprint':'hold'", "'sprint':'run'")]
        [TestCase("'sprint':'hold'", "'sprint':''")]
        [TestCase("'sprint':'hold'", "'sprint':0")]
        [TestCase("'sprint':'hold'", "'sprint':true")]
        [TestCase("'sprint':'hold'", "'sprint':null")]
        [TestCase("'sprint':'hold'", "'sprint':['hold']")]
        [TestCase("'sprint':'hold'", "'sprint':{}")]
        public void BrokenEntryFallsBackOnlyForThatEntry(string from, string to)
        {
            Assert.That(_valid.Contains(from, StringComparison.Ordinal), Is.True);
            var loaded = ControlSettings.FromJson(Json(_valid.Replace(from, to, StringComparison.Ordinal)), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("gamepad.sprint"));
            Assert.That(loaded.Get(InputDeviceKind.Gamepad, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle), "the default");
            Assert.That(loaded.Get(InputDeviceKind.Gamepad, HoldAction.Aim), Is.EqualTo(HoldMode.Toggle), "the saved value");
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle), "the saved value");
        }

        [TestCase("'gamepad':{'sprint':'hold','crouch':'toggle','aim':'toggle'}", "'gamepad':'toggle'")]
        [TestCase(",'gamepad':{'sprint':'hold','crouch':'toggle','aim':'toggle'}", "")]
        public void BrokenDeviceFallsBackOnlyForThatDevice(string from, string to)
        {
            Assert.That(_valid.Contains(from, StringComparison.Ordinal), Is.True);
            var loaded = ControlSettings.FromJson(Json(_valid.Replace(from, to, StringComparison.Ordinal)), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("gamepad"));
            foreach (var action in _actions)
            {
                Assert.That(
                    loaded.Get(InputDeviceKind.Gamepad, action),
                    Is.EqualTo(ControlSettings.Default.Get(InputDeviceKind.Gamepad, action)));
            }

            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle), "the saved value");
        }

        [Test]
        public void EachBrokenEntryIsReported()
        {
            var json = Json(_valid.Replace("'toggle'", "'x'", StringComparison.Ordinal));

            var loaded = ControlSettings.FromJson(json, out var problems);

            // 壊れていない keyboardMouse.crouch と gamepad.sprint の Hold だけが残る。
            Assert.That(problems, Has.Count.EqualTo(4));
            Assert.That(loaded, Is.EqualTo(ControlSettings.Default.With(InputDeviceKind.Gamepad, HoldAction.Sprint, HoldMode.Hold)));
        }

        [TestCase("'version':1,", "'version':1,'mouseSensitivity':'0.1',", "'mouseSensitivity'")]
        [TestCase("'aim':'toggle'}}", "'aim':'toggle','lean':'hold'}}", "'gamepad.lean'")]
        public void UnknownKeysAreReportedAndIgnored(string from, string to, string reported)
        {
            Assert.That(_valid.Contains(from, StringComparison.Ordinal), Is.True);
            var expected = ControlSettings.FromJson(Json(_valid), out _);

            var loaded = ControlSettings.FromJson(Json(_valid.Replace(from, to, StringComparison.Ordinal)), out var problems);

            Assert.That(loaded, Is.EqualTo(expected));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain(reported));
        }

        [Test]
        public void RejectsDeepNestingWithoutThrowing()
        {
            var json = "{\"version\":1,\"x\":" + new string('[', 10000) + new string(']', 10000) + "}";

            var loaded = ControlSettings.FromJson(json, out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default));
            Assert.That(problems, Has.Count.EqualTo(1));
        }

        [Test]
        public void RejectsNullInput()
        {
            Assert.Throws<ArgumentNullException>(() => ControlSettings.FromJson(null, out _));
        }

        /// <summary>読みやすさのため ' で書いた JSON を " に直す。</summary>
        private static string Json(string text)
        {
            return text.Replace('\'', '"');
        }
    }
}
