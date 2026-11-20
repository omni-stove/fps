using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class NameplateViewTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private Camera _camera;
        private NameplateView _view;
        private GameObject _player;
        private ControlSettings _settingsBefore;
        private float _volumeBefore;

        [SetUp]
        public void SetUp()
        {
            _settingsBefore = AppliedControlSettings.Current;
            _volumeBefore = AudioListener.volume;
            AppliedControlSettings.Apply(_settingsBefore.WithPalette(ColorPalette.Standard));

            var cameraObject = Track(new GameObject("ViewCamera"));
            cameraObject.transform.SetPositionAndRotation(_eye, Quaternion.identity);
            _camera = cameraObject.AddComponent<Camera>();

            // batchmode では画面の大きさが決まらないことがあるので、縦横比を決めておく。
            _camera.aspect = 16f / 9f;

            _player = Spawn("LocalPlayer", 0, new Vector3(0f, 0f, 0f));
            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(_player, null, null, null, null, null, null, null));

            var hudObject = Track(new GameObject("Hud"));
            _view = hudObject.AddComponent<NameplateView>();
            _view.Viewer = _camera;
            _view.Spec = new NameplateSpec(friendlyRange: 60f, enemyRange: 35f);
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
            GameSignals.ResetAll();
            AppliedControlSettings.Apply(_settingsBefore);
            AudioListener.volume = _volumeBefore;
            KitTuning.Reload();
        }

        [Test]
        public void AnAllyBehindAWallIsNamedButAnEnemyBehindAWallIsNot()
        {
            var ally = Spawn("Ally", 0, new Vector3(-1f, 0f, 20f));
            var enemy = Spawn("Enemy", 1, new Vector3(1f, 0f, 20f));
            Wall(new Vector3(0f, 1f, 10f));

            _view.Refresh();

            Assert.That(_view.TryGetPlate(IdOf(ally), out var plate), Is.True, "allies are shown through walls");
            Assert.That(plate.Text, Is.EqualTo("Ally"), "the name is the participant's name looked up by Id");
            Assert.That(plate.Relation, Is.EqualTo(TeamRelation.Friendly));
            Assert.That(_view.TryGetPlate(IdOf(enemy), out _), Is.False, "an enemy behind a wall would give its position away");
            Assert.That(_view.ShownCount, Is.EqualTo(1));
        }

        [Test]
        public void AnEnemyInSightIsNamedOnlyWithinTheEnemyRange()
        {
            var near = Spawn("Near", 1, new Vector3(-2f, 0f, 30f));
            var far = Spawn("Far", 1, new Vector3(2f, 0f, 50f));

            _view.Refresh();

            Assert.That(_view.TryGetPlate(IdOf(near), out var plate), Is.True);
            Assert.That(plate.Relation, Is.EqualTo(TeamRelation.Enemy));
            Assert.That(plate.Color, Is.EqualTo(TeamColors.ToColor(TeamPalette.Standard.Enemy)));
            Assert.That(_view.TryGetPlate(IdOf(far), out _), Is.False, "50 m is past the 35 m enemy range");
        }

        [Test]
        public void TheRangesComeFromTheSpec()
        {
            var ally = Spawn("Ally", 0, new Vector3(0f, 0f, 30f));
            var enemy = Spawn("Enemy", 1, new Vector3(3f, 0f, 30f));

            _view.Spec = new NameplateSpec(friendlyRange: 20f, enemyRange: 40f);
            _view.Refresh();

            Assert.That(_view.TryGetPlate(IdOf(ally), out _), Is.False, "30 m is past a 20 m friendly range");
            Assert.That(_view.TryGetPlate(IdOf(enemy), out _), Is.True, "30 m is within a 40 m enemy range");

            _view.Spec = new NameplateSpec(friendlyRange: 40f, enemyRange: 20f);
            _view.Refresh();

            Assert.That(_view.TryGetPlate(IdOf(ally), out _), Is.True);
            Assert.That(_view.TryGetPlate(IdOf(enemy), out _), Is.False, "the plate is dropped once it falls out of range");
        }

        [Test]
        public void WithoutASpecTheViewReadsKitTuning()
        {
            var view = Track(new GameObject("OtherHud")).AddComponent<NameplateView>();
            KitTuning.Reload();

            Assert.That(view.Spec.FriendlyRange, Is.EqualTo(KitTuning.Nameplates.FriendlyRange));
            Assert.That(view.Spec.EnemyRange, Is.EqualTo(KitTuning.Nameplates.EnemyRange));
            Assert.Throws<System.ArgumentNullException>(() => view.Spec = null);
            Assert.Throws<System.ArgumentNullException>(() => view.Targets = null);
            Assert.That(view.Targets, Is.InstanceOf<OfflineNameplateTargets>(), "offline targets are used until a game sets its own");
        }

        [Test]
        public void PublishedTargetsReplaceTheOfflineOnesUntilTheyAreWithdrawn()
        {
            var view = Track(new GameObject("OtherHud")).AddComponent<NameplateView>();
            view.Viewer = _camera;
            view.Spec = new NameplateSpec(friendlyRange: 60f, enemyRange: 35f);
            var published = new FakeTargets(
                new FakeParticipants(new Participant(42, "Online", Team.Alpha, false)),
                new NameplateTarget(42, _eye + Vector3.forward * 10f, TeamRelation.Friendly, true, false));
            Assert.That(view.Targets, Is.InstanceOf<OfflineNameplateTargets>());

            GameSignals.NameplateTargets.Publish(published);
            view.Refresh();

            Assert.That(view.Targets, Is.SameAs(published), "the online match's targets are used once published");
            Assert.That(view.TryGetPlate(42, out var plate), Is.True);
            Assert.That(plate.Text, Is.EqualTo("Online"), "the name comes from the published targets' participants");

            GameSignals.NameplateTargets.ClearIfCurrent(published);
            view.Refresh();

            Assert.That(view.Targets, Is.InstanceOf<OfflineNameplateTargets>(), "after leaving the match the offline targets are back");
            Assert.That(view.TryGetPlate(42, out _), Is.False);

            var own = new FakeTargets(new FakeParticipants());
            view.Targets = own;
            GameSignals.NameplateTargets.Publish(published);

            Assert.That(view.Targets, Is.SameAs(own), "targets set on the view win over published ones");
        }

        [Test]
        public void TheLocalPlayerTheDeadAndThoseBehindTheCameraAreNotNamed()
        {
            var dead = Spawn("Dead", 0, new Vector3(0f, 0f, 10f));
            var behind = Spawn("Behind", 0, new Vector3(0f, 0f, -10f));
            dead.GetComponent<Damageable>().Kill();

            _view.Refresh();

            Assert.That(_view.TryGetPlate(IdOf(_player), out _), Is.False, "the local player has no plate");
            Assert.That(_view.TryGetPlate(IdOf(dead), out _), Is.False);
            Assert.That(_view.TryGetPlate(IdOf(behind), out _), Is.False, "a plate is not pinned to the screen edge");
            Assert.That(_view.ShownCount, Is.Zero);
        }

        [Test]
        public void ThePlateSitsAboveTheHeadOnScreen()
        {
            var ally = Spawn("Ally", 0, new Vector3(0f, 0f, 10f));

            _view.Refresh();

            Assert.That(_view.TryGetPlate(IdOf(ally), out var plate), Is.True);
            Assert.That(plate.X, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(NameplateView.TryPlace(_camera, OfflineNameplateTargets.HeadOf(ally.GetComponent<SideMember>(), ally.GetComponent<Damageable>()), out var head), Is.True);
            Assert.That(plate.Y, Is.EqualTo(head.y).Within(1e-4f));
            Assert.That(plate.Y, Is.LessThan(0.5f), "the head of a 2 m capsule is above the eye line, so the plate is in the upper half");
        }

        [Test]
        public void TryPlaceRejectsPointsOffScreenAndBehind()
        {
            Assert.That(NameplateView.TryPlace(_camera, _eye + Vector3.forward * 10f, out var centre), Is.True);
            Assert.That(centre.x, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(centre.y, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(NameplateView.TryPlace(_camera, _eye + Vector3.back * 10f, out _), Is.False);
            Assert.That(NameplateView.TryPlace(_camera, _eye + new Vector3(100f, 0f, 1f), out _), Is.False);
            Assert.Throws<System.ArgumentNullException>(() => NameplateView.TryPlace(null, Vector3.zero, out _));
        }

        [Test]
        public void TheNameIsLookedUpByIdAndAnUnknownIdIsNotShown()
        {
            var targets = new FakeTargets(
                new FakeParticipants(new Participant(5, "Raven", Team.Alpha, false)),
                new NameplateTarget(5, _eye + Vector3.forward * 10f, TeamRelation.Friendly, true, false),
                new NameplateTarget(6, _eye + Vector3.forward * 12f, TeamRelation.Friendly, true, false));
            _view.Targets = targets;

            _view.Refresh();

            Assert.That(targets.Eyes, Is.EqualTo(new[] { _eye }), "targets are gathered from the viewer's eye");
            Assert.That(_view.TryGetPlate(5, out var plate), Is.True);
            Assert.That(plate.Text, Is.EqualTo("Raven"));
            Assert.That(_view.TryGetPlate(6, out _), Is.False, "a target with no name to show gets no plate");
        }

        [Test]
        public void ThePlateCarriesThePalettesMarkAndColor()
        {
            var targets = new FakeTargets(
                new FakeParticipants(new Participant(5, "Raven", Team.Alpha, false), new Participant(6, "Crow", Team.Bravo, false)),
                new NameplateTarget(5, _eye + new Vector3(-1f, 0f, 10f), TeamRelation.Friendly, true, false),
                new NameplateTarget(6, _eye + new Vector3(1f, 0f, 10f), TeamRelation.Enemy, true, true));
            _view.Targets = targets;
            AppliedControlSettings.Apply(_settingsBefore.WithPalette(ColorPalette.RedGreen));

            _view.Refresh();

            Assert.That(_view.TryGetPlate(5, out var friend), Is.True);
            Assert.That(_view.TryGetPlate(6, out var foe), Is.True);
            Assert.That(friend.Text, Is.EqualTo(TeamPalette.FriendlyMark + " Raven"));
            Assert.That(foe.Text, Is.EqualTo(TeamPalette.EnemyMark + " Crow"));
            Assert.That(friend.Color, Is.EqualTo(TeamColors.ToColor(TeamPalette.RedGreen.Friendly)));
            Assert.That(foe.Color, Is.EqualTo(TeamColors.ToColor(TeamPalette.RedGreen.Enemy)));
        }

        [Test]
        public void TheBoundViewAddsALabelPerPlateAndHidesWhenNoneAreShown()
        {
            var targets = new FakeTargets(
                new FakeParticipants(new Participant(5, "Raven", Team.Alpha, false)),
                new NameplateTarget(5, _eye + Vector3.forward * 10f, TeamRelation.Friendly, true, false));
            _view.Targets = targets;

            LogAssert.Expect(LogType.Log, "[Nameplate] view=NameplateView friendlyRange=60.0 enemyRange=35.0");
            LogAssert.Expect(LogType.Log, "[Nameplate] shown id=5 relation=Friendly distance=10.0");
            _view.Bind(CloneLayout());

            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the nameplates element");
            Assert.That(_view.Element.name, Is.EqualTo(NameplateView.UxmlName));
            var labels = _view.Element.Query<Label>(className: NameplateView.PlateClass).ToList();
            Assert.That(labels, Has.Count.EqualTo(1));
            Assert.That(labels[0].text, Is.EqualTo("Raven"));
            Assert.That(labels[0].ClassListContains(NameplateView.FriendlyClass), Is.True);
            Assert.That(labels[0].ClassListContains(NameplateView.EnemyClass), Is.False);
            Assert.That(labels[0].pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(labels[0].style.left.value.value, Is.EqualTo(50f).Within(0.1f));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            targets.Items.Clear();
            LogAssert.Expect(LogType.Log, "[Nameplate] hidden id=5");
            _view.Refresh();

            Assert.That(_view.Element.Query<Label>(className: NameplateView.PlateClass).ToList(), Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void UnbindingRemovesTheLabels()
        {
            _view.Targets = new FakeTargets(
                new FakeParticipants(new Participant(5, "Raven", Team.Alpha, false)),
                new NameplateTarget(5, _eye + Vector3.forward * 10f, TeamRelation.Friendly, true, false));
            var root = CloneLayout();
            _view.Bind(root);
            var element = _view.Element;
            Assert.That(element.childCount, Is.EqualTo(1));

            _view.Unbind();

            Assert.That(element.childCount, Is.Zero);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.ShownCount, Is.Zero);
        }

        [Test]
        public void OfflineTargetsTakeTheRelationFromTheSidesAndSkipTheLocalPlayer()
        {
            var ally = Spawn("Ally", 0, new Vector3(-1f, 0f, 10f));
            var enemy = Spawn("Enemy", 1, new Vector3(1f, 0f, 10f));
            var source = new OfflineNameplateTargets();
            var found = new List<NameplateTarget>();

            source.CopyTo(_eye, found);

            Assert.That(found.Select(t => t.Id), Is.EquivalentTo(new[] { IdOf(ally), IdOf(enemy) }));
            var allyTarget = found.Single(t => t.Id == IdOf(ally));
            var enemyTarget = found.Single(t => t.Id == IdOf(enemy));
            Assert.That(allyTarget.Relation, Is.EqualTo(TeamRelation.Friendly));
            Assert.That(allyTarget.IsInSight, Is.False, "sight is only checked for enemies");
            Assert.That(enemyTarget.Relation, Is.EqualTo(TeamRelation.Enemy));
            Assert.That(enemyTarget.IsInSight, Is.True);
            Assert.That(enemyTarget.IsAlive, Is.True);
            Assert.That(enemyTarget.HeadPosition.y, Is.EqualTo(2f).Within(1e-3f), "the head is the top of the body");
            Assert.That(source.Participants.TryGet(IdOf(enemy), out var participant), Is.True);
            Assert.That(participant.Name, Is.EqualTo("Enemy"));
            Assert.Throws<System.ArgumentNullException>(() => source.CopyTo(_eye, null));
        }

        [Test]
        public void WithoutASideEveryoneIsAnEnemy()
        {
            Object.DestroyImmediate(_player.GetComponent<SideMember>());
            var other = Spawn("Other", 0, new Vector3(0f, 0f, 10f));
            var found = new List<NameplateTarget>();

            new OfflineNameplateTargets().CopyTo(_eye, found);

            Assert.That(found.Single(t => t.Id == IdOf(other)).Relation, Is.EqualTo(TeamRelation.Enemy));
        }

        private GameObject Spawn(string name, int side, Vector3 feet)
        {
            // カプセルは高さ 2 m で原点が中心なので、足元を feet に合わせる。
            var body = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            body.name = name;
            body.transform.position = feet + Vector3.up;
            body.AddComponent<Damageable>();
            body.AddComponent<SideMember>().Side = new Side(side);
            Physics.SyncTransforms();
            return body;
        }

        private void Wall(Vector3 centre)
        {
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = centre;
            wall.transform.localScale = new Vector3(10f, 6f, 0.5f);
            Physics.SyncTransforms();
        }

        private static int IdOf(GameObject go)
        {
            return CombatEvents.OfflineId(go);
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

        private sealed class FakeTargets : INameplateTargets
        {
            public FakeTargets(IParticipants participants, params NameplateTarget[] items)
            {
                Participants = participants;
                Items = new List<NameplateTarget>(items);
            }

            public IParticipants Participants { get; }

            public List<NameplateTarget> Items { get; }

            public List<Vector3> Eyes { get; } = new List<Vector3>();

            public void CopyTo(Vector3 eye, List<NameplateTarget> targets)
            {
                Eyes.Add(eye);
                targets.AddRange(Items);
            }
        }

        private sealed class FakeParticipants : IParticipants
        {
            private readonly Dictionary<int, Participant> _byId = new Dictionary<int, Participant>();

            public FakeParticipants(params Participant[] participants)
            {
                foreach (var participant in participants)
                {
                    _byId.Add(participant.Id, participant);
                }
            }

            public bool TryGet(int id, out Participant participant)
            {
                return _byId.TryGetValue(id, out participant);
            }
        }
    }
}
