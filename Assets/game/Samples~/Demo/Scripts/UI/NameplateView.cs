using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 出すかどうかをここで決めないのは、距離と壁越しの決まりを NameplateRule と nameplates.json の 1 か所に置くため。
    [DisallowMultipleComponent]
    public sealed class NameplateView : HudElementView
    {
        public const string UxmlName = "nameplates";

        public const string PlateClass = "nameplate";

        public const string FriendlyClass = "nameplate--friendly";

        public const string EnemyClass = "nameplate--enemy";

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<NameplateTarget> _targets = new List<NameplateTarget>();
        private readonly List<int> _stale = new List<int>();
        private NameplateSpec _spec;
        private INameplateTargets _source;

        public override string ElementName => UxmlName;

        public Camera Viewer { get; set; }

        public NameplateSpec Spec
        {
            get => _spec ?? KitTuning.Nameplates;
            set => _spec = value ?? throw new ArgumentNullException(nameof(value));
        }

        public INameplateTargets Targets
        {
            get => _source ?? (_source = new OfflineNameplateTargets());
            set => _source = value ?? throw new ArgumentNullException(nameof(value));
        }

        public int ShownCount => _entries.Count;

        public bool TryGetPlate(int id, out Plate plate)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                plate = entry.Plate;
                return true;
            }

            plate = default;
            return false;
        }

        // 画面の外や背中側の相手は、端に寄せると誰の名前か分からなくなるので出さない。
        public static bool TryPlace(Camera camera, Vector3 world, out Vector2 screen)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            var viewport = camera.WorldToViewportPoint(world);
            if (!IsFinite(viewport.x) || !IsFinite(viewport.y) || !IsFinite(viewport.z)
                || viewport.z <= 0f
                || viewport.x < 0f || viewport.x > 1f
                || viewport.y < 0f || viewport.y > 1f)
            {
                screen = default;
                return false;
            }

            // ビューポートは下が 0 で、UI の top は上が 0 なので縦を返す。
            screen = new Vector2(viewport.x, 1f - viewport.y);
            return true;
        }

        public void Refresh()
        {
            var camera = Viewer != null ? Viewer : PingMarkersView.LocalPlayerCamera();
            _targets.Clear();
            if (camera != null)
            {
                Targets.CopyTo(camera.transform.position, _targets);
            }

            var spec = Spec;
            var participants = Targets.Participants;
            var palette = TeamColors.Current;
            foreach (var entry in _entries.Values)
            {
                entry.Seen = false;
            }

            foreach (var target in _targets)
            {
                var distance = Vector3.Distance(camera.transform.position, target.HeadPosition);
                if (!NameplateRule.ShouldShow(spec, target.Relation, distance, target.IsInSight, target.IsAlive)
                    || !TryPlace(camera, target.HeadPosition, out var screen)
                    || !TryName(participants, target.Id, out var name))
                {
                    continue;
                }

                if (!_entries.TryGetValue(target.Id, out var entry))
                {
                    entry = new Entry();
                    _entries.Add(target.Id, entry);
                    Debug.Log(NameplateLog.Shown(target.Id, target.Relation, distance), this);
                }

                entry.Seen = true;
                entry.Plate = new Plate(
                    target.Id,
                    palette.NameplateText(target.Relation, name),
                    target.Relation,
                    TeamColors.ToColor(palette.ColorOf(target.Relation)),
                    screen.x,
                    screen.y);
                ApplyEntry(entry);
            }

            DropUnseen();
            _targets.Clear();

            if (Element != null)
            {
                Element.style.display = _entries.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        protected override void OnBound(VisualElement element)
        {
            Debug.Log(NameplateLog.View(GetType().Name, Spec), this);
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            foreach (var entry in _entries.Values)
            {
                RemoveLabel(entry);
            }

            _entries.Clear();
            element.style.display = DisplayStyle.None;
        }

        private void Start()
        {
            // Hud.AddViews の後から手で付けたときは Hud が木を作り直すまで要素を受けないので、今の木から受ける。
            if (Element == null)
            {
                MinimapView.BindToDocument(this);
            }
        }

        private void LateUpdate()
        {
            // 要素の無い間に毎フレーム相手を集めて視線を調べないよう、結び付いているときだけ置き直す。
            if (Element != null && IsAllowed)
            {
                Refresh();
            }
        }

        private static bool TryName(IParticipants participants, int id, out string name)
        {
            if (participants != null && participants.TryGet(id, out var participant) && !string.IsNullOrWhiteSpace(participant.Name))
            {
                name = participant.Name;
                return true;
            }

            name = null;
            return false;
        }

        private void DropUnseen()
        {
            _stale.Clear();
            foreach (var pair in _entries)
            {
                if (!pair.Value.Seen)
                {
                    _stale.Add(pair.Key);
                }
            }

            foreach (var id in _stale)
            {
                RemoveLabel(_entries[id]);
                _entries.Remove(id);
                Debug.Log(NameplateLog.Hidden(id), this);
            }

            _stale.Clear();
        }

        private void ApplyEntry(Entry entry)
        {
            var parent = Element;
            if (parent == null)
            {
                return;
            }

            var plate = entry.Plate;
            if (entry.Label == null)
            {
                entry.Label = new Label { pickingMode = PickingMode.Ignore };
                entry.Label.AddToClassList(PlateClass);
                parent.Add(entry.Label);
            }

            var label = entry.Label;
            label.EnableInClassList(FriendlyClass, plate.Relation == TeamRelation.Friendly);
            label.EnableInClassList(EnemyClass, plate.Relation == TeamRelation.Enemy);
            if (!string.Equals(label.text, plate.Text, StringComparison.Ordinal))
            {
                label.text = plate.Text;
            }

            // 色は設定の色覚のパレットで変わるので USS に決め打ちせずここで入れる。
            label.style.color = plate.Color;
            label.style.left = Length.Percent(plate.X * 100f);
            label.style.top = Length.Percent(plate.Y * 100f);
        }

        private static void RemoveLabel(Entry entry)
        {
            entry.Label?.RemoveFromHierarchy();
            entry.Label = null;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public readonly struct Plate
        {
            public Plate(int id, string text, TeamRelation relation, Color color, float x, float y)
            {
                Id = id;
                Text = text ?? string.Empty;
                Relation = relation;
                Color = color;
                X = x;
                Y = y;
            }

            public int Id { get; }

            public string Text { get; }

            public TeamRelation Relation { get; }

            public Color Color { get; }

            public float X { get; }

            public float Y { get; }
        }

        private sealed class Entry
        {
            public Plate Plate;
            public bool Seen;
            public Label Label;
        }
    }
}
