using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// 拠点制圧の同期型 (<see cref="NetControlPointState"/>・<see cref="NetObjectiveStatus"/>)、シーンの拠点から範囲を組む
    /// <see cref="NetControlPoint.TryBuildMap"/>、HUD の <see cref="NetControlPointsView"/>。ネットワークは起動しない。
    /// </summary>
    public sealed class NetControlPointTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _objects)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void StateKeepsTheOwnerAndContestedAndTruncatesTheControlToPercent()
        {
            var state = new NetControlPointState(Team.Bravo, -0.4567f, true);

            Assert.That(state.Owner, Is.EqualTo((Team?)Team.Bravo));
            Assert.That(state.ControlPercent, Is.EqualTo((sbyte)-45));
            Assert.That(state.Control, Is.EqualTo(-0.45f).Within(1e-6f));
            Assert.That(state.LeaningTeam, Is.EqualTo((Team?)Team.Bravo));
            Assert.That(state.Fill, Is.EqualTo(0.45f).Within(1e-6f));
            Assert.That(state.Contested, Is.True);
        }

        [Test]
        public void StateReachesTheEndOnlyWhenTheControlDoes()
        {
            Assert.That(NetControlPointState.ToPercent(0.9999f), Is.EqualTo((sbyte)99));
            Assert.That(NetControlPointState.ToPercent(1f), Is.EqualTo((sbyte)100));
            Assert.That(NetControlPointState.ToPercent(-1f), Is.EqualTo((sbyte)-100));
            Assert.That(NetControlPointState.ToPercent(5f), Is.EqualTo((sbyte)100));
            Assert.That(NetControlPointState.ToPercent(float.NaN), Is.EqualTo((sbyte)0));
        }

        [Test]
        public void NeutralStateHasNoOwnerAndNoLean()
        {
            var neutral = NetControlPointState.Neutral;

            Assert.That(neutral.Owner, Is.Null);
            Assert.That(neutral.LeaningTeam, Is.Null);
            Assert.That(neutral.Fill, Is.Zero);
            Assert.That(neutral, Is.EqualTo(new NetControlPointState(null, 0f, false)));
            Assert.That(new NetControlPointState { OwnerCode = 9 }.Owner, Is.Null, "an unknown owner code reads as neutral");
        }

        [Test]
        public void StatesWithTheSameValuesAreEqual()
        {
            var a = new NetControlPointState(Team.Alpha, 0.5f, false);

            Assert.That(a, Is.EqualTo(new NetControlPointState(Team.Alpha, 0.505f, false)));
            Assert.That(a, Is.Not.EqualTo(new NetControlPointState(Team.Alpha, 0.5f, true)));
            Assert.That(a, Is.Not.EqualTo(new NetControlPointState(null, 0.5f, false)));
        }

        [Test]
        public void ObjectiveStatusReadsThePointsOfEachTeam()
        {
            var status = new NetObjectiveStatus(MatchObjective.Domination, 12, -3);

            Assert.That(status.IsDomination, Is.True);
            Assert.That(status.PointsOf(Team.Alpha), Is.EqualTo(12));
            Assert.That(status.PointsOf(Team.Bravo), Is.Zero);
            Assert.That(NetObjectiveStatus.None.IsDomination, Is.False);
            Assert.That(status, Is.EqualTo(new NetObjectiveStatus(MatchObjective.Domination, 12, -3)));
        }

        [Test]
        public void BuildMapOrdersThePointsByIndex()
        {
            var b = CreatePoint("B", 1, new Vector3(20f, 0f, 0f));
            var a = CreatePoint("A", 0, Vector3.zero);

            Assert.That(NetControlPoint.TryBuildMap(new[] { b, a }, out var map, out var byIndex, out var error), Is.True, error);

            Assert.That(map.Count, Is.EqualTo(2));
            Assert.That(byIndex, Is.EqualTo(new[] { a, b }));
            Assert.That(map.Locate(new System.Numerics.Vector3(19f, 0f, 0f)), Is.EqualTo((int?)1));
            Assert.That(map.Locate(new System.Numerics.Vector3(0f, 0f, 30f)), Is.Null);
        }

        [Test]
        public void BuildMapRejectsNoPointsAndBrokenIndices()
        {
            Assert.That(NetControlPoint.TryBuildMap(new NetControlPoint[0], out _, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("no NetControlPoint"));

            var first = CreatePoint("A", 0, Vector3.zero);
            var duplicate = CreatePoint("A2", 0, new Vector3(20f, 0f, 0f));
            Assert.That(NetControlPoint.TryBuildMap(new[] { first, duplicate }, out _, out _, out error), Is.False);
            Assert.That(error, Does.Contain("more than once"));

            var gap = CreatePoint("C", 2, new Vector3(40f, 0f, 0f));
            Assert.That(NetControlPoint.TryBuildMap(new[] { first, gap }, out _, out _, out error), Is.False);
            Assert.That(error, Does.Contain("out of range"));
        }

        [Test]
        public void TheLayoutHasAHiddenControlPointPanel()
        {
            var element = CloneLayout().Q<VisualElement>(NetControlPointsView.UxmlName);

            Assert.That(element, Is.Not.Null);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ViewDrawsOnePointPerStateAndHidesWhenCleared()
        {
            var root = CloneLayout();
            var view = CreateView();
            view.Bind(root);

            view.Show(new[]
            {
                new NetControlPointState(Team.Alpha, 1f, false),
                new NetControlPointState(null, -0.3f, true),
            });

            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            var points = view.Element.Query<VisualElement>(className: NetControlPointsView.PointClass).ToList();
            Assert.That(points, Has.Count.EqualTo(2));
            Assert.That(points[1].ClassListContains(NetControlPointsView.ContestedClass), Is.True);

            var labels = view.Element.Query<Label>(className: NetControlPointsView.LabelClass).ToList();
            Assert.That(labels[0].text, Is.EqualTo("A"));
            Assert.That(labels[1].text, Is.EqualTo("B"));
            Assert.That(labels[0].style.color.value, Is.EqualTo(view.ColorOf(Team.Alpha)), "the owner's colour");
            Assert.That(labels[1].style.color.value, Is.EqualTo(ControlPointZone.NeutralColor));

            view.Show(new[]
            {
                new NetControlPointState(Team.Alpha, 1f, false),
                new NetControlPointState(null, -0.3f, false),
            });
            Assert.That(points[1].ClassListContains(NetControlPointsView.ContestedClass), Is.False, "the contested mark follows the state");

            view.Clear();
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Element.childCount, Is.Zero);
        }

        [Test]
        public void ViewUsesTheTeamColorsItIsGiven()
        {
            var view = CreateView();

            view.SetTeamColors(Color.green, Color.magenta);

            Assert.That(view.ColorOf(Team.Alpha), Is.EqualTo(Color.green));
            Assert.That(view.ColorOf(Team.Bravo), Is.EqualTo(Color.magenta));
            Assert.That(view.ColorOf(null), Is.EqualTo(ControlPointZone.NeutralColor));
        }

        private NetControlPoint CreatePoint(string name, int index, Vector3 position)
        {
            var pointObject = new GameObject(name);
            _objects.Add(pointObject);
            pointObject.transform.position = position;
            pointObject.AddComponent<NetworkObject>();
            pointObject.AddComponent<ControlPointZone>().Index = index;
            return pointObject.AddComponent<NetControlPoint>();
        }

        private NetControlPointsView CreateView()
        {
            var owner = new GameObject(nameof(NetControlPointTests));
            _objects.Add(owner);
            return owner.AddComponent<NetControlPointsView>();
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }
    }
}
