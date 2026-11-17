using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ControlSettingsTests
    {
        private const string _valid =
            "{'version':1,'keyboardMouse':{'sprint':'toggle','crouch':'hold','aim':'toggle'}," +
            "'gamepad':{'sprint':'hold','crouch':'toggle','aim':'toggle'}}";

        private const string _validV2 =
            "{'version':2,'keyboardMouse':{'sprint':'toggle','crouch':'hold','aim':'toggle'}," +
            "'gamepad':{'sprint':'hold','crouch':'toggle','aim':'toggle'}," +
            "'mouseSensitivity':150,'gamepadSensitivity':80,'adsSensitivity':120," +
            "'fieldOfView':75,'masterVolume':40,'effectsVolume':0}";

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
                    "{'version':10,'keyboardMouse':{'sprint':'hold','crouch':'hold','aim':'hold'}," +
                    "'gamepad':{'sprint':'toggle','crouch':'toggle','aim':'hold'}," +
                    "'mouseSensitivity':100,'gamepadSensitivity':100,'adsSensitivity':100," +
                    "'fieldOfView':60,'masterVolume':100,'effectsVolume':100,'cameraShake':100,'viewmodelMotion':100," +
                    "'showBlood':true,'invertLookY':false,'stickDeadzone':125,'rumble':true,'rumbleStrength':100,'showNetStats':false," +
                    "'colorPalette':'standard','showSubtitles':false,'aimAssist':true}")));
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
        [TestCase("{'version':11,'gamepad':{'sprint':'hold','crouch':'hold','aim':'hold'}}")]
        [TestCase("{'version':-1,'gamepad':{'sprint':'hold','crouch':'hold','aim':'hold'}}")]
        public void BrokenDataFallsBackToAllDefaults(string text)
        {
            var loaded = ControlSettings.FromJson(Json(text), out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default));
            Assert.That(problems, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReportsTheVersionItRejected()
        {
            ControlSettings.FromJson(Json("{'version':11}"), out var problems);

            Assert.That(problems[0], Does.Contain("version 11"));
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

        [TestCase(ControlValue.MouseSensitivity, 10, 500, 100)]
        [TestCase(ControlValue.GamepadSensitivity, 10, 500, 100)]
        [TestCase(ControlValue.AdsSensitivity, 10, 200, 100)]
        [TestCase(ControlValue.FieldOfView, 60, 90, 60)]
        [TestCase(ControlValue.MasterVolume, 0, 100, 100)]
        [TestCase(ControlValue.EffectsVolume, 0, 100, 100)]
        [TestCase(ControlValue.CameraShakeIntensity, 0, 100, 100)]
        [TestCase(ControlValue.ViewmodelMotionIntensity, 0, 100, 100)]
        [TestCase(ControlValue.StickDeadzone, 0, 500, 125)]
        [TestCase(ControlValue.RumbleStrength, 0, 100, 100)]
        public void ValueRangesAndDefaults(ControlValue value, int min, int max, int expectedDefault)
        {
            var range = ControlSettings.RangeOf(value);

            Assert.That(range.Min, Is.EqualTo(min));
            Assert.That(range.Max, Is.EqualTo(max));
            Assert.That(range.Default, Is.EqualTo(expectedDefault));
            Assert.That(ControlSettings.Default.Get(value), Is.EqualTo(expectedDefault));
        }

        [Test]
        public void ListsEveryValueOnce()
        {
            Assert.That(ControlSettings.Values, Is.EquivalentTo(Enum.GetValues<ControlValue>()));
            Assert.That(ControlSettings.Values, Is.Unique);
        }

        [Test]
        public void DefaultsKeepTheViewAsItWasBeforeSettings()
        {
            var settings = ControlSettings.Default;

            Assert.That(settings.LookSensitivity(InputDeviceKind.KeyboardMouse), Is.EqualTo(1f));
            Assert.That(settings.LookSensitivity(InputDeviceKind.Gamepad), Is.EqualTo(1f));
            Assert.That(settings.AdsSensitivity, Is.EqualTo(1f));
            Assert.That(settings.FieldOfView, Is.EqualTo(60f));
            Assert.That(settings.MasterVolume, Is.EqualTo(1f));
            Assert.That(settings.EffectsVolume, Is.EqualTo(1f));
        }

        [Test]
        public void WithValueChangesOnlyThatValue()
        {
            var changed = ControlSettings.Default.With(ControlValue.GamepadSensitivity, 250);

            Assert.That(changed.Get(ControlValue.GamepadSensitivity), Is.EqualTo(250));
            Assert.That(changed.LookSensitivity(InputDeviceKind.Gamepad), Is.EqualTo(2.5f));
            Assert.That(changed.LookSensitivity(InputDeviceKind.KeyboardMouse), Is.EqualTo(1f));
            Assert.That(ControlSettings.Default.Get(ControlValue.GamepadSensitivity), Is.EqualTo(100));
            Assert.That(changed, Is.Not.EqualTo(ControlSettings.Default));
            Assert.That(changed.With(ControlValue.GamepadSensitivity, 100), Is.EqualTo(ControlSettings.Default));
        }

        [Test]
        public void WithValueKeepsTheModesAndWithModeKeepsTheValues()
        {
            var settings = ControlSettings.Default
                .With(ControlValue.FieldOfView, 75)
                .With(InputDeviceKind.KeyboardMouse, HoldAction.Aim, HoldMode.Toggle)
                .With(ControlValue.EffectsVolume, 0);

            Assert.That(settings.FieldOfView, Is.EqualTo(75f));
            Assert.That(settings.EffectsVolume, Is.EqualTo(0f));
            Assert.That(settings.Get(InputDeviceKind.KeyboardMouse, HoldAction.Aim), Is.EqualTo(HoldMode.Toggle));
        }

        [Test]
        public void WithTheSameValueReturnsTheSameSettings()
        {
            Assert.That(ControlSettings.Default.With(ControlValue.MasterVolume, 100), Is.SameAs(ControlSettings.Default));
        }

        [TestCase(ControlValue.MouseSensitivity, 9)]
        [TestCase(ControlValue.MouseSensitivity, 501)]
        [TestCase(ControlValue.AdsSensitivity, 201)]
        [TestCase(ControlValue.FieldOfView, 59)]
        [TestCase(ControlValue.FieldOfView, 91)]
        [TestCase(ControlValue.MasterVolume, -1)]
        [TestCase(ControlValue.EffectsVolume, 101)]
        [TestCase(ControlValue.StickDeadzone, -1)]
        [TestCase(ControlValue.StickDeadzone, 501)]
        [TestCase(ControlValue.RumbleStrength, 101)]
        public void WithValueRejectsValuesOutsideTheRange(ControlValue value, int amount)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.Default.With(value, amount));

            Assert.That(e.ParamName, Is.EqualTo("amount"));
        }

        [TestCase(ControlValue.MouseSensitivity, 10)]
        [TestCase(ControlValue.MouseSensitivity, 500)]
        [TestCase(ControlValue.FieldOfView, 90)]
        [TestCase(ControlValue.MasterVolume, 0)]
        public void WithValueAcceptsBothEnds(ControlValue value, int amount)
        {
            Assert.That(ControlSettings.Default.With(value, amount).Get(value), Is.EqualTo(amount));
        }

        [Test]
        public void RejectsUnknownValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.Default.Get((ControlValue)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.Default.With((ControlValue)99, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.RangeOf((ControlValue)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.Default.LookSensitivity((InputDeviceKind)2));
        }

        [Test]
        public void RoundTripsEveryValue()
        {
            foreach (var value in ControlSettings.Values)
            {
                foreach (var amount in new[] { ControlSettings.RangeOf(value).Min, ControlSettings.RangeOf(value).Max })
                {
                    var settings = ControlSettings.Default
                        .With(InputDeviceKind.KeyboardMouse, HoldAction.Sprint, HoldMode.Toggle)
                        .With(value, amount);

                    var loaded = ControlSettings.FromJson(settings.ToJson(), out var problems);

                    Assert.That(problems, Is.Empty);
                    Assert.That(loaded, Is.EqualTo(settings), $"{value}={amount}");
                }
            }
        }

        [Test]
        public void ReadsVersionOneWithDefaultValuesAndNoProblems()
        {
            var loaded = ControlSettings.FromJson(Json(_valid), out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle), "the saved mode");
            Assert.That(loaded.Get(InputDeviceKind.Gamepad, HoldAction.Aim), Is.EqualTo(HoldMode.Toggle), "the saved mode");
            foreach (var value in ControlSettings.Values)
            {
                Assert.That(loaded.Get(value), Is.EqualTo(ControlSettings.RangeOf(value).Default), value.ToString());
            }
        }

        [Test]
        public void SavingVersionOneDataWritesTheCurrentVersion()
        {
            var loaded = ControlSettings.FromJson(Json(_valid), out _);

            Assert.That(loaded.ToJson(), Does.StartWith(Json("{'version':10,")));
            Assert.That(ControlSettings.FromJson(loaded.ToJson(), out var problems), Is.EqualTo(loaded));
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void ReadsHandWrittenVersionTwo()
        {
            var loaded = ControlSettings.FromJson(Json(_validV2), out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Get(ControlValue.MouseSensitivity), Is.EqualTo(150));
            Assert.That(loaded.Get(ControlValue.GamepadSensitivity), Is.EqualTo(80));
            Assert.That(loaded.Get(ControlValue.AdsSensitivity), Is.EqualTo(120));
            Assert.That(loaded.Get(ControlValue.FieldOfView), Is.EqualTo(75));
            Assert.That(loaded.Get(ControlValue.MasterVolume), Is.EqualTo(40));
            Assert.That(loaded.Get(ControlValue.EffectsVolume), Is.EqualTo(0));
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle));
        }

        // 1 つの数値が欠けている・整数でないなら、その数値だけ初期値
        [TestCase("'fieldOfView':75,", "")]
        [TestCase("'fieldOfView':75", "'fieldOfView':'75'")]
        [TestCase("'fieldOfView':75", "'fieldOfView':null")]
        [TestCase("'fieldOfView':75", "'fieldOfView':true")]
        [TestCase("'fieldOfView':75", "'fieldOfView':[75]")]
        public void BrokenValueFallsBackOnlyForThatValue(string from, string to)
        {
            Assert.That(_validV2.Contains(from, StringComparison.Ordinal), Is.True);
            var loaded = ControlSettings.FromJson(Json(_validV2.Replace(from, to, StringComparison.Ordinal)), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("fieldOfView"));
            Assert.That(loaded.Get(ControlValue.FieldOfView), Is.EqualTo(60), "the default");
            Assert.That(loaded.Get(ControlValue.MouseSensitivity), Is.EqualTo(150), "the saved value");
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle), "the saved mode");
        }

        // 小数は SaveJson が読まないので、JSON 全体が壊れている扱いになる
        [Test]
        public void FractionalValueBreaksTheWholeDocument()
        {
            var loaded = ControlSettings.FromJson(
                Json(_validV2.Replace("'fieldOfView':75", "'fieldOfView':75.5", StringComparison.Ordinal)), out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("not valid JSON"));
        }

        [TestCase("'fieldOfView':75", "'fieldOfView':120", ControlValue.FieldOfView, 90)]
        [TestCase("'fieldOfView':75", "'fieldOfView':0", ControlValue.FieldOfView, 60)]
        [TestCase("'masterVolume':40", "'masterVolume':-5", ControlValue.MasterVolume, 0)]
        [TestCase("'mouseSensitivity':150", "'mouseSensitivity':99999999999", ControlValue.MouseSensitivity, 500)]
        public void ValueOutsideTheRangeIsClampedAndReported(string from, string to, ControlValue value, int expected)
        {
            Assert.That(_validV2.Contains(from, StringComparison.Ordinal), Is.True);
            var loaded = ControlSettings.FromJson(Json(_validV2.Replace(from, to, StringComparison.Ordinal)), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("outside"));
            Assert.That(loaded.Get(value), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownKeysInVersionTwoAreReportedAndIgnored()
        {
            var expected = ControlSettings.FromJson(Json(_validV2), out _);

            var loaded = ControlSettings.FromJson(
                Json(_validV2.Replace("'version':2,", "'version':2,'brightness':50,", StringComparison.Ordinal)), out var problems);

            Assert.That(loaded, Is.EqualTo(expected));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'brightness'"));
        }

        [Test]
        public void EqualityAndHashIncludeTheValues()
        {
            var a = ControlSettings.Default.With(ControlValue.AdsSensitivity, 50);
            var b = ControlSettings.Default.With(ControlValue.AdsSensitivity, 50);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(ControlSettings.Default.With(ControlValue.AdsSensitivity, 51)));
        }

        [Test]
        public void RangeRejectsAnInconsistentDefinition()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControlValueRange(10, 5, 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControlValueRange(0, 10, 11));
            Assert.That(new ControlValueRange(0, 10, 5).Clamp(long.MaxValue), Is.EqualTo(10));
            Assert.That(new ControlValueRange(0, 10, 5).Clamp(long.MinValue), Is.EqualTo(0));
        }

        [Test]
        public void NetStatsAreHiddenByDefault()
        {
            Assert.That(ControlSettings.Default.ShowNetStats, Is.False);
        }

        [Test]
        public void WithShowNetStatsChangesOnlyThatFlag()
        {
            var settings = ControlSettings.Default
                .With(ControlValue.FieldOfView, 80)
                .With(InputDeviceKind.KeyboardMouse, HoldAction.Aim, HoldMode.Toggle);

            var shown = settings.WithShowNetStats(true);

            Assert.That(shown.ShowNetStats, Is.True);
            Assert.That(settings.ShowNetStats, Is.False);
            Assert.That(shown.FieldOfView, Is.EqualTo(80f));
            Assert.That(shown.Get(InputDeviceKind.KeyboardMouse, HoldAction.Aim), Is.EqualTo(HoldMode.Toggle));
            Assert.That(shown, Is.Not.EqualTo(settings));
            Assert.That(shown.WithShowNetStats(false), Is.EqualTo(settings));
            Assert.That(shown.WithShowNetStats(false).GetHashCode(), Is.EqualTo(settings.GetHashCode()));
            Assert.That(settings.WithShowNetStats(false), Is.SameAs(settings));
        }

        [Test]
        public void ChangingAModeOrValueKeepsTheNetStatsFlag()
        {
            var shown = ControlSettings.Default.WithShowNetStats(true);

            Assert.That(shown.With(ControlValue.MasterVolume, 10).ShowNetStats, Is.True);
            Assert.That(shown.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle).ShowNetStats, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RoundTripsTheNetStatsFlag(bool show)
        {
            var settings = ControlSettings.Default.With(ControlValue.MouseSensitivity, 200).WithShowNetStats(show);

            var loaded = ControlSettings.FromJson(settings.ToJson(), out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.ShowNetStats, Is.EqualTo(show));
        }

        [Test]
        public void ReadsVersionTwoWithTheNetStatsHiddenAndNoProblems()
        {
            var loaded = ControlSettings.FromJson(Json(_validV2), out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.ShowNetStats, Is.False);
            Assert.That(loaded.Get(ControlValue.FieldOfView), Is.EqualTo(75), "the saved value");
        }

        [Test]
        public void VersionTwoReportsTheNetStatsKeyAsUnknown()
        {
            var loaded = ControlSettings.FromJson(
                Json(_validV2.Replace("'effectsVolume':0}", "'effectsVolume':0,'showNetStats':true}", StringComparison.Ordinal)),
                out var problems);

            Assert.That(loaded.ShowNetStats, Is.False);
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'showNetStats'"));
        }

        // 回線の表示が欠けている・真偽値でないなら、その項目だけ初期値 (出さない)
        [TestCase(",'showNetStats':true", "")]
        [TestCase("'showNetStats':true", "'showNetStats':'true'")]
        [TestCase("'showNetStats':true", "'showNetStats':1")]
        [TestCase("'showNetStats':true", "'showNetStats':null")]
        public void BrokenNetStatsFlagFallsBackOnlyForThatFlag(string from, string to)
        {
            var valid = Json(_validV2.Replace("'version':2", "'version':3", StringComparison.Ordinal)
                .Replace("'effectsVolume':0}", "'effectsVolume':0,'showNetStats':true}", StringComparison.Ordinal));
            Assert.That(valid.Contains(Json(from), StringComparison.Ordinal), Is.True);

            var loaded = ControlSettings.FromJson(valid.Replace(Json(from), Json(to), StringComparison.Ordinal), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("showNetStats"));
            Assert.That(loaded.ShowNetStats, Is.False, "the default");
            Assert.That(loaded.Get(ControlValue.FieldOfView), Is.EqualTo(75), "the saved value");
        }

        [Test]
        public void ThePaletteIsStandardByDefault()
        {
            Assert.That(ControlSettings.Default.Palette, Is.EqualTo(ColorPalette.Standard));
        }

        [Test]
        public void WithPaletteChangesOnlyThePalette()
        {
            var settings = ControlSettings.Default.With(ControlValue.FieldOfView, 80).WithShowNetStats(true);

            var redGreen = settings.WithPalette(ColorPalette.RedGreen);

            Assert.That(redGreen.Palette, Is.EqualTo(ColorPalette.RedGreen));
            Assert.That(settings.Palette, Is.EqualTo(ColorPalette.Standard));
            Assert.That(redGreen.FieldOfView, Is.EqualTo(80f));
            Assert.That(redGreen.ShowNetStats, Is.True);
            Assert.That(redGreen, Is.Not.EqualTo(settings));
            Assert.That(redGreen.WithPalette(ColorPalette.Standard), Is.EqualTo(settings));
            Assert.That(redGreen.WithPalette(ColorPalette.Standard).GetHashCode(), Is.EqualTo(settings.GetHashCode()));
            Assert.That(settings.WithPalette(ColorPalette.Standard), Is.SameAs(settings));
        }

        [Test]
        public void ChangingOtherEntriesKeepsThePalette()
        {
            var blueYellow = ControlSettings.Default.WithPalette(ColorPalette.BlueYellow);

            Assert.That(blueYellow.With(ControlValue.MasterVolume, 10).Palette, Is.EqualTo(ColorPalette.BlueYellow));
            Assert.That(blueYellow.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle).Palette, Is.EqualTo(ColorPalette.BlueYellow));
            Assert.That(blueYellow.WithShowNetStats(true).Palette, Is.EqualTo(ColorPalette.BlueYellow));
        }

        [Test]
        public void WithPaletteRejectsUnknownValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.Default.WithPalette((ColorPalette)3));
        }

        [TestCase(ColorPalette.Standard, "standard")]
        [TestCase(ColorPalette.RedGreen, "redGreen")]
        [TestCase(ColorPalette.BlueYellow, "blueYellow")]
        public void RoundTripsThePalette(ColorPalette palette, string name)
        {
            var settings = ControlSettings.Default.WithPalette(palette);

            var json = settings.ToJson();
            var loaded = ControlSettings.FromJson(json, out var problems);

            Assert.That(json, Does.Contain(Json(",'colorPalette':'" + name + "',")));
            Assert.That(problems, Is.Empty);
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.Palette, Is.EqualTo(palette));
        }

        [Test]
        public void ReadsVersionThreeWithTheStandardPaletteAndNoProblems()
        {
            var versionThree = Json(_validV2.Replace("'version':2", "'version':3", StringComparison.Ordinal)
                .Replace("'effectsVolume':0}", "'effectsVolume':0,'showNetStats':true}", StringComparison.Ordinal));

            var loaded = ControlSettings.FromJson(versionThree, out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Palette, Is.EqualTo(ColorPalette.Standard));
            Assert.That(loaded.ShowNetStats, Is.True, "the saved value");
        }

        [Test]
        public void VersionThreeReportsThePaletteKeyAsUnknown()
        {
            var versionThree = Json(_validV2.Replace("'version':2", "'version':3", StringComparison.Ordinal)
                .Replace("'effectsVolume':0}", "'effectsVolume':0,'showNetStats':true,'colorPalette':'redGreen'}", StringComparison.Ordinal));

            var loaded = ControlSettings.FromJson(versionThree, out var problems);

            Assert.That(loaded.Palette, Is.EqualTo(ColorPalette.Standard));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'colorPalette'"));
        }

        // 色の組が欠けている・文字列でない・知らない名前なら、その項目だけ初期値 (標準)
        [TestCase(",'colorPalette':'redGreen'", "")]
        [TestCase("'colorPalette':'redGreen'", "'colorPalette':'RedGreen'")]
        [TestCase("'colorPalette':'redGreen'", "'colorPalette':'purple'")]
        [TestCase("'colorPalette':'redGreen'", "'colorPalette':1")]
        [TestCase("'colorPalette':'redGreen'", "'colorPalette':null")]
        public void BrokenPaletteFallsBackOnlyForThePalette(string from, string to)
        {
            var valid = Json(_validV2.Replace("'version':2", "'version':4", StringComparison.Ordinal)
                .Replace("'effectsVolume':0}", "'effectsVolume':0,'showNetStats':true,'colorPalette':'redGreen'}", StringComparison.Ordinal));
            Assert.That(ControlSettings.FromJson(valid, out var none).Palette, Is.EqualTo(ColorPalette.RedGreen));
            Assert.That(none, Is.Empty);
            Assert.That(valid.Contains(Json(from), StringComparison.Ordinal), Is.True);

            var loaded = ControlSettings.FromJson(valid.Replace(Json(from), Json(to), StringComparison.Ordinal), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("colorPalette"));
            Assert.That(loaded.Palette, Is.EqualTo(ColorPalette.Standard), "the default");
            Assert.That(loaded.ShowNetStats, Is.True, "the saved value");
            Assert.That(loaded.Get(ControlValue.FieldOfView), Is.EqualTo(75), "the saved value");
        }

        [Test]
        public void SubtitlesAreOffByDefault()
        {
            Assert.That(ControlSettings.Default.ShowSubtitles, Is.False);
        }

        [Test]
        public void WithShowSubtitlesChangesOnlyThatFlag()
        {
            var settings = ControlSettings.Default
                .With(ControlValue.FieldOfView, 80)
                .WithShowNetStats(true)
                .WithPalette(ColorPalette.RedGreen);

            var shown = settings.WithShowSubtitles(true);

            Assert.That(shown.ShowSubtitles, Is.True);
            Assert.That(settings.ShowSubtitles, Is.False);
            Assert.That(shown.FieldOfView, Is.EqualTo(80f));
            Assert.That(shown.ShowNetStats, Is.True);
            Assert.That(shown.Palette, Is.EqualTo(ColorPalette.RedGreen));
            Assert.That(shown, Is.Not.EqualTo(settings));
            Assert.That(shown.WithShowSubtitles(false), Is.EqualTo(settings));
            Assert.That(shown.WithShowSubtitles(false).GetHashCode(), Is.EqualTo(settings.GetHashCode()));
            Assert.That(settings.WithShowSubtitles(false), Is.SameAs(settings));
        }

        [Test]
        public void ChangingOtherEntriesKeepsTheSubtitles()
        {
            var shown = ControlSettings.Default.WithShowSubtitles(true);

            Assert.That(shown.With(ControlValue.MasterVolume, 10).ShowSubtitles, Is.True);
            Assert.That(shown.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle).ShowSubtitles, Is.True);
            Assert.That(shown.WithShowNetStats(true).ShowSubtitles, Is.True);
            Assert.That(shown.WithPalette(ColorPalette.BlueYellow).ShowSubtitles, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RoundTripsTheSubtitles(bool show)
        {
            var settings = ControlSettings.Default.WithPalette(ColorPalette.BlueYellow).WithShowSubtitles(show);

            var json = settings.ToJson();
            var loaded = ControlSettings.FromJson(json, out var problems);

            Assert.That(json, Does.EndWith(Json(",'showSubtitles':" + (show ? "true" : "false") + ",'aimAssist':true}")));
            Assert.That(problems, Is.Empty);
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.ShowSubtitles, Is.EqualTo(show));
        }

        [Test]
        public void ReadsVersionFourWithTheSubtitlesOffAndNoProblems()
        {
            var versionFour = Json(_validV2.Replace("'version':2", "'version':4", StringComparison.Ordinal)
                .Replace("'effectsVolume':0}", "'effectsVolume':0,'showNetStats':true,'colorPalette':'redGreen'}", StringComparison.Ordinal));

            var loaded = ControlSettings.FromJson(versionFour, out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.ShowSubtitles, Is.False);
            Assert.That(loaded.Palette, Is.EqualTo(ColorPalette.RedGreen), "the saved value");
        }

        [Test]
        public void VersionFourReportsTheSubtitlesKeyAsUnknown()
        {
            var versionFour = Json(_validV2.Replace("'version':2", "'version':4", StringComparison.Ordinal)
                .Replace(
                    "'effectsVolume':0}",
                    "'effectsVolume':0,'showNetStats':true,'colorPalette':'redGreen','showSubtitles':true}",
                    StringComparison.Ordinal));

            var loaded = ControlSettings.FromJson(versionFour, out var problems);

            Assert.That(loaded.ShowSubtitles, Is.False);
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'showSubtitles'"));
        }

        // 字幕が欠けている・真偽値でないなら、その項目だけ初期値 (出さない)
        [TestCase(",'showSubtitles':true", "")]
        [TestCase("'showSubtitles':true", "'showSubtitles':'true'")]
        [TestCase("'showSubtitles':true", "'showSubtitles':1")]
        [TestCase("'showSubtitles':true", "'showSubtitles':null")]
        public void BrokenSubtitlesFlagFallsBackOnlyForThatFlag(string from, string to)
        {
            var valid = Json(_validV2.Replace("'version':2", "'version':5", StringComparison.Ordinal)
                .Replace(
                    "'effectsVolume':0}",
                    "'effectsVolume':0,'showNetStats':true,'colorPalette':'redGreen','showSubtitles':true}",
                    StringComparison.Ordinal));
            Assert.That(ControlSettings.FromJson(valid, out var none).ShowSubtitles, Is.True);
            Assert.That(none, Is.Empty);
            Assert.That(valid.Contains(Json(from), StringComparison.Ordinal), Is.True);

            var loaded = ControlSettings.FromJson(valid.Replace(Json(from), Json(to), StringComparison.Ordinal), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("showSubtitles"));
            Assert.That(loaded.ShowSubtitles, Is.False, "the default");
            Assert.That(loaded.ShowNetStats, Is.True, "the saved value");
            Assert.That(loaded.Palette, Is.EqualTo(ColorPalette.RedGreen), "the saved value");
        }

        [Test]
        public void TheCurrentFormatHasNoLanguage()
        {
            var json = ControlSettings.Default.WithShowSubtitles(true).ToJson();

            Assert.That(json, Does.StartWith(Json("{'version':10,")));
            Assert.That(json, Does.EndWith(Json(",'showSubtitles':true,'aimAssist':true}")));
            Assert.That(json, Does.Not.Contain("language"));
        }

        [TestCase(6)]
        [TestCase(7)]
        public void VersionsSixAndSevenSkipTheirLanguageWithoutProblems(int version)
        {
            var settings = ControlSettings.Default.WithShowSubtitles(true).WithPalette(ColorPalette.RedGreen);
            var json = version == 6 ? WithoutPresentation(settings.ToJson()) : settings.ToJson();
            var old = AsVersion(json, version);
            var withLanguage = old.Insert(old.Length - 1, ",\"language\":\"fr\"");

            var loaded = ControlSettings.FromJson(withLanguage, out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.ToJson(), Does.StartWith(Json("{'version':10,")).And.Not.Contain("language"));
        }

        [TestCase(5)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void AVersionWithoutALanguageReportsTheLanguageKeyAsUnknown(int version)
        {
            var json = version >= 8 ? ControlSettings.Default.ToJson() : WithoutPresentation(ControlSettings.Default.ToJson());

            var withLanguage = AsVersion(json, version);
            withLanguage = withLanguage.Insert(withLanguage.Length - 1, ",\"language\":\"en\"");

            var loaded = ControlSettings.FromJson(withLanguage, out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'language'"));
        }

        [Test]
        public void PresentationDefaultsKeepTheEffectsAsTheyWere()
        {
            var settings = ControlSettings.Default;

            Assert.That(settings.CameraShakeIntensity, Is.EqualTo(1f));
            Assert.That(settings.ViewmodelMotionIntensity, Is.EqualTo(1f));
            Assert.That(settings.ShowBlood, Is.True);
        }

        [Test]
        public void IntensitiesAreFractionsOfOne()
        {
            var settings = ControlSettings.Default
                .With(ControlValue.CameraShakeIntensity, 0)
                .With(ControlValue.ViewmodelMotionIntensity, 25);

            Assert.That(settings.CameraShakeIntensity, Is.EqualTo(0f));
            Assert.That(settings.ViewmodelMotionIntensity, Is.EqualTo(0.25f));
        }

        [Test]
        public void WithShowBloodChangesOnlyThatFlag()
        {
            var settings = ControlSettings.Default
                .With(ControlValue.CameraShakeIntensity, 40)
                .WithShowSubtitles(true);

            var hidden = settings.WithShowBlood(false);

            Assert.That(hidden.ShowBlood, Is.False);
            Assert.That(settings.ShowBlood, Is.True, "the original does not change");
            Assert.That(hidden.Get(ControlValue.CameraShakeIntensity), Is.EqualTo(40));
            Assert.That(hidden.ShowSubtitles, Is.True);
            Assert.That(hidden, Is.Not.EqualTo(settings));
            Assert.That(hidden.WithShowBlood(true), Is.EqualTo(settings));
            Assert.That(hidden.WithShowBlood(true).GetHashCode(), Is.EqualTo(settings.GetHashCode()));
            Assert.That(settings.WithShowBlood(true), Is.SameAs(settings));
        }

        [Test]
        public void ChangingOtherEntriesKeepsTheBloodFlag()
        {
            var hidden = ControlSettings.Default.WithShowBlood(false);

            Assert.That(hidden.With(ControlValue.MasterVolume, 10).ShowBlood, Is.False);
            Assert.That(hidden.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle).ShowBlood, Is.False);
            Assert.That(hidden.WithShowNetStats(true).ShowBlood, Is.False);
            Assert.That(hidden.WithPalette(ColorPalette.BlueYellow).ShowBlood, Is.False);
            Assert.That(hidden.WithShowSubtitles(true).ShowBlood, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RoundTripsThePresentationSettings(bool showBlood)
        {
            var settings = ControlSettings.Default
                .With(ControlValue.CameraShakeIntensity, 0)
                .With(ControlValue.ViewmodelMotionIntensity, 35)
                .WithShowBlood(showBlood);

            var json = settings.ToJson();
            var loaded = ControlSettings.FromJson(json, out var problems);

            Assert.That(json, Does.Contain(Json(",'cameraShake':0,'viewmodelMotion':35,'showBlood':" + (showBlood ? "true" : "false") + ",")));
            Assert.That(problems, Is.Empty);
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.ShowBlood, Is.EqualTo(showBlood));
        }

        [Test]
        public void ReadsVersionSixWithThePresentationDefaultsAndNoProblems()
        {
            var versionSix = AsVersion(WithoutPresentation(ControlSettings.Default.WithShowSubtitles(true).ToJson()), 6);
            versionSix = versionSix.Insert(versionSix.Length - 1, ",\"language\":\"en\"");
            Assert.That(versionSix, Does.Not.Contain("cameraShake"));

            var loaded = ControlSettings.FromJson(versionSix, out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Get(ControlValue.CameraShakeIntensity), Is.EqualTo(100));
            Assert.That(loaded.Get(ControlValue.ViewmodelMotionIntensity), Is.EqualTo(100));
            Assert.That(loaded.ShowBlood, Is.True);
            Assert.That(loaded.ShowSubtitles, Is.True, "the saved value");
            Assert.That(loaded.ToJson(), Does.StartWith(Json("{'version':10,")));
        }

        [Test]
        public void ReadsVersionTwoWithThePresentationDefaultsAndNoProblems()
        {
            var loaded = ControlSettings.FromJson(Json(_validV2), out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Get(ControlValue.CameraShakeIntensity), Is.EqualTo(100));
            Assert.That(loaded.Get(ControlValue.ViewmodelMotionIntensity), Is.EqualTo(100));
            Assert.That(loaded.ShowBlood, Is.True);
        }

        [Test]
        public void VersionSixReportsThePresentationKeysAsUnknown()
        {
            var versionSix = AsVersion(
                ControlSettings.Default
                    .With(ControlValue.CameraShakeIntensity, 0)
                    .WithShowBlood(false)
                    .ToJson(),
                6);

            var loaded = ControlSettings.FromJson(versionSix, out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default), "a version 6 file cannot have these entries");
            Assert.That(problems, Has.Count.EqualTo(3));
            Assert.That(string.Join("\n", problems), Does.Contain("'cameraShake'").And.Contain("'viewmodelMotion'").And.Contain("'showBlood'"));
        }

        // 血の表示が欠けている・真偽値でないなら、その項目だけ初期値 (出す)
        [TestCase(",'showBlood':false", "")]
        [TestCase("'showBlood':false", "'showBlood':'false'")]
        [TestCase("'showBlood':false", "'showBlood':0")]
        [TestCase("'showBlood':false", "'showBlood':null")]
        public void BrokenBloodFlagFallsBackOnlyForThatFlag(string from, string to)
        {
            var valid = ControlSettings.Default.With(ControlValue.CameraShakeIntensity, 20).WithShowBlood(false).ToJson();
            Assert.That(valid.Contains(Json(from), StringComparison.Ordinal), Is.True);

            var loaded = ControlSettings.FromJson(valid.Replace(Json(from), Json(to), StringComparison.Ordinal), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("showBlood"));
            Assert.That(loaded.ShowBlood, Is.True, "the default");
            Assert.That(loaded.Get(ControlValue.CameraShakeIntensity), Is.EqualTo(20), "the saved value");
        }

        [TestCase("'cameraShake':100,", "", ControlValue.CameraShakeIntensity, 100, "missing")]
        [TestCase("'cameraShake':100", "'cameraShake':150", ControlValue.CameraShakeIntensity, 100, "outside")]
        [TestCase("'viewmodelMotion':100", "'viewmodelMotion':-3", ControlValue.ViewmodelMotionIntensity, 0, "outside")]
        [TestCase("'viewmodelMotion':100", "'viewmodelMotion':'50'", ControlValue.ViewmodelMotionIntensity, 100, "integer")]
        public void BrokenIntensityFallsBackOnlyForThatValue(string from, string to, ControlValue value, int expected, string reported)
        {
            var valid = ControlSettings.Default.WithShowBlood(false).ToJson();
            Assert.That(valid.Contains(Json(from), StringComparison.Ordinal), Is.True);

            var loaded = ControlSettings.FromJson(valid.Replace(Json(from), Json(to), StringComparison.Ordinal), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain(reported));
            Assert.That(loaded.Get(value), Is.EqualTo(expected));
            Assert.That(loaded.ShowBlood, Is.False, "the saved value");
        }

        [Test]
        public void AimAssistIsOnByDefault()
        {
            Assert.That(ControlSettings.Default.AimAssist, Is.True);
        }

        [Test]
        public void WithAimAssistChangesOnlyThatFlag()
        {
            var settings = ControlSettings.Default
                .With(ControlValue.GamepadSensitivity, 150)
                .WithShowSubtitles(true)
                .WithShowBlood(false);

            var off = settings.WithAimAssist(false);

            Assert.That(off.AimAssist, Is.False);
            Assert.That(settings.AimAssist, Is.True, "the original does not change");
            Assert.That(off.Get(ControlValue.GamepadSensitivity), Is.EqualTo(150));
            Assert.That(off.ShowSubtitles, Is.True);
            Assert.That(off.ShowBlood, Is.False);
            Assert.That(off, Is.Not.EqualTo(settings));
            Assert.That(off.WithAimAssist(true), Is.EqualTo(settings));
            Assert.That(off.WithAimAssist(true).GetHashCode(), Is.EqualTo(settings.GetHashCode()));
            Assert.That(settings.WithAimAssist(true), Is.SameAs(settings));
            Assert.That(off.WithAimAssist(false), Is.SameAs(off));
        }

        [Test]
        public void ChangingOtherEntriesKeepsTheAimAssist()
        {
            var off = ControlSettings.Default.WithAimAssist(false);

            Assert.That(off.With(ControlValue.MasterVolume, 10).AimAssist, Is.False);
            Assert.That(off.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle).AimAssist, Is.False);
            Assert.That(off.WithShowNetStats(true).AimAssist, Is.False);
            Assert.That(off.WithPalette(ColorPalette.BlueYellow).AimAssist, Is.False);
            Assert.That(off.WithShowSubtitles(true).AimAssist, Is.False);
            Assert.That(off.WithShowBlood(false).AimAssist, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RoundTripsTheAimAssist(bool enabled)
        {
            var settings = ControlSettings.Default.WithShowSubtitles(true).WithAimAssist(enabled);

            var json = settings.ToJson();
            var loaded = ControlSettings.FromJson(json, out var problems);

            Assert.That(json, Does.EndWith(Json(",'showSubtitles':true,'aimAssist':" + (enabled ? "true" : "false") + "}")));
            Assert.That(problems, Is.Empty);
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.AimAssist, Is.EqualTo(enabled));
        }

        [Test]
        public void ReadsVersionEightWithTheAimAssistOnAndNoProblems()
        {
            var saved = ControlSettings.Default.WithShowBlood(false).WithPalette(ColorPalette.RedGreen);
            var versionEight = AsVersion(saved.ToJson(), 8);
            Assert.That(versionEight, Does.StartWith(Json("{'version':8,")).And.Not.Contain("aimAssist"));

            var loaded = ControlSettings.FromJson(versionEight, out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.AimAssist, Is.True);
            Assert.That(loaded, Is.EqualTo(saved), "every other value is kept");
            Assert.That(loaded.ToJson(), Does.StartWith(Json("{'version':10,")).And.EndWith(Json(",'aimAssist':true}")));
        }

        [Test]
        public void VersionEightReportsTheAimAssistKeyAsUnknown()
        {
            var versionEight = WithoutGamepadFeel(ControlSettings.Default.WithAimAssist(false).ToJson())
                .Replace("\"version\":" + ControlSettings.CurrentVersion, "\"version\":8", StringComparison.Ordinal);

            var loaded = ControlSettings.FromJson(versionEight, out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default), "a version 8 file cannot have this entry");
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'aimAssist'"));
        }

        // エイムアシストが欠けている・真偽値でないなら、その項目だけ初期値 (効かせる)
        [TestCase(",'aimAssist':false", "")]
        [TestCase("'aimAssist':false", "'aimAssist':'false'")]
        [TestCase("'aimAssist':false", "'aimAssist':0")]
        [TestCase("'aimAssist':false", "'aimAssist':null")]
        public void BrokenAimAssistFlagFallsBackOnlyForThatFlag(string from, string to)
        {
            var valid = ControlSettings.Default.WithShowBlood(false).WithAimAssist(false).ToJson();
            Assert.That(valid.Contains(Json(from), StringComparison.Ordinal), Is.True);

            var loaded = ControlSettings.FromJson(valid.Replace(Json(from), Json(to), StringComparison.Ordinal), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("aimAssist"));
            Assert.That(loaded.AimAssist, Is.True, "the default");
            Assert.That(loaded.ShowBlood, Is.False, "the saved value");
        }

        [Test]
        public void GamepadFeelDefaultsKeepTheInputAsItWas()
        {
            var settings = ControlSettings.Default;

            Assert.That(settings.InvertLookY, Is.False);
            Assert.That(settings.StickDeadzone, Is.EqualTo(0.125f));
            Assert.That(settings.RumbleEnabled, Is.True);
            Assert.That(settings.RumbleStrength, Is.EqualTo(1f));
        }

        [Test]
        public void DeadzoneIsHeldInThousandthsAndRumbleStrengthInPercent()
        {
            var settings = ControlSettings.Default.WithStickDeadzone(500).WithRumbleStrength(40);

            Assert.That(settings.Get(ControlValue.StickDeadzone), Is.EqualTo(500));
            Assert.That(settings.StickDeadzone, Is.EqualTo(0.5f));
            Assert.That(settings.Get(ControlValue.RumbleStrength), Is.EqualTo(40));
            Assert.That(settings.RumbleStrength, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(ControlSettings.Default.WithStickDeadzone(0).StickDeadzone, Is.Zero);
            Assert.That(settings.WithStickDeadzone(500), Is.SameAs(settings));
            Assert.That(settings.WithRumbleStrength(40), Is.SameAs(settings));
        }

        [TestCase(-1)]
        [TestCase(501)]
        public void WithStickDeadzoneRejectsValuesOutsideTheRange(int permille)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.Default.WithStickDeadzone(permille));
        }

        [TestCase(-1)]
        [TestCase(101)]
        public void WithRumbleStrengthRejectsValuesOutsideTheRange(int percent)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.Default.WithRumbleStrength(percent));
        }

        [Test]
        public void WithInvertLookYChangesOnlyThatFlag()
        {
            var settings = ControlSettings.Default
                .With(ControlValue.MouseSensitivity, 150)
                .WithAimAssist(false)
                .WithRumbleEnabled(false);

            var inverted = settings.WithInvertLookY(true);

            Assert.That(inverted.InvertLookY, Is.True);
            Assert.That(settings.InvertLookY, Is.False, "the original does not change");
            Assert.That(inverted.Get(ControlValue.MouseSensitivity), Is.EqualTo(150));
            Assert.That(inverted.AimAssist, Is.False);
            Assert.That(inverted.RumbleEnabled, Is.False);
            Assert.That(inverted, Is.Not.EqualTo(settings));
            Assert.That(inverted.WithInvertLookY(false), Is.EqualTo(settings));
            Assert.That(inverted.WithInvertLookY(false).GetHashCode(), Is.EqualTo(settings.GetHashCode()));
            Assert.That(settings.WithInvertLookY(false), Is.SameAs(settings));
            Assert.That(inverted.WithInvertLookY(true), Is.SameAs(inverted));
        }

        [TestCase(2.5f)]
        [TestCase(-0.75f)]
        [TestCase(0f)]
        public void ApplyInvertLookYKeepsTheVerticalLookWhenOff(float lookUp)
        {
            Assert.That(ControlSettings.Default.ApplyInvertLookY(lookUp), Is.EqualTo(lookUp));
            Assert.That(ControlSettings.Default.WithInvertLookY(true).WithInvertLookY(false).ApplyInvertLookY(lookUp), Is.EqualTo(lookUp));
        }

        [TestCase(2.5f, -2.5f)]
        [TestCase(-0.75f, 0.75f)]
        [TestCase(0f, 0f)]
        public void ApplyInvertLookYFlipsTheVerticalLookWhenOn(float lookUp, float expected)
        {
            var inverted = ControlSettings.Default.WithInvertLookY(true);

            Assert.That(inverted.ApplyInvertLookY(lookUp), Is.EqualTo(expected));
            Assert.That(inverted.ApplyInvertLookY(inverted.ApplyInvertLookY(lookUp)), Is.EqualTo(lookUp), "flipping twice gives the input back");
        }

        [Test]
        public void WithRumbleEnabledChangesOnlyThatFlag()
        {
            var settings = ControlSettings.Default
                .WithRumbleStrength(30)
                .WithInvertLookY(true)
                .WithShowBlood(false);

            var off = settings.WithRumbleEnabled(false);

            Assert.That(off.RumbleEnabled, Is.False);
            Assert.That(settings.RumbleEnabled, Is.True, "the original does not change");
            Assert.That(off.Get(ControlValue.RumbleStrength), Is.EqualTo(30), "the strength is kept for when it is turned back on");
            Assert.That(off.InvertLookY, Is.True);
            Assert.That(off.ShowBlood, Is.False);
            Assert.That(off, Is.Not.EqualTo(settings));
            Assert.That(off.WithRumbleEnabled(true), Is.EqualTo(settings));
            Assert.That(off.WithRumbleEnabled(true).GetHashCode(), Is.EqualTo(settings.GetHashCode()));
            Assert.That(settings.WithRumbleEnabled(true), Is.SameAs(settings));
            Assert.That(off.WithRumbleEnabled(false), Is.SameAs(off));
        }

        [Test]
        public void ChangingOtherEntriesKeepsTheGamepadFeel()
        {
            var changed = ControlSettings.Default
                .WithInvertLookY(true)
                .WithStickDeadzone(200)
                .WithRumbleEnabled(false)
                .WithRumbleStrength(50);

            foreach (var other in new[]
            {
                changed.With(ControlValue.MasterVolume, 10),
                changed.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle),
                changed.WithShowNetStats(true),
                changed.WithPalette(ColorPalette.BlueYellow),
                changed.WithShowSubtitles(true),
                changed.WithShowBlood(false),
                changed.WithAimAssist(false),
            })
            {
                Assert.That(other.InvertLookY, Is.True);
                Assert.That(other.Get(ControlValue.StickDeadzone), Is.EqualTo(200));
                Assert.That(other.RumbleEnabled, Is.False);
                Assert.That(other.Get(ControlValue.RumbleStrength), Is.EqualTo(50));
            }
        }

        [Test]
        public void RoundTripsTheGamepadFeel()
        {
            var settings = ControlSettings.Default
                .WithShowSubtitles(true)
                .WithInvertLookY(true)
                .WithStickDeadzone(80)
                .WithRumbleEnabled(false)
                .WithRumbleStrength(35);

            var json = settings.ToJson();
            var loaded = ControlSettings.FromJson(json, out var problems);

            Assert.That(
                json,
                Does.Contain(Json(",'showBlood':true,'invertLookY':true,'stickDeadzone':80,'rumble':false,'rumbleStrength':35,'showNetStats':false,")));
            Assert.That(problems, Is.Empty);
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.InvertLookY, Is.True);
            Assert.That(loaded.StickDeadzone, Is.EqualTo(0.08f).Within(1e-6f));
            Assert.That(loaded.RumbleEnabled, Is.False);
            Assert.That(loaded.RumbleStrength, Is.EqualTo(0.35f).Within(1e-6f));
        }

        // 版 9 (視点の上下反転・デッドゾーン・振動を足す前) の保存を読むと、足した欄は何も言わずに初期値になり、ほかの欄は残る
        [Test]
        public void ReadsVersionNineWithTheGamepadFeelDefaultsAndNoProblems()
        {
            const string versionNine =
                "{'version':9,'keyboardMouse':{'sprint':'toggle','crouch':'hold','aim':'hold'}," +
                "'gamepad':{'sprint':'hold','crouch':'toggle','aim':'toggle'}," +
                "'mouseSensitivity':150,'gamepadSensitivity':80,'adsSensitivity':120," +
                "'fieldOfView':75,'masterVolume':40,'effectsVolume':0,'cameraShake':30,'viewmodelMotion':0," +
                "'showBlood':false,'showNetStats':true," +
                "'colorPalette':'redGreen','showSubtitles':true,'aimAssist':false}";
            var expected = ControlSettings.Default
                .With(InputDeviceKind.KeyboardMouse, HoldAction.Sprint, HoldMode.Toggle)
                .With(InputDeviceKind.Gamepad, HoldAction.Sprint, HoldMode.Hold)
                .With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle)
                .With(ControlValue.MouseSensitivity, 150)
                .With(ControlValue.GamepadSensitivity, 80)
                .With(ControlValue.AdsSensitivity, 120)
                .With(ControlValue.FieldOfView, 75)
                .With(ControlValue.MasterVolume, 40)
                .With(ControlValue.EffectsVolume, 0)
                .With(ControlValue.CameraShakeIntensity, 30)
                .With(ControlValue.ViewmodelMotionIntensity, 0)
                .WithShowBlood(false)
                .WithShowNetStats(true)
                .WithPalette(ColorPalette.RedGreen)
                .WithShowSubtitles(true)
                .WithAimAssist(false);

            var loaded = ControlSettings.FromJson(Json(versionNine), out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded, Is.EqualTo(expected), "every saved value is kept");
            Assert.That(loaded.InvertLookY, Is.False);
            Assert.That(loaded.Get(ControlValue.StickDeadzone), Is.EqualTo(125));
            Assert.That(loaded.RumbleEnabled, Is.True);
            Assert.That(loaded.Get(ControlValue.RumbleStrength), Is.EqualTo(100));
            Assert.That(
                loaded.ToJson(),
                Does.StartWith(Json("{'version':10,"))
                    .And.Contain(Json(",'showBlood':false,'invertLookY':false,'stickDeadzone':125,'rumble':true,'rumbleStrength':100,")));
        }

        [Test]
        public void VersionNineReportsTheGamepadFeelKeysAsUnknown()
        {
            var versionNine = ControlSettings.Default
                .WithInvertLookY(true)
                .WithStickDeadzone(300)
                .WithRumbleEnabled(false)
                .WithRumbleStrength(20)
                .ToJson()
                .Replace("\"version\":" + ControlSettings.CurrentVersion, "\"version\":9", StringComparison.Ordinal);

            var loaded = ControlSettings.FromJson(versionNine, out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default), "a version 9 file cannot have these entries");
            Assert.That(problems, Has.Count.EqualTo(4));
            Assert.That(
                string.Join("\n", problems),
                Does.Contain("'invertLookY'").And.Contain("'stickDeadzone'").And.Contain("'rumble'").And.Contain("'rumbleStrength'"));
        }

        // 上下反転・振動が欠けている・真偽値でないなら、その項目だけ初期値
        [TestCase(",'invertLookY':true", "", "invertLookY")]
        [TestCase("'invertLookY':true", "'invertLookY':'true'", "invertLookY")]
        [TestCase("'invertLookY':true", "'invertLookY':1", "invertLookY")]
        [TestCase(",'rumble':false", "", "rumble")]
        [TestCase("'rumble':false", "'rumble':'off'", "rumble")]
        [TestCase("'rumble':false", "'rumble':null", "rumble")]
        public void BrokenGamepadFeelFlagFallsBackOnlyForThatFlag(string from, string to, string reported)
        {
            var valid = ControlSettings.Default.WithInvertLookY(true).WithRumbleEnabled(false).WithStickDeadzone(250).ToJson();
            Assert.That(valid.Contains(Json(from), StringComparison.Ordinal), Is.True);

            var loaded = ControlSettings.FromJson(valid.Replace(Json(from), Json(to), StringComparison.Ordinal), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'" + reported + "'"));
            Assert.That(loaded.InvertLookY, Is.EqualTo(reported != "invertLookY"));
            Assert.That(loaded.RumbleEnabled, Is.EqualTo(reported == "rumble"));
            Assert.That(loaded.Get(ControlValue.StickDeadzone), Is.EqualTo(250), "the saved value");
        }

        [TestCase("'stickDeadzone':125,", "", ControlValue.StickDeadzone, 125, "missing")]
        [TestCase("'stickDeadzone':125", "'stickDeadzone':900", ControlValue.StickDeadzone, 500, "outside")]
        [TestCase("'stickDeadzone':125", "'stickDeadzone':-4", ControlValue.StickDeadzone, 0, "outside")]
        [TestCase("'rumbleStrength':100", "'rumbleStrength':'50'", ControlValue.RumbleStrength, 100, "integer")]
        [TestCase("'rumbleStrength':100", "'rumbleStrength':250", ControlValue.RumbleStrength, 100, "outside")]
        public void BrokenGamepadFeelValueFallsBackOnlyForThatValue(string from, string to, ControlValue value, int expected, string reported)
        {
            var valid = ControlSettings.Default.WithInvertLookY(true).ToJson();
            Assert.That(valid.Contains(Json(from), StringComparison.Ordinal), Is.True);

            var loaded = ControlSettings.FromJson(valid.Replace(Json(from), Json(to), StringComparison.Ordinal), out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain(reported));
            Assert.That(loaded.Get(value), Is.EqualTo(expected));
            Assert.That(loaded.InvertLookY, Is.True, "the saved value");
        }

        /// <summary>今の形式から、バージョン 7 で足した揺れの強さと血の表示を除く (初期値のときだけ)。</summary>
        private static string WithoutPresentation(string json)
        {
            return json.Replace(Json(",'cameraShake':100,'viewmodelMotion':100,'showBlood':true"), string.Empty, StringComparison.Ordinal);
        }

        /// <summary>今の形式から、バージョン 10 で足した視点の上下反転・デッドゾーン・振動・振動の強さを除く (初期値のときだけ)。</summary>
        private static string WithoutGamepadFeel(string json)
        {
            return json.Replace(
                Json(",'invertLookY':false,'stickDeadzone':125,'rumble':true,'rumbleStrength':100"),
                string.Empty,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// 今の形式の json を version の形にする。10 より前は、バージョン 10 で足した欄を除き、
        /// 9 より前は、バージョン 9 で足したエイムアシストも除く (どちらも初期値のときだけ)。
        /// </summary>
        private static string AsVersion(string json, int version)
        {
            var old = json.Replace("\"version\":" + ControlSettings.CurrentVersion, "\"version\":" + version, StringComparison.Ordinal);
            if (version < 10)
            {
                old = WithoutGamepadFeel(old);
            }

            return version >= 9 ? old : old.Replace(Json(",'aimAssist':true"), string.Empty, StringComparison.Ordinal);
        }

        /// <summary>読みやすさのため ' で書いた JSON を " に直す。</summary>
        private static string Json(string text)
        {
            return text.Replace('\'', '"');
        }
    }
}
