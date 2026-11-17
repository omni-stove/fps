using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// 既定の分隊への指示の印 (SquadOrderMarkerView) が、GameSignals.SquadOrder に出された守る場所・狙う敵の上に札付きの印を出し、
    /// 画面の外なら端に寄せ、指示を取り消すと消すことを確かめる。
    /// </summary>
    public sealed class SquadOrderMarkerViewTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private WeaponDefinition _rifle;
        private SquadCommandController _commander;
        private Camera _camera;
        private SquadOrderMarkerView _view;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            var cameraObject = Track(new GameObject("ViewCamera"));
            cameraObject.transform.SetPositionAndRotation(_eye, Quaternion.identity);
            _camera = cameraObject.AddComponent<Camera>();

            // batchmode では画面の大きさが決まらないことがあるので、縦横比を決めておく。
            _camera.aspect = 16f / 9f;

            var commanderObject = Track(new GameObject("Commander"));
            commanderObject.transform.position = _eye;
            commanderObject.AddComponent<Damageable>().Initialize("Commander", Damageable.DefaultMaxHealth);
            commanderObject.AddComponent<SideMember>().Side = new Side(0);
            _commander = commanderObject.AddComponent<SquadCommandController>();
            _commander.Configure();

            var bot = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            bot.name = "Ally";
            bot.transform.position = new Vector3(3f, 1f, 0f);
            bot.AddComponent<Damageable>().Initialize("Ally", Damageable.DefaultMaxHealth);
            bot.AddComponent<SideMember>().Side = new Side(0);
            var agent = bot.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            Assert.That(agent.TryInitialize(), Is.True);

            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.name = "Wall";
            wall.transform.position = new Vector3(0f, 1f, 10f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            Physics.SyncTransforms();

            var hudObject = Track(new GameObject("Hud"));
            _view = hudObject.AddComponent<SquadOrderMarkerView>();
            _view.Viewer = _camera;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
            Object.DestroyImmediate(_rifle);
            GameSignals.ResetAll();
        }

        [Test]
        public void AHoldOrderIsDrawnWhereItIsWithItsLabel()
        {
            var root = CloneLayout();
            LogAssert.Expect(LogType.Log, SquadOrderLog.View(nameof(SquadOrderMarkerView)));
            _view.Bind(root);
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the squad-order element");
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing is shown without an order");

            // 指示を出すと GameSignals.SquadOrder に出され、表示はそれを受けて置き直す。
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Squad\] screen kind=Hold onScreen=true edge=None "));
            _commander.TryOrderAlong(_eye, Vector3.forward, out _);

            Assert.That(_view.Order.HasValue, Is.True);
            Assert.That(_view.Order.Value.Order, Is.EqualTo(BotErrandKind.Hold));
            Assert.That(_view.ShownKind, Is.EqualTo(BotErrandKind.Hold));
            Assert.That(_view.ShownLabel, Is.EqualTo("守れ"));
            Assert.That(_view.TryGetPlacement(out var placement), Is.True);
            Assert.That(placement.X, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(placement.Y, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            var marker = _view.Element.Q(className: SquadOrderMarkerView.MarkerClass);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.ClassListContains(SquadOrderMarkerView.HoldClass), Is.True);
            Assert.That(marker.ClassListContains(SquadOrderMarkerView.AttackClass), Is.False);
        }

        [Test]
        public void AnOrderBehindGoesToTheEdgeAndARecallClearsIt()
        {
            _view.Bind(CloneLayout());
            _commander.TryOrderAlong(_eye, Vector3.forward, out _);
            _view.Refresh();

            _camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Squad\] screen kind=Hold onScreen=false edge=Bottom "));
            _view.Refresh();
            Assert.That(_view.TryGetPlacement(out var behind), Is.True);
            Assert.That(behind.Edge, Is.EqualTo(PingScreenEdge.Bottom));

            _commander.Recall(out _);
            Assert.That(GameSignals.SquadOrder.Value, Is.Null, "a recall clears the order");
            Assert.That(_view.ShownKind, Is.EqualTo(BotErrandKind.None), "the cleared signal redraws the view");
            Assert.That(_view.TryGetPlacement(out _), Is.False);
        }

        [Test]
        public void AnAttackMarkerFollowsTheTarget()
        {
            var target = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            target.name = "Foe";
            target.transform.position = new Vector3(0f, 1f, 5f);
            var foe = target.AddComponent<Damageable>();
            foe.Initialize("Foe", Damageable.DefaultMaxHealth);
            target.AddComponent<SideMember>().Side = new Side(1);
            Physics.SyncTransforms();

            _view.Show(new SquadOrderStatus(BotErrandKind.Attack, foe.AimPoint, foe));
            Assert.That(_view.ShownKind, Is.EqualTo(BotErrandKind.Attack));
            Assert.That(_view.TryGetPlacement(out var ahead), Is.True);
            Assert.That(ahead.X, Is.EqualTo(0.5f).Within(1e-3f));

            // 敵が右へ動くと、出し直さなくても次の置き直しで印が付いていく。
            target.transform.position = new Vector3(2f, 1f, 5f);
            Physics.SyncTransforms();
            _view.Refresh();
            Assert.That(_view.TryGetPlacement(out var moved), Is.True);
            Assert.That(moved.X, Is.GreaterThan(0.5f));

            _view.Show(null);
            Assert.That(_view.Order, Is.Null);
            Assert.That(_view.ShownKind, Is.EqualTo(BotErrandKind.None));
        }

        [Test]
        public void BindingCatchesUpWithTheOrderAlreadyPublished()
        {
            _commander.TryOrderAlong(_eye, Vector3.forward, out _);
            Assert.That(_view.Order, Is.Null, "an unbound view does not follow the signal");

            _view.Bind(CloneLayout());
            Assert.That(_view.Order, Is.EqualTo(GameSignals.SquadOrder.Value));
            Assert.That(_view.ShownKind, Is.EqualTo(BotErrandKind.Hold));

            _view.Unbind();
            _commander.Recall(out _);
            Assert.That(_view.Order.HasValue, Is.True, "after unbinding the view keeps what it had");

            _view.Bind(CloneLayout());
            Assert.That(_view.Order, Is.Null, "binding again clears what the signal cleared");
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
