using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using Fps.Runtime.Traversal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class TraversalTuningTests
    {
        private const string GameJson =
            "{\"slide\": {\"startSpeed\": 22, \"deceleration\": 6, \"endSpeed\": 2, \"minSeconds\": 0.5, \"maxSeconds\": 4, \"slopeAcceleration\": 15},"
            + " \"parachute\": {\"minOpenHeight\": 10, \"descentSpeed\": 3, \"horizontalSpeed\": 7, \"steerAcceleration\": 5},"
            + " \"zipline\": {\"grabRadius\": 2, \"hangOffset\": 1.1, \"regrabSeconds\": 0.8, \"spreadMoveFraction\": 0.5}}";

        private readonly List<Object> _created = new List<Object>();
        private readonly List<InputAction> _actions = new List<InputAction>();
        private GameObject _body;
        private PlayerController _player;

        [SetUp]
        public void SetUp()
        {
            // プロジェクトの Resources/FpsKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
            GameContent.Use(Track(ScriptableObject.CreateInstance<GameContent>()));

            _body = Track(new GameObject("Body"));
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _body.AddComponent<Damageable>().Initialize("Player", 100);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(
                controller,
                look,
                Action("Move", InputActionType.Value, "Vector2"),
                Action("Sprint", InputActionType.Button, null),
                Action("Crouch", InputActionType.Button, null),
                Action("Jump", InputActionType.Button, null),
                new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var action in _actions)
            {
                action.Dispose();
            }

            _actions.Clear();
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
        public void TheKitTraversalJsonHoldsTheDefaults()
        {
            var spec = TraversalTuning.Current;

            Assert.That(spec, Is.Not.SameAs(TraversalSpec.Default), "read from Resources/Tuning/traversal.json");
            Assert.That(spec.Slide.StartSpeed, Is.EqualTo(SlideMotion.DefaultStartSpeed));
            Assert.That(spec.Slide.Deceleration, Is.EqualTo(SlideMotion.DefaultDeceleration));
            Assert.That(spec.Slide.EndSpeed, Is.EqualTo(SlideMotion.DefaultEndSpeed));
            Assert.That(spec.Slide.MinSeconds, Is.EqualTo(SlideMotion.DefaultMinSeconds));
            Assert.That(spec.Slide.MaxSeconds, Is.EqualTo(SlideMotion.DefaultMaxSeconds));
            Assert.That(spec.Slide.SlopeAcceleration, Is.EqualTo(SlideMotion.DefaultSlopeAcceleration));
            Assert.That(spec.Parachute.MinOpenHeight, Is.EqualTo(ParachuteMotion.DefaultMinOpenHeight));
            Assert.That(spec.Parachute.DescentSpeed, Is.EqualTo(ParachuteMotion.DefaultDescentSpeed));
            Assert.That(spec.Parachute.HorizontalSpeed, Is.EqualTo(ParachuteMotion.DefaultHorizontalSpeed));
            Assert.That(spec.Parachute.SteerAcceleration, Is.EqualTo(ParachuteMotion.DefaultSteerAcceleration));
            Assert.That(spec.Zipline.GrabRadius, Is.EqualTo(ZiplineMovement.DefaultGrabRadius));
            Assert.That(spec.Zipline.HangOffset, Is.EqualTo(ZiplineMovement.DefaultHangOffset));
            Assert.That(spec.Zipline.RegrabSeconds, Is.EqualTo(ZiplineMovement.DefaultRegrabSeconds));
            Assert.That(spec.Zipline.SpreadMoveFraction, Is.EqualTo(ZiplineMovement.DefaultSpreadMoveFraction));
        }

        [Test]
        public void TheInstallersPassTheGameContentValues()
        {
            UseTraversalJson("GameTraversal", GameJson);
            var rules = GameRules.Default.WithActions(
                ActionRules.Default.WithSlide(Slide.On).WithParachute(Parachute.On).WithZipline(Fps.Core.Zipline.On));

            new SlideActionInstaller().Install(_body, rules);
            new ParachuteActionInstaller().Install(_body, rules);
            new ZiplineActionInstaller().Install(_body, rules);

            var slide = _body.GetComponent<SlideMovement>().Motion;
            Assert.That(slide.StartSpeed, Is.EqualTo(22f));
            Assert.That(slide.Deceleration, Is.EqualTo(6f));
            Assert.That(slide.EndSpeed, Is.EqualTo(2f));
            Assert.That(slide.MinSeconds, Is.EqualTo(0.5f));
            Assert.That(slide.MaxSeconds, Is.EqualTo(4f));
            Assert.That(slide.SlopeAcceleration, Is.EqualTo(15f));

            var parachute = _body.GetComponent<ParachuteMovement>().Motion;
            Assert.That(parachute.MinOpenHeight, Is.EqualTo(10f));
            Assert.That(parachute.DescentSpeed, Is.EqualTo(3f));
            Assert.That(parachute.HorizontalSpeed, Is.EqualTo(7f));
            Assert.That(parachute.SteerAcceleration, Is.EqualTo(5f));

            var zipline = _body.GetComponent<ZiplineMovement>();
            Assert.That(zipline.GrabRadius, Is.EqualTo(2f));
            Assert.That(zipline.HangOffset, Is.EqualTo(1.1f));
            Assert.That(zipline.RegrabSeconds, Is.EqualTo(0.8f));
            Assert.That(zipline.SpreadMoveFraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void InstallingAgainWithTheSameValuesKeepsTheMotions()
        {
            UseTraversalJson("GameTraversal", GameJson);
            var rules = GameRules.Default.WithActions(ActionRules.Default.WithSlide(Slide.On).WithParachute(Parachute.On));
            new SlideActionInstaller().Install(_body, rules);
            new ParachuteActionInstaller().Install(_body, rules);
            var slide = _body.GetComponent<SlideMovement>().Motion;
            var parachute = _body.GetComponent<ParachuteMovement>().Motion;

            new SlideActionInstaller().Install(_body, rules);
            new ParachuteActionInstaller().Install(_body, rules);

            Assert.That(_body.GetComponent<SlideMovement>().Motion, Is.SameAs(slide));
            Assert.That(_body.GetComponent<ParachuteMovement>().Motion, Is.SameAs(parachute));
        }

        [Test]
        public void ABrokenTraversalJsonLogsTheItemAndUsesTheDefaults()
        {
            UseTraversalJson("BrokenTraversal", "{\"slide\": {\"maxSeconds\": 0}}");
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"^\[Tuning\] traversal JSON 'GameContent '.*' \(BrokenTraversal\)' item 'slide\.maxSeconds' is invalid: .*; using the default values instead$", RegexOptions.Singleline));

            new SlideActionInstaller().Install(_body, GameRules.Default.WithActions(ActionRules.Default.WithSlide(Slide.On)));

            Assert.That(_body.GetComponent<SlideMovement>().Motion.MaxSeconds, Is.EqualTo(SlideMotion.DefaultMaxSeconds));
        }

        private void UseTraversalJson(string name, string json)
        {
            var text = Track(new TextAsset(json));
            text.name = name;
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Game";
            content.TraversalTuning = text;
            GameContent.Use(content);
        }

        private InputAction Action(string name, InputActionType type, string controlType)
        {
            var action = new InputAction(name, type, expectedControlType: controlType);
            _actions.Add(action);
            return action;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
