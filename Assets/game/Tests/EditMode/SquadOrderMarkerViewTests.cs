using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 既定の分隊への指示の印 (SquadOrderMarkerView) が、守る場所・狙う敵の上に札付きの印を出し、画面の外なら端に寄せ、
    /// 指示を取り消すと消すことと、既定の生成役 (DefaultSquadOrderViewFactory) と差し替え口 (SquadOrderUi) を確かめる。
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
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());

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
            SquadOrderUi.ViewFactory = null;
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void AHoldOrderIsDrawnWhereItIsWithItsLabel()
        {
            var root = CloneLayout();
            _view.Bind(root);
            _view.Show(_commander);
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the squad-order element");
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing is shown without an order");

            // 指示を出すと持ち主の Changed で表示が置き直す。
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Squad\] screen kind=Hold onScreen=true edge=None "));
            _commander.TryOrderAlong(_eye, Vector3.forward, out _);

            Assert.That(_view.ShownKind, Is.EqualTo(BotErrandKind.Hold));
            Assert.That(_view.ShownLabel, Is.EqualTo(SquadOrderText.Marker(BotErrandKind.Hold)));
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
            _view.Show(_commander);
            _commander.TryOrderAlong(_eye, Vector3.forward, out _);
            _view.Refresh();

            _camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Squad\] screen kind=Hold onScreen=false edge=Bottom "));
            _view.Refresh();
            Assert.That(_view.TryGetPlacement(out var behind), Is.True);
            Assert.That(behind.Edge, Is.EqualTo(PingScreenEdge.Bottom));

            _commander.Recall(out _);
            Assert.That(_view.ShownKind, Is.EqualTo(BotErrandKind.None), "the commander's change event redraws the view");
            Assert.That(_view.TryGetPlacement(out _), Is.False);
        }

        [Test]
        public void TheDefaultFactoryAttachesToTheDefaultHud()
        {
            var factory = new DefaultSquadOrderViewFactory();
            var hud = _view.GetComponent<Hud>();

            Assert.That(factory.Create(hud), Is.SameAs(_view), "a HUD that already has the view keeps it");
            Assert.That(factory.Create(null), Is.Null, "only the default Hud gets the default view");
            Assert.That(SquadOrderUi.ViewFactory, Is.InstanceOf<DefaultSquadOrderViewFactory>(), "the UI assembly registers the default");
            Assert.That(SquadOrderUi.CreateView(null), Is.Null);
        }

        [Test]
        public void TheFactoryCanBeReplacedAndTheControllerBindsTheView()
        {
            var replacement = new FixedFactory();
            SquadOrderUi.ViewFactory = replacement;
            Assert.That(SquadOrderUi.CreateView(_view.GetComponent<Hud>()), Is.SameAs(replacement.View));

            LogAssert.Expect(LogType.Log, "[Squad] view=FakeView");
            _commander.BindView(replacement.View);
            Assert.That(replacement.View.Source, Is.SameAs(_commander));
            Assert.That(_commander.View, Is.SameAs(replacement.View));

            _commander.BindView(null);
            Assert.That(replacement.View.Source, Is.Null, "the old view lets go");

            SquadOrderUi.ViewFactory = null;
            Assert.That(SquadOrderUi.ViewFactory, Is.InstanceOf<DefaultSquadOrderViewFactory>(), "null goes back to the default");
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }

        private sealed class FakeView : ISquadOrderView
        {
            public SquadCommandController Source { get; private set; }

            public void Show(SquadCommandController source)
            {
                Source = source;
            }
        }

        private sealed class FixedFactory : ISquadOrderViewFactory
        {
            public FakeView View { get; } = new FakeView();

            public ISquadOrderView Create(IHud hud) => View;
        }
    }
}
