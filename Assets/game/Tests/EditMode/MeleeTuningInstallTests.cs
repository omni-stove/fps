using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Edit Mode では Awake が走らないので、MeleeController は Initialize で結んでから取り付けを呼ぶ。寸法は MeleeControllerTests と同じ。
    public sealed class MeleeTuningInstallTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<InputAction> _actions = new List<InputAction>();
        private GameObject _body;
        private MeleeController _melee;

        private static GameRules Rules()
        {
            return GameRules.Default.WithActions(ActionRules.Default.WithMelee(Melee.On));
        }

        [SetUp]
        public void SetUp()
        {
            // プロジェクトの Resources/FpsKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
            GameContent.Use(Content("Empty"));

            _body = Track(new GameObject("Body"));
            _body.transform.position = new Vector3(0f, 1f, 0f);
            var characterController = _body.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.5f;
            characterController.center = Vector3.zero;

            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(eye, false);
            _body.AddComponent<ViewRig>().Initialize(eye, camera);

            var look = _body.AddComponent<FirstPersonLook>();
            var player = _body.AddComponent<PlayerController>();
            player.Initialize(
                characterController,
                look,
                Action("Move", InputActionType.Value, "Vector2"),
                Action("Sprint"),
                Action("Crouch"),
                Action("Jump"),
                new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f));

            var rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());
            var weapon = camera.gameObject.AddComponent<WeaponController>();
            weapon.Initialize(camera.transform, Action("Attack"), Action("Reload"), rifle);

            _melee = _body.AddComponent<MeleeController>();
            _melee.Initialize(player, weapon, Action("Melee"));
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
        public void TheKitMeleeJsonHoldsTheSpecDefaults()
        {
            Assert.That(MeleeActionInstaller.TuningResourcePath, Is.EqualTo("Tuning/melee"));
            var asset = Resources.Load<TextAsset>(MeleeActionInstaller.TuningResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/melee.json");

            var spec = MeleeJson.Parse(asset.text, "Resources/Tuning/melee.json");
            Resources.UnloadAsset(asset);

            Assert.That(spec.Damage, Is.EqualTo(MeleeSpec.DefaultDamage));
            Assert.That(spec.Reach, Is.EqualTo(MeleeSpec.DefaultReach));
            Assert.That(spec.ArcDegrees, Is.EqualTo(MeleeSpec.DefaultArcDegrees));
            Assert.That(spec.SwingSeconds, Is.EqualTo(MeleeSpec.DefaultSwingSeconds));
            Assert.That(spec.CooldownSeconds, Is.EqualTo(MeleeSpec.DefaultCooldownSeconds));
            Assert.That(spec.ImpactSeconds, Is.EqualTo(MeleeSpec.DefaultImpactSeconds));
            Assert.That(spec.LungeDistance, Is.EqualTo(MeleeSpec.DefaultLungeDistance));
            Assert.That(spec.FinisherSeconds, Is.EqualTo(MeleeSpec.DefaultFinisherSeconds));
            Assert.That(spec.FinisherHealth, Is.EqualTo(MeleeSpec.DefaultFinisherHealth));
            Assert.That(spec.FinisherAmmo, Is.EqualTo(MeleeSpec.DefaultFinisherAmmo));
            Assert.That(spec.FinisherInterruptible, Is.EqualTo(MeleeSpec.DefaultFinisherInterruptible));
            Assert.That(spec.TakedownSeconds, Is.EqualTo(MeleeSpec.DefaultTakedownSeconds));
            Assert.That(spec.TakedownInterruptible, Is.EqualTo(MeleeSpec.DefaultTakedownInterruptible));
            Assert.That(spec.TakedownWitnessAwareness, Is.EqualTo(MeleeSpec.DefaultTakedownWitnessAwareness));
            Assert.That(spec.AlignSeconds, Is.EqualTo(MeleeSpec.DefaultAlignSeconds));
            Assert.That(spec.StandDistance, Is.EqualTo(MeleeSpec.DefaultStandDistance));
            Assert.That(spec.BehindArcDegrees, Is.EqualTo(MeleeSpec.DefaultBehindArcDegrees));
        }

        [Test]
        public void WithoutAnEntryFieldTheInstallerGivesTheResourcesValues()
        {
            new MeleeActionInstaller().Install(_body, Rules());

            Assert.That(_melee.Spec, Is.SameAs(MeleeActionInstaller.Tuning));
            Assert.That(_melee.Spec.Damage, Is.EqualTo(50));
            Assert.That(_melee.enabled, Is.True);
        }

        [Test]
        public void TheInstallersPassTheEntryMeleeJson()
        {
            var content = Content("Game");
            content.MeleeTuning = Text(
                "GameMelee",
                "{ \"damage\": 100, \"reach\": 3, \"alignSeconds\": 0.3, \"standDistance\": 1.5, \"behindArcDegrees\": 120 }");
            GameContent.Use(content);

            new MeleeActionInstaller().Install(_body, Rules());
            new ContextMeleeActionInstaller().Install(_body, Rules());

            Assert.That(_melee.Spec.Damage, Is.EqualTo(100));
            Assert.That(_melee.Spec.Reach, Is.EqualTo(3f));
            Assert.That(_melee.Spec.SwingSeconds, Is.EqualTo(MeleeSpec.DefaultSwingSeconds), "an omitted item keeps its default");

            var context = _body.GetComponent<ContextMeleeAction>();
            Assert.That(context, Is.Not.Null);
            Assert.That(context.BehindArcDegrees, Is.EqualTo(120f));
            var takedown = context.Selector.Find(ContextMeleeMove.TakedownId);
            Assert.That(takedown, Is.Not.Null);
            Assert.That(takedown.AlignSeconds, Is.EqualTo(0.3f));
            Assert.That(takedown.StandDistance, Is.EqualTo(1.5f));
            Assert.That(takedown.Reach, Is.EqualTo(3f));
            Assert.That(context.Selector.Find(ContextMeleeMove.FinisherId).StandDistance, Is.EqualTo(1.5f));
        }

        private InputAction Action(string name, InputActionType type = InputActionType.Button, string controlType = null)
        {
            var action = new InputAction(name, type, expectedControlType: controlType);
            _actions.Add(action);
            return action;
        }

        private GameContent Content(string name)
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = name;
            return content;
        }

        private TextAsset Text(string name, string json)
        {
            var asset = Track(new TextAsset(json));
            asset.name = name;
            return asset;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
