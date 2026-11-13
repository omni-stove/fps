using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="PauseMenu"/> が開閉で時間と画面を切り替えること。入力の止め方は Core の PauseState のテストで確かめ、
    /// ここでは Unity 側の時間・表示・ボタンを見る。Edit Mode では UIDocument が木を作らないことがあるので、
    /// Hud.uxml を複製して <see cref="HudElementView.Bind"/> に渡す。
    /// </summary>
    public sealed class PauseMenuTests
    {
        private Hud _hud;
        private PauseMenu _pause;
        private float _timeScaleBefore;

        [SetUp]
        public void SetUp()
        {
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = 1f;
            _hud = Hud.Create("TestPauseHud", null, null, null);
            _pause = _hud.PauseMenu;
        }

        [TearDown]
        public void TearDown()
        {
            // Edit Mode では OnDisable が走らないので、開いたまま次のテストへ時間を止めて持ち越さない
            if (_pause != null)
            {
                _pause.Close();
            }

            if (_hud != null)
            {
                var settings = _hud.OwnedPanelSettings;
                Object.DestroyImmediate(_hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }
            }

            Time.timeScale = _timeScaleBefore;
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        [Test]
        public void TheHudHasAPauseMenuThatStartsClosedAndKeepsTimeRunning()
        {
            Assert.That(_pause, Is.Not.Null);
            Assert.That(_pause.IsOpen, Is.False);
            Assert.That(_pause.TimeMode, Is.EqualTo(PauseTimeMode.KeepRunning), "online must not freeze time unless a caller asks for it");
            Assert.That(_pause.ElementName, Is.EqualTo(PauseMenu.UxmlName));
        }

        [Test]
        public void TheLayoutHasThePauseMenuAndItsButtons()
        {
            var root = CloneLayout();
            var element = root.Q<VisualElement>(PauseMenu.UxmlName);

            Assert.That(element, Is.Not.Null);
            Assert.That(element.Q<Button>(PauseMenu.ResumeButtonName), Is.Not.Null);
            Assert.That(element.Q<Button>(PauseMenu.SettingsButtonName), Is.Not.Null);
            Assert.That(element.Q<Button>(PauseMenu.ReturnToMenuButtonName), Is.Not.Null);
            Assert.That(element.Q<Label>(PauseMenu.NoteLabelName), Is.Not.Null);
        }

        [Test]
        public void FreezeStopsTimeWhileOpenAndRestoresItOnClose()
        {
            _pause.TimeMode = PauseTimeMode.Freeze;
            Time.timeScale = 0.75f;

            _pause.Open();

            Assert.That(_pause.IsOpen, Is.True);
            Assert.That(PauseMenu.IsAnyOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            _pause.Close();

            Assert.That(_pause.IsOpen, Is.False);
            Assert.That(PauseMenu.IsAnyOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
        }

        [Test]
        public void KeepRunningLeavesTimeAlone()
        {
            _pause.TimeMode = PauseTimeMode.KeepRunning;

            _pause.Open();

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void ToggleOpensAndCloses()
        {
            var opened = 0;
            var closed = 0;
            _pause.Opened += () => opened++;
            _pause.Closed += () => closed++;

            _pause.Toggle();
            _pause.Toggle();
            _pause.Close();

            Assert.That(opened, Is.EqualTo(1));
            Assert.That(closed, Is.EqualTo(1), "closing an already closed menu does not raise Closed again");
        }

        [Test]
        public void TheTimeModeCannotChangeWhileOpen()
        {
            _pause.TimeMode = PauseTimeMode.Freeze;
            _pause.Open();

            Assert.Throws<InvalidOperationException>(() => _pause.TimeMode = PauseTimeMode.KeepRunning);
        }

        [Test]
        public void TheElementIsShownOnlyWhileOpen()
        {
            _pause.Bind(CloneLayout());
            Assert.That(_pause.Element.style.display.value, Is.EqualTo(DisplayStyle.None));

            _pause.Open();
            Assert.That(_pause.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            _pause.Close();
            Assert.That(_pause.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void SettingsIsDisabledUntilAHandlerIsGiven()
        {
            _pause.Bind(CloneLayout());
            var settings = _pause.Element.Q<Button>(PauseMenu.SettingsButtonName);

            Assert.That(settings.enabledSelf, Is.False);

            _pause.OpenSettings = () => { };
            Assert.That(settings.enabledSelf, Is.True);

            _pause.OpenSettings = null;
            Assert.That(settings.enabledSelf, Is.False);
        }

        [Test]
        public void TheOnlineNoteIsShownOnlyWhenTimeKeepsRunning()
        {
            _pause.Bind(CloneLayout());
            var note = _pause.Element.Q<Label>(PauseMenu.NoteLabelName);

            Assert.That(note.text, Is.EqualTo(PauseMenu.KeepRunningNote));
            Assert.That(note.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            _pause.TimeMode = PauseTimeMode.Freeze;
            Assert.That(note.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ReturnToMenuNeedsPlayMode()
        {
            Assert.Throws<InvalidOperationException>(() => _pause.ReturnToMenu());
        }
    }
}
