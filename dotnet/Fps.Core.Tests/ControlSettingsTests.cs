using System;
using NUnit.Framework;

namespace Fps.Core.Tests
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
                    "{'version':6,'keyboardMouse':{'sprint':'hold','crouch':'hold','aim':'hold'}," +
                    "'gamepad':{'sprint':'toggle','crouch':'toggle','aim':'hold'}," +
                    "'mouseSensitivity':100,'gamepadSensitivity':100,'adsSensitivity':100," +
                    "'fieldOfView':60,'masterVolume':100,'effectsVolume':100,'showNetStats':false," +
                    "'colorPalette':'standard','showSubtitles':false,'language':'ja'}")));
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
        [TestCase("{'version':7,'gamepad':{'sprint':'hold','crouch':'hold','aim':'hold'}}")]
        [TestCase("{'version':-1,'gamepad':{'sprint':'hold','crouch':'hold','aim':'hold'}}")]
        public void BrokenDataFallsBackToAllDefaults(string text)
        {
            var loaded = ControlSettings.FromJson(Json(text), out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default));
            Assert.That(problems, Has.Count.EqualTo(1));
        }

        [Test]
        public void BrokenDataFallsBackToTheGivenLanguage()
        {
            var loaded = ControlSettings.FromJson("{not json", GameLanguage.English, out var problems);

            Assert.That(loaded, Is.EqualTo(ControlSettings.DefaultFor(GameLanguage.English)));
            Assert.That(problems, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReportsTheVersionItRejected()
        {
            ControlSettings.FromJson(Json("{'version':7}"), out var problems);

            Assert.That(problems[0], Does.Contain("version 7"));
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

            Assert.That(loaded.ToJson(), Does.StartWith(Json("{'version':6,")));
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

            Assert.That(json, Does.EndWith(Json(",'showSubtitles':" + (show ? "true" : "false") + ",'language':'ja'}")));
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
        public void TheDefaultLanguageIsJapanese()
        {
            Assert.That(ControlSettings.Default.Language, Is.EqualTo(GameLanguage.Japanese));
        }

        [Test]
        public void DefaultForChangesOnlyTheLanguage()
        {
            var english = ControlSettings.DefaultFor(GameLanguage.English);

            Assert.That(english.Language, Is.EqualTo(GameLanguage.English));
            Assert.That(english.WithLanguage(GameLanguage.Japanese), Is.EqualTo(ControlSettings.Default));
            Assert.That(ControlSettings.DefaultFor(GameLanguage.Japanese), Is.SameAs(ControlSettings.Default));
        }

        [Test]
        public void WithLanguageChangesOnlyTheLanguage()
        {
            var settings = ControlSettings.Default.With(ControlValue.FieldOfView, 80).WithPalette(ColorPalette.BlueYellow);

            var english = settings.WithLanguage(GameLanguage.English);

            Assert.That(english.Language, Is.EqualTo(GameLanguage.English));
            Assert.That(settings.Language, Is.EqualTo(GameLanguage.Japanese), "the original does not change");
            Assert.That(english.Get(ControlValue.FieldOfView), Is.EqualTo(80));
            Assert.That(english.Palette, Is.EqualTo(ColorPalette.BlueYellow));
            Assert.That(english, Is.Not.EqualTo(settings));
            Assert.That(english.WithLanguage(GameLanguage.English), Is.SameAs(english));
            Assert.That(english.WithLanguage(GameLanguage.Japanese), Is.EqualTo(settings));
            Assert.That(english.WithLanguage(GameLanguage.Japanese).GetHashCode(), Is.EqualTo(settings.GetHashCode()));
        }

        [Test]
        public void WithLanguageRejectsUnknownLanguages()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.Default.WithLanguage(default));
            Assert.Throws<ArgumentOutOfRangeException>(() => ControlSettings.FromJson("{}", default, out _));
        }

        [TestCase("ja", "'language':'ja'")]
        [TestCase("en", "'language':'en'")]
        public void RoundTripsTheLanguage(string code, string written)
        {
            var language = new GameLanguage(code);
            var settings = ControlSettings.Default.WithShowSubtitles(true).WithLanguage(language);

            // 読む側の既定の言語に引きずられず、保存した言語に戻る
            var other = language == GameLanguage.Japanese ? GameLanguage.English : GameLanguage.Japanese;
            var loaded = ControlSettings.FromJson(settings.ToJson(), other, out var problems);

            Assert.That(settings.ToJson(), Does.EndWith(Json("," + written + "}")));
            Assert.That(problems, Is.Empty);
            Assert.That(loaded, Is.EqualTo(settings));
        }

        [TestCase("ja")]
        [TestCase("en")]
        public void ReadsVersionFiveWithTheGivenLanguageAndNoProblems(string fallbackCode)
        {
            var fallback = new GameLanguage(fallbackCode);
            var versionFive = Json(_validV2.Replace("'version':2", "'version':5", StringComparison.Ordinal)
                .Replace(
                    "'effectsVolume':0}",
                    "'effectsVolume':0,'showNetStats':true,'colorPalette':'redGreen','showSubtitles':true}",
                    StringComparison.Ordinal));

            var loaded = ControlSettings.FromJson(versionFive, fallback, out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Language, Is.EqualTo(fallback));
            Assert.That(loaded.ShowSubtitles, Is.True, "the saved value");
            Assert.That(loaded.ToJson(), Does.StartWith(Json("{'version':6,")));
        }

        [Test]
        public void VersionFiveReportsTheLanguageKeyAsUnknown()
        {
            var versionFive = ControlSettings.Default.WithLanguage(GameLanguage.English).ToJson()
                .Replace("\"version\":6", "\"version\":5", StringComparison.Ordinal);

            var loaded = ControlSettings.FromJson(versionFive, GameLanguage.Japanese, out var problems);

            Assert.That(loaded.Language, Is.EqualTo(GameLanguage.Japanese), "a version 5 file cannot have a language");
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'language'"));
        }

        // 言語が欠けている・文字列でない・知らない言語コードなら、その項目だけ読む側の既定の言語
        [TestCase(",'language':'en'", "")]
        [TestCase("'language':'en'", "'language':'EN'")]
        [TestCase("'language':'en'", "'language':'fr'")]
        [TestCase("'language':'en'", "'language':'English'")]
        [TestCase("'language':'en'", "'language':1")]
        [TestCase("'language':'en'", "'language':null")]
        public void BrokenLanguageFallsBackOnlyForTheLanguage(string from, string to)
        {
            var valid = ControlSettings.Default.WithPalette(ColorPalette.RedGreen).WithLanguage(GameLanguage.English).ToJson();
            Assert.That(valid.Contains(Json(from), StringComparison.Ordinal), Is.True);

            var loaded = ControlSettings.FromJson(valid.Replace(Json(from), Json(to), StringComparison.Ordinal), GameLanguage.Japanese, out var problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("language"));
            Assert.That(loaded.Language, Is.EqualTo(GameLanguage.Japanese), "the reader's default");
            Assert.That(loaded.Palette, Is.EqualTo(ColorPalette.RedGreen), "the saved value");
        }

        /// <summary>読みやすさのため ' で書いた JSON を " に直す。</summary>
        private static string Json(string text)
        {
            return text.Replace('\'', '"');
        }
    }
}
