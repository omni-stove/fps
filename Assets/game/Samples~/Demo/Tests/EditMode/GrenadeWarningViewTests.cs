using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="GrenadeWarningView"/> が爆発の届く距離のグレネードにだけ印を出すことと、<see cref="ThrownGrenade.DampRoll"/> が転がりを減らすことを確かめる。
    /// Edit Mode では OnEnable が走らず <see cref="ThrownGrenade.Live"/> に載らないので、グレネードの並びを直接渡す。
    /// </summary>
    public sealed class GrenadeWarningViewTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
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
        public void TheLayoutHasTheGrenadeWarningArea()
        {
            Assert.That(CloneLayout().Q<VisualElement>(GrenadeWarningView.UxmlName), Is.Not.Null);
        }

        [Test]
        public void WarnsOnlyAboutGrenadesWhoseBlastReachesTheViewer()
        {
            var view = CreateView(out var root);
            view.Bind(root);
            var viewer = CreateViewer();
            var near = SpawnGrenade(new Vector3(0f, 0f, 3f));
            var lethal = SpawnGrenade(new Vector3(1f, 0f, 0f));
            var far = SpawnGrenade(new Vector3(0f, 0f, GrenadeSpec.DefaultDamageRadius + 2f));

            view.Show(viewer, new[] { near, far, lethal });

            Assert.That(view.ShownCount, Is.EqualTo(2), "the far grenade is outside the blast");
            Assert.That(view.ThreatAt(0).Distance, Is.EqualTo(1f).Within(1e-4f), "nearest first");
            Assert.That(view.ThreatAt(0).IsLethal, Is.True);
            Assert.That(view.ThreatAt(1).IsLethal, Is.False);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            var markers = view.Element.Query<VisualElement>(className: GrenadeWarningView.MarkerClass).ToList();
            Assert.That(markers.Count, Is.EqualTo(2));
            Assert.That(markers[0].ClassListContains(GrenadeWarningView.LethalClass), Is.True);
            Assert.That(markers[1].ClassListContains(GrenadeWarningView.LethalClass), Is.False);
        }

        [Test]
        public void HidesWhenNoGrenadeIsNearOrTheHudHidesIt()
        {
            var view = CreateView(out var root);
            view.Bind(root);
            var viewer = CreateViewer();
            var near = SpawnGrenade(new Vector3(0f, 0f, 2f));

            view.Show(viewer, new[] { near });
            Assert.That(view.ShownCount, Is.EqualTo(1));

            view.SetAllowed(false);
            view.Show(viewer, new[] { near });
            Assert.That(view.ShownCount, Is.Zero);

            view.SetAllowed(true);
            view.Show(viewer, new ThrownGrenade[0]);
            Assert.That(view.ShownCount, Is.Zero);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void DoesNotWarnAboutGrenadesThatDoNotExplode()
        {
            var view = CreateView(out var root);
            view.Bind(root);
            var smoke = new GrenadeSpec(
                "ecaf6e57-cb77-4707-9321-6ea5b659408f",
                "grenade.smoke",
                new ImpactSpec(damage: 0, areaEffect: new AreaEffectSpec(5f, 15f, 0.5f, 0)));
            var grenade = ThrownGrenade.Spawn(null, new Vector3(0f, 0f, 1f), Vector3.zero, smoke, null);
            _created.Add(grenade.gameObject);

            view.Show(CreateViewer(), new[] { grenade });

            Assert.That(view.ShownCount, Is.Zero);
        }

        [Test]
        public void DampRollSlowsTheHorizontalSpeedAndSpinButNotTheFall()
        {
            var grenade = SpawnGrenade(Vector3.zero);
            var body = grenade.GetComponent<Rigidbody>();
            body.linearVelocity = new Vector3(4f, -2f, 0f);
            body.angularVelocity = new Vector3(0f, 0f, -10f);

            ThrownGrenade.DampRoll(body, rollDamping: 2f, deltaSeconds: 0.5f);

            var factor = Mathf.Exp(-1f);
            Assert.That(body.linearVelocity.x, Is.EqualTo(4f * factor).Within(1e-4f));
            Assert.That(body.linearVelocity.y, Is.EqualTo(-2f).Within(1e-4f));
            Assert.That(body.angularVelocity.z, Is.EqualTo(-10f * factor).Within(1e-4f));
        }

        [Test]
        public void TheSpecRollDampingReachesTheGrenade()
        {
            var spec = new GrenadeSpec(rollDamping: 1.25f);
            var grenade = ThrownGrenade.Spawn(null, Vector3.zero, Vector3.zero, spec, null);
            _created.Add(grenade.gameObject);

            Assert.That(grenade.RollDamping, Is.EqualTo(1.25f));
        }

        private ThrownGrenade SpawnGrenade(Vector3 position)
        {
            var grenade = ThrownGrenade.Spawn(null, position, Vector3.zero, GrenadeSpec.Default, null);
            _created.Add(grenade.gameObject);
            return grenade;
        }

        private GrenadeWarningView CreateView(out VisualElement root)
        {
            var gameObject = new GameObject(nameof(GrenadeWarningView));
            _created.Add(gameObject);
            root = CloneLayout();
            return gameObject.AddComponent<GrenadeWarningView>();
        }

        private Transform CreateViewer()
        {
            var viewer = new GameObject("viewer");
            _created.Add(viewer);
            viewer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return viewer.transform;
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }
    }
}
