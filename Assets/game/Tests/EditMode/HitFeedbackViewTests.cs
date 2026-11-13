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
    /// <see cref="HitMarkerView"/> と <see cref="DamageDirectionView"/> が、Hud.uxml の要素に印と弧を出して時間で消すことを確かめる。
    /// Edit Mode では MonoBehaviour の OnEnable が走らずイベントを購読しないので、Show と Tick を直接呼ぶ。
    /// </summary>
    public sealed class HitFeedbackViewTests
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
        public void TheLayoutHasTheHitMarkerArmsAndTheDamageDirectionArea()
        {
            var root = CloneLayout();

            var marker = root.Q<VisualElement>(HitMarkerView.UxmlName);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Query<VisualElement>(className: "hit-marker__line").ToList(), Has.Count.EqualTo(4));
            Assert.That(root.Q<VisualElement>(DamageDirectionView.UxmlName), Is.Not.Null);
        }

        [Test]
        public void HitMarkerShowsForAMomentThenHides()
        {
            var view = CreateView<HitMarkerView>(out var root);
            view.Catalog = null;
            view.Bind(root);
            var element = view.Element;

            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing before a hit");

            view.Show(new HitConfirm(25, HitZone.Body, killed: false, downed: false));
            Assert.That(view.IsShowing, Is.True);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(element.ClassListContains(HitMarkerView.HeadClass), Is.False);
            Assert.That(element.ClassListContains(HitMarkerView.KillClass), Is.False);

            view.Tick(HitMarkerView.DisplaySeconds * 0.5f);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex), "still within 0.15 s");

            view.Tick(HitMarkerView.DisplaySeconds);
            Assert.That(view.IsShowing, Is.False);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void HeadshotsAndKillsChangeTheMarker()
        {
            var view = CreateView<HitMarkerView>(out var root);
            view.Catalog = null;
            view.Bind(root);
            var element = view.Element;

            view.Show(new HitConfirm(50, HitZone.Head, killed: false, downed: false));
            Assert.That(element.ClassListContains(HitMarkerView.HeadClass), Is.True);
            Assert.That(element.ClassListContains(HitMarkerView.KillClass), Is.False);

            view.Show(new HitConfirm(25, HitZone.Limb, killed: true, downed: false));
            Assert.That(element.ClassListContains(HitMarkerView.HeadClass), Is.False, "the newest hit decides the colour");
            Assert.That(element.ClassListContains(HitMarkerView.KillClass), Is.True);
        }

        [Test]
        public void ANewHitRestartsTheMarkerTime()
        {
            var view = CreateView<HitMarkerView>(out _);
            view.Catalog = null;

            view.Show(new HitConfirm(25, HitZone.Body, killed: false, downed: false));
            view.Tick(HitMarkerView.DisplaySeconds * 0.9f);
            view.Show(new HitConfirm(25, HitZone.Body, killed: false, downed: false));

            Assert.That(view.Remaining, Is.EqualTo(HitMarkerView.DisplaySeconds).Within(1e-6f));
        }

        [Test]
        public void HitMarkerShownBeforeBindingAppearsWhenBound()
        {
            var view = CreateView<HitMarkerView>(out var root);
            view.Catalog = null;

            view.Show(new HitConfirm(25, HitZone.Head, killed: true, downed: false));
            view.Bind(root);

            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.Element.ClassListContains(HitMarkerView.HeadClass), Is.True);
            Assert.That(view.Element.ClassListContains(HitMarkerView.KillClass), Is.True);
        }

        [Test]
        public void HitMarkerWithoutSoundsInTheCatalogAddsNoAudioSource()
        {
            var view = CreateView<HitMarkerView>(out _);
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            _created.Add(catalog);
            view.Catalog = catalog;

            view.Show(new HitConfirm(25, HitZone.Body, killed: true, downed: false));

            Assert.That(view.GetComponent<AudioSource>(), Is.Null);
        }

        [Test]
        public void CatalogPicksTheKillSoundAndFallsBackToTheHitSound()
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            _created.Add(catalog);
            var hit = AudioClip.Create("hit", 441, 1, 44100, false);
            var kill = AudioClip.Create("kill", 441, 1, 44100, false);
            _created.Add(hit);
            _created.Add(kill);

            catalog.HitMarkerSound = hit;
            Assert.That(catalog.HitMarkerSoundFor(killed: true), Is.SameAs(hit));

            catalog.KillMarkerSound = kill;
            Assert.That(catalog.HitMarkerSoundFor(killed: false), Is.SameAs(hit));
            Assert.That(catalog.HitMarkerSoundFor(killed: true), Is.SameAs(kill));
        }

        [Test]
        public void DamageDirectionDrawsAnArcTowardTheSourceAndFadesInOneSecond()
        {
            var view = CreateView<DamageDirectionView>(out var root);
            view.Viewer = CreateViewer(yaw: 0f);
            view.Bind(root);
            var element = view.Element;

            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing before being hit");

            view.Show(new Vector3(10f, 0f, 0f));
            Assert.That(view.ActiveCount, Is.EqualTo(1));
            Assert.That(view.TryGetAngle(0, out var degrees), Is.True);
            Assert.That(degrees, Is.EqualTo(90f).Within(1e-3f), "a shooter on +X is on the right when facing +Z");
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            var arcs = element.Query<VisualElement>(className: DamageDirectionView.ArcClass).ToList();
            Assert.That(arcs, Has.Count.EqualTo(1));
            Assert.That(arcs[0].style.rotate.value.angle.ToDegrees(), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(arcs[0].style.opacity.value, Is.EqualTo(1f).Within(1e-5f));

            view.Tick(0.5f);
            Assert.That(view.OpacityAt(0), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(arcs[0].style.opacity.value, Is.EqualTo(0.5f).Within(1e-4f));

            view.Tick(0.5f);
            Assert.That(view.ActiveCount, Is.EqualTo(0));
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(element.Query<VisualElement>(className: DamageDirectionView.ArcClass).ToList(), Is.Empty);
        }

        [Test]
        public void DamageDirectionFollowsTheViewerTurning()
        {
            var view = CreateView<DamageDirectionView>(out var root);
            var viewer = CreateViewer(yaw: 0f);
            view.Viewer = viewer;
            view.Bind(root);

            view.Show(new Vector3(0f, 0f, -10f));
            Assert.That(view.TryGetAngle(0, out var behind), Is.True);
            Assert.That(behind, Is.EqualTo(180f).Within(1e-3f));

            viewer.rotation = Quaternion.Euler(0f, 180f, 0f);
            view.Tick(0.1f);
            Assert.That(view.TryGetAngle(0, out var ahead), Is.True);
            Assert.That(ahead, Is.EqualTo(0f).Within(1e-3f).Or.EqualTo(360f).Within(1e-3f));
        }

        [Test]
        public void TheSameSourceReusesItsArcAndManySourcesAreCapped()
        {
            var view = CreateView<DamageDirectionView>(out var root);
            view.Viewer = CreateViewer(yaw: 0f);
            view.Bind(root);

            view.Show(new Vector3(5f, 0f, 5f));
            view.Tick(0.4f);
            view.Show(new Vector3(5.2f, 0f, 5f));
            Assert.That(view.ActiveCount, Is.EqualTo(1));
            Assert.That(view.OpacityAt(0), Is.EqualTo(1f).Within(1e-5f), "shown again from the start");

            for (var i = 0; i < DamageDirectionView.MaxIndicators + 3; i++)
            {
                view.Show(new Vector3(10f * Mathf.Cos(i), 0f, 10f * Mathf.Sin(i)) * (i + 2));
            }

            Assert.That(view.ActiveCount, Is.EqualTo(DamageDirectionView.MaxIndicators));
            Assert.That(
                view.Element.Query<VisualElement>(className: DamageDirectionView.ArcClass).ToList(),
                Has.Count.EqualTo(DamageDirectionView.MaxIndicators));
        }

        [Test]
        public void AShotFromStraightAboveKeepsTheArcHidden()
        {
            var view = CreateView<DamageDirectionView>(out var root);
            view.Viewer = CreateViewer(yaw: 0f);
            view.Bind(root);
            view.Show(new Vector3(0f, 5f, 0f));

            var arc = view.Element.Q<VisualElement>(className: DamageDirectionView.ArcClass);
            Assert.That(view.TryGetAngle(0, out _), Is.False);
            Assert.That(arc.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ClearRemovesEveryArc()
        {
            var view = CreateView<DamageDirectionView>(out var root);
            view.Viewer = CreateViewer(yaw: 0f);
            view.Bind(root);
            view.Show(new Vector3(10f, 0f, 0f));
            view.Show(new Vector3(-10f, 0f, 0f));

            view.Clear();

            Assert.That(view.ActiveCount, Is.EqualTo(0));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Element.Query<VisualElement>(className: DamageDirectionView.ArcClass).ToList(), Is.Empty);
        }

        [Test]
        public void RebindingMovesTheArcsToTheNewTree()
        {
            var view = CreateView<DamageDirectionView>(out var root);
            view.Viewer = CreateViewer(yaw: 0f);
            view.Bind(root);
            view.Show(new Vector3(10f, 0f, 0f));
            var oldElement = view.Element;

            view.Bind(CloneLayout());

            Assert.That(oldElement.Query<VisualElement>(className: DamageDirectionView.ArcClass).ToList(), Is.Empty);
            Assert.That(view.Element.Query<VisualElement>(className: DamageDirectionView.ArcClass).ToList(), Has.Count.EqualTo(1));
        }

        [Test]
        public void AHitMarkerTheDensityForbidsShowsNothingAndHidesItsElement()
        {
            var view = CreateView<HitMarkerView>(out var root);
            view.Catalog = null;
            view.Bind(root);
            view.Show(new HitConfirm(25, HitZone.Body, killed: false, downed: false));

            view.SetAllowed(false);
            Assert.That(view.IsAllowed, Is.False);
            Assert.That(view.IsShowing, Is.False, "the marker shown before the switch goes away");
            Assert.That(view.Element.style.visibility.value, Is.EqualTo(Visibility.Hidden));

            view.Show(new HitConfirm(25, HitZone.Head, killed: true, downed: false));
            Assert.That(view.IsShowing, Is.False);

            view.SetAllowed(true);
            Assert.That(view.Element.style.visibility.value, Is.EqualTo(Visibility.Visible));
            view.Show(new HitConfirm(25, HitZone.Body, killed: false, downed: false));
            Assert.That(view.IsShowing, Is.True);
        }

        [Test]
        public void AForbiddenViewStaysHiddenOnANewTree()
        {
            var view = CreateView<HitMarkerView>(out _);
            view.SetAllowed(false);

            view.Bind(CloneLayout());

            Assert.That(view.Element.style.visibility.value, Is.EqualTo(Visibility.Hidden));
        }

        [Test]
        public void ADamageDirectionTheDensityForbidsClearsItsArcsAndIgnoresHits()
        {
            var view = CreateView<DamageDirectionView>(out var root);
            view.Viewer = CreateViewer(yaw: 0f);
            view.Bind(root);
            view.Show(new Vector3(10f, 0f, 0f));

            view.SetAllowed(false);
            Assert.That(view.ActiveCount, Is.EqualTo(0));

            view.Show(new Vector3(-10f, 0f, 0f));
            Assert.That(view.ActiveCount, Is.EqualTo(0));
        }

        private T CreateView<T>(out VisualElement root)
            where T : HudElementView
        {
            var gameObject = new GameObject(typeof(T).Name);
            _created.Add(gameObject);
            root = CloneLayout();
            return gameObject.AddComponent<T>();
        }

        private Transform CreateViewer(float yaw)
        {
            var viewer = new GameObject("viewer");
            _created.Add(viewer);
            viewer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            return viewer.transform;
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }
    }
}
