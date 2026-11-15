using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 爆発に巻き込まれる距離にあるグレネードを知らせるビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// 毎フレーム <see cref="ThrownGrenade.Live"/> から、今のカメラ (<see cref="Viewer"/>) が爆発の届く半径の内側にいるものを拾い、
    /// その方向を画面の中央を囲む円周上の印 (<see cref="MarkerClass"/>) と距離の文字で出す。減らさずに当たる半径の内側なら
    /// <see cref="LethalClass"/> を足す。見た目 (色・大きさ・アイコン) は Hud.uss の各クラスで決める。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeWarningView : HudElementView
    {
        /// <summary>Hud.uxml のグレネード警告の要素。</summary>
        public const string UxmlName = "grenade-warning";

        /// <summary>グレネード 1 個の印に付けるクラス。</summary>
        public const string MarkerClass = "grenade-warning__marker";

        /// <summary>印の中の文字に付けるクラス。</summary>
        public const string LabelClass = "grenade-warning__label";

        /// <summary>減らさずに当たる半径の内側にいるときに印へ足すクラス。</summary>
        public const string LethalClass = "grenade-warning__marker--lethal";

        /// <summary>印の文字のキー。{0} は距離 (m、切り上げ)。</summary>
        public const string TextKey = "hud.grenadeWarning";

        /// <summary>同時に出す印の数の上限。近い順に出す。</summary>
        public const int MaxMarkers = 8;

        /// <summary>印を置く円の半径 (HUD の基準解像度での px)。</summary>
        public const float MarkerRadius = 160f;

        private readonly List<GrenadeThreat> _threats = new List<GrenadeThreat>();
        private readonly List<Marker> _markers = new List<Marker>();

        public override string ElementName => UxmlName;

        /// <summary>
        /// 距離と向きの基準にするカメラ。null なら <see cref="Camera.main"/> を使う。
        /// </summary>
        public Transform Viewer { get; set; }

        /// <summary>今出している印の数。</summary>
        public int ShownCount => _threats.Count;

        /// <summary>index 番目に出している (近い順) グレネードの警告。</summary>
        public GrenadeThreat ThreatAt(int index)
        {
            return _threats[index];
        }

        private void OnDisable()
        {
            _threats.Clear();
            Refresh();
        }

        private void LateUpdate()
        {
            // カメラが動いた後に向きを見直すため LateUpdate。
            var viewer = ResolveViewer();
            if (viewer == null)
            {
                Show(viewer, System.Array.Empty<ThrownGrenade>());
                return;
            }

            Show(viewer, ThrownGrenade.Live);
        }

        /// <summary>
        /// grenades のうち viewer が爆発の届く半径の内側にいるものを、近い順に <see cref="MaxMarkers"/> 個まで出す。
        /// 爆発しない種類・爆発した後のもの・HUD の量で出さないとき (<see cref="HudElementView.IsAllowed"/> が false) は出さない。
        /// </summary>
        public void Show(Transform viewer, IReadOnlyList<ThrownGrenade> grenades)
        {
            _threats.Clear();
            if (IsAllowed && viewer != null && grenades != null)
            {
                var eye = viewer.position;
                foreach (var grenade in grenades)
                {
                    var damage = grenade != null && !grenade.HasExploded ? grenade.Explosion : null;
                    if (damage == null)
                    {
                        continue;
                    }

                    var position = grenade.transform.position;
                    var distance = Vector3.Distance(eye, position);
                    if (distance <= damage.Radius)
                    {
                        _threats.Add(new GrenadeThreat(position, distance, distance <= damage.FullDamageRadius));
                    }
                }

                _threats.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                if (_threats.Count > MaxMarkers)
                {
                    _threats.RemoveRange(MaxMarkers, _threats.Count - MaxMarkers);
                }
            }

            Refresh(viewer);
        }

        protected override void OnBound(VisualElement element)
        {
            // 前の木の印は木と一緒に捨てられているので、新しい木に作り直す。
            _markers.Clear();
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            foreach (var marker in _markers)
            {
                marker.Root.RemoveFromHierarchy();
            }

            _markers.Clear();
        }

        protected override void OnAllowedChanged(bool allowed)
        {
            if (!allowed)
            {
                _threats.Clear();
                Refresh();
            }
        }

        protected override void OnLanguageChanged()
        {
            foreach (var marker in _markers)
            {
                marker.ShownMeters = -1;
            }

            Refresh();
        }

        private void Refresh()
        {
            Refresh(ResolveViewer());
        }

        private void Refresh(Transform viewer)
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.style.display = _threats.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var center = element.contentRect.center;
            for (var i = 0; i < _threats.Count; i++)
            {
                if (i == _markers.Count)
                {
                    _markers.Add(CreateMarker(element));
                }

                var marker = _markers[i];
                var threat = _threats[i];
                if (viewer == null || !DamageDirection.TryScreenAngle(
                        viewer.position.ToNumerics(), viewer.eulerAngles.y, threat.Position.ToNumerics(), out var degrees))
                {
                    // 真上か真下にあって向きが決まらないときは、中央に出す。
                    degrees = float.NaN;
                }

                var radians = degrees * Mathf.Deg2Rad;
                var offset = float.IsNaN(radians) ? Vector2.zero : new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)) * MarkerRadius;
                marker.Root.style.display = DisplayStyle.Flex;
                marker.Root.style.left = center.x + offset.x;
                marker.Root.style.top = center.y + offset.y;
                marker.Root.EnableInClassList(LethalClass, threat.IsLethal);

                var meters = Mathf.CeilToInt(threat.Distance);
                if (meters != marker.ShownMeters)
                {
                    marker.ShownMeters = meters;
                    marker.Label.text = Localizer.Resolve(new LocalizedText(TextKey, meters));
                }
            }

            for (var i = _threats.Count; i < _markers.Count; i++)
            {
                _markers[i].Root.style.display = DisplayStyle.None;
            }
        }

        private Transform ResolveViewer()
        {
            if (Viewer != null)
            {
                return Viewer;
            }

            var main = Camera.main;
            return main != null ? main.transform : null;
        }

        private static Marker CreateMarker(VisualElement parent)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.AddToClassList(MarkerClass);
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(LabelClass);
            root.Add(label);
            parent.Add(root);
            return new Marker { Root = root, Label = label, ShownMeters = -1 };
        }

        private sealed class Marker
        {
            public VisualElement Root;
            public Label Label;
            public int ShownMeters;
        }
    }

    /// <summary><see cref="GrenadeWarningView"/> が出しているグレネード 1 個。</summary>
    public readonly struct GrenadeThreat
    {
        public GrenadeThreat(Vector3 position, float distance, bool isLethal)
        {
            Position = position;
            Distance = distance;
            IsLethal = isLethal;
        }

        public Vector3 Position { get; }

        /// <summary>カメラからの距離 (m)。</summary>
        public float Distance { get; }

        /// <summary>減らさずに当たる半径の内側にいるか。</summary>
        public bool IsLethal { get; }
    }
}
