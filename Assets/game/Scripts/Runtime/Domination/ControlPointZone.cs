using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 範囲にいる参加者を自分では調べず、Runner に毎フレーム Scan させるのは、誰が参加者かを知っているのが Runner だから。
    [DisallowMultipleComponent]
    public sealed class ControlPointZone : MonoBehaviour
    {
        public const float DefaultRadius = 5f;
        public const float DefaultHeight = 6f;

        public const string MarkerName = "ControlPointMarker";

        // 床に埋もれず、上に立っても段差にならない薄さにする。
        public const float MarkerThickness = 0.04f;

        public const float DimmedBrightness = 0.3f;

        public const float HighlightEmission = 1.5f;

        private const int GizmoSegments = 32;
        private const string EmissionKeyword = "_EMISSION";
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Min(0), Tooltip("拠点の番号。シーンの拠点で 0 から抜けなく 1 つずつ振る。HUD とログでは 0 = A, 1 = B, ...")]
        private int _index;

        [SerializeField, Min(0.1f), Tooltip("範囲の水平の半径 (m)")]
        private float _radius = DefaultRadius;

        [SerializeField, Min(0.1f), Tooltip("範囲の高さ (m)。この GameObject の位置から上下に半分ずつ")]
        private float _height = DefaultHeight;

        [SerializeField, Tooltip("Play 中に範囲を示す円盤を足元に出すか")]
        private bool _showMarker = true;

        [SerializeField, Tooltip("範囲を示す印の見た目。原点を中心にした直径 1m の形にする (水平に範囲の直径まで拡大し、高さはそのまま)。当たり判定は外し、材質はチームの色で塗る。空なら仮の薄い円盤を作る")]
        private GameObject _markerPrefab;

        private readonly List<Damageable> _occupants = new List<Damageable>();
        private readonly List<Material> _markerMaterials = new List<Material>();
        private GameObject _marker;
        private bool _markerFromPrefab;
        private Team? _owner;
        private bool? _highlight;
        private Team? _viewerTeam = TeamColors.OfflineViewer;
        private bool _followsSettings;

        public Team? ViewerTeam
        {
            get => _viewerTeam;
            set
            {
                _viewerTeam = value;
                ApplyMarkerColor();
            }
        }

        public int Index
        {
            get => _index;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Index must not be negative");
                }

                _index = value;
            }
        }

        public float Radius
        {
            get => _radius;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Radius must be positive and finite");
                }

                _radius = value;
            }
        }

        public float Height
        {
            get => _height;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Height must be positive and finite");
                }

                _height = value;
            }
        }

        public GameObject MarkerPrefab
        {
            get => _markerPrefab;
            set => _markerPrefab = value;
        }

        public bool ShowsMarker
        {
            get => _showMarker;
            set => _showMarker = value;
        }

        public ControlPointArea Area => new ControlPointArea(_index, transform.position.ToNumerics(), _radius, _height);

        public IReadOnlyList<Damageable> Occupants => _occupants;

        public Team? Owner => _owner;

        public bool? Highlight => _highlight;

        public GameObject Marker => _marker;

        public bool Contains(Vector3 position)
        {
            return Area.Contains(position.ToNumerics());
        }

        public void Scan(IReadOnlyList<Damageable> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            _occupants.Clear();
            var area = Area;
            foreach (var candidate in candidates)
            {
                if (candidate != null && !candidate.IsDead && area.Contains(candidate.transform.position.ToNumerics()))
                {
                    _occupants.Add(candidate);
                }
            }
        }

        public void ShowOwner(Team? owner)
        {
            _owner = owner;
            ApplyMarkerColor();
        }

        public void SetHighlight(bool? highlighted)
        {
            _highlight = highlighted;
            ApplyMarkerColor();
        }

        public static Color MarkerColorOf(Team? owner, bool? highlight, Team? viewer = TeamColors.OfflineViewer)
        {
            var color = ColorOf(owner, viewer);
            if (highlight == false)
            {
                color = new Color(color.r * DimmedBrightness, color.g * DimmedBrightness, color.b * DimmedBrightness, color.a);
            }

            return color;
        }

        public static Color ColorOf(Team? owner, Team? viewer = TeamColors.OfflineViewer)
        {
            return TeamColors.Of(owner, viewer);
        }

        public void RefreshMarker()
        {
            if (!_showMarker)
            {
                return;
            }

            if (_marker == null)
            {
                CreateMarker();
            }

            FitMarker(_marker.transform, transform.lossyScale, _radius, _markerFromPrefab);
        }

        // Cylinder は高さ 2・直径 1 なので、仮の円盤は厚みを MarkerThickness に合わせて縮め、直径 1m のプレハブは高さをそのままにする。
        internal static void FitMarker(Transform marker, Vector3 parentScale, float radius, bool fromPrefab)
        {
            var height = fromPrefab ? 1f : MarkerThickness * 0.5f;
            marker.localScale = new Vector3(
                SafeDivide(radius * 2f, parentScale.x),
                SafeDivide(height, parentScale.y),
                SafeDivide(radius * 2f, parentScale.z));
            marker.localPosition = Vector3.zero;
            marker.localRotation = Quaternion.identity;
        }

        // 塗るための材質は Renderer ごとに複製して materials に足すので、呼んだ側が印と一緒に消す。
        internal static GameObject CreateMarkerObject(GameObject prefab, Transform parent, string markerName, List<Material> materials)
        {
            GameObject marker;
            if (prefab != null)
            {
                marker = VisualPrefab.Instantiate(prefab, parent, markerName, stripColliders: true);
            }
            else
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = markerName;
                // Destroy は次のフレームまで残り、その間に弾や移動の判定に当たるので、すぐ消す。
                DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.SetParent(parent, false);
            }

            foreach (var renderer in marker.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // 共有のマテリアルを塗り替えると同じ材質の他の物まで色が変わるので、この印用に複製する。
                var shared = renderer.sharedMaterials;
                for (var i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null)
                    {
                        continue;
                    }

                    shared[i] = new Material(shared[i]) { name = markerName };
                    materials.Add(shared[i]);
                }

                renderer.sharedMaterials = shared;
            }

            return marker;
        }

        internal static void DestroyMaterials(List<Material> materials)
        {
            foreach (var material in materials)
            {
                DestroySafely(material);
            }

            materials.Clear();
        }

        private void Start()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            RefreshMarker();
        }

        private void OnDestroy()
        {
            if (_followsSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsSettings = false;
            }

            DestroyMaterials(_markerMaterials);
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            if (this != null)
            {
                ApplyMarkerColor();
            }
        }

        private void CreateMarker()
        {
            if (!_followsSettings)
            {
                AppliedControlSettings.Changed += HandleSettingsChanged;
                _followsSettings = true;
            }

            _markerFromPrefab = _markerPrefab != null;
            _marker = CreateMarkerObject(_markerPrefab, transform, MarkerName, _markerMaterials);
            ApplyMarkerColor();
        }

        private void ApplyMarkerColor()
        {
            foreach (var material in _markerMaterials)
            {
                if (material != null)
                {
                    ApplyMarkerColor(material);
                }
            }
        }

        private void ApplyMarkerColor(Material material)
        {
            material.color = MarkerColorOf(_owner, _highlight, _viewerTeam);

            // 発光を持たないシェーダーでは色だけ変える。
            if (!material.HasProperty(EmissionColorId))
            {
                return;
            }

            if (_highlight == true)
            {
                material.EnableKeyword(EmissionKeyword);
                material.SetColor(EmissionColorId, ColorOf(_owner, _viewerTeam) * HighlightEmission);
            }
            else
            {
                material.DisableKeyword(EmissionKeyword);
                material.SetColor(EmissionColorId, Color.black);
            }
        }

        private void OnDrawGizmos()
        {
            if (!(_radius > 0f) || !(_height > 0f))
            {
                return;
            }

            // 編集中に保存した設定を読みに行かない (読むと全体の音量まで当ててしまう) よう、標準の組で描く
            Gizmos.color = TeamColors.Of(TeamPalette.Standard, _owner, _viewerTeam);
            var center = transform.position;
            var half = Vector3.up * (_height * 0.5f);
            var previous = center + new Vector3(_radius, 0f, 0f);
            for (var i = 1; i <= GizmoSegments; i++)
            {
                var angle = i * Mathf.PI * 2f / GizmoSegments;
                var next = center + new Vector3(Mathf.Cos(angle) * _radius, 0f, Mathf.Sin(angle) * _radius);
                Gizmos.DrawLine(previous - half, next - half);
                Gizmos.DrawLine(previous + half, next + half);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 1e-6f ? value / divisor : value;
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
