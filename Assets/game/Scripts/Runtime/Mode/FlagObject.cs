using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 形はプレハブでもコードで作ったものでも、弾と移動を遮らないよう当たり判定を外す。
    [DisallowMultipleComponent]
    public sealed class FlagObject : MonoBehaviour
    {
        public const string ModelName = "FlagModel";

        public const float PoleHeight = 2f;

        public const float CarryHeight = 0.2f;

        public const float CarryBack = 0.35f;

        public const float DroppedBrightness = 0.6f;

        private const float PoleThickness = 0.06f;
        private static readonly Vector3 ClothSize = new Vector3(0.7f, 0.45f, 0.04f);

        [SerializeField, Min(0), Tooltip("この旗の陣営の ID。0 = Alpha、1 = Bravo")]
        private int _sideId;

        [SerializeField, Tooltip("旗の見た目。足元を原点にし、竿を上 (+Y) に立てた形にする。当たり判定は外し、材質は陣営の色で塗る。空なら仮の竿と布を作る")]
        private GameObject _modelPrefab;

        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private GameObject _model;
        private Transform _carrier;
        private FlagState? _shown;
        private bool _visible;
        private bool _followsSettings;
        private Team? _viewerTeam = TeamColors.OfflineViewer;

        public Side Side
        {
            get => new Side(Mathf.Max(0, _sideId));
            set
            {
                _sideId = value.Id;
                Paint();
            }
        }

        public Team? ViewerTeam
        {
            get => _viewerTeam;
            set
            {
                _viewerTeam = value;
                Paint();
            }
        }

        public GameObject ModelPrefab
        {
            get => _modelPrefab;
            set => _modelPrefab = value;
        }

        public GameObject Model => _model;

        public Transform Carrier => _carrier;

        public FlagState? ShownState => _shown;

        public bool IsVisible => _visible;

        public void ShowAtBase(Vector3 ground)
        {
            PlaceOnGround(ground, FlagState.AtBase);
        }

        public void ShowDropped(Vector3 ground)
        {
            PlaceOnGround(ground, FlagState.Dropped);
        }

        public void ShowCarried(Transform carrier)
        {
            if (carrier == null)
            {
                Hide();
                return;
            }

            _carrier = carrier;
            _shown = FlagState.Carried;
            EnsureModel();
            Follow();
            SetVisible(true);
            Paint();
        }

        public void Hide()
        {
            _carrier = null;
            _shown = null;
            SetVisible(false);
        }

        private void PlaceOnGround(Vector3 ground, FlagState state)
        {
            _carrier = null;
            _shown = state;
            EnsureModel();
            transform.SetPositionAndRotation(ground, Quaternion.identity);
            SetVisible(true);
            Paint();
        }

        private void LateUpdate()
        {
            if (_carrier != null)
            {
                Follow();
            }
        }

        private void OnDestroy()
        {
            if (_followsSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsSettings = false;
            }

            ControlPointZone.DestroyMaterials(_materials);
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            if (this != null)
            {
                Paint();
            }
        }

        private void Follow()
        {
            var forward = _carrier.forward;
            forward.y = 0f;
            var back = forward.sqrMagnitude > 1e-6f ? -forward.normalized : Vector3.zero;
            transform.SetPositionAndRotation(
                _carrier.position + (Vector3.up * CarryHeight) + (back * CarryBack),
                Quaternion.LookRotation(back.sqrMagnitude > 0f ? -back : Vector3.forward, Vector3.up));
        }

        private void EnsureModel()
        {
            if (_model != null)
            {
                return;
            }

            if (!_followsSettings)
            {
                AppliedControlSettings.Changed += HandleSettingsChanged;
                _followsSettings = true;
            }

            _model = _modelPrefab != null
                ? VisualPrefab.Instantiate(_modelPrefab, transform, ModelName, stripColliders: true)
                : BuildPlaceholder(transform);

            _model.GetComponentsInChildren(true, _renderers);
            foreach (var renderer in _renderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // 共有のマテリアルを塗り替えると同じ材質の他の物まで色が変わるので、この旗用に複製する。
                var shared = renderer.sharedMaterials;
                for (var i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null)
                    {
                        continue;
                    }

                    shared[i] = new Material(shared[i]) { name = ModelName };
                    _materials.Add(shared[i]);
                }

                renderer.sharedMaterials = shared;
            }
        }

        private static GameObject BuildPlaceholder(Transform parent)
        {
            var root = new GameObject(ModelName);
            root.transform.SetParent(parent, false);

            // Cylinder は高さ 2・直径 1 なので、y の拡縮は高さの半分。
            var pole = Primitive(PrimitiveType.Cylinder, "Pole", root.transform);
            pole.transform.localScale = new Vector3(PoleThickness, PoleHeight * 0.5f, PoleThickness);
            pole.transform.localPosition = Vector3.up * (PoleHeight * 0.5f);

            var cloth = Primitive(PrimitiveType.Cube, "Cloth", root.transform);
            cloth.transform.localScale = ClothSize;
            cloth.transform.localPosition = new Vector3((ClothSize.x + PoleThickness) * 0.5f, PoleHeight - (ClothSize.y * 0.5f), 0f);
            return root;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            // Destroy は次のフレームまで残り、その間に弾や移動の判定に当たるので、すぐ消す。
            DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            return part;
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private void Paint()
        {
            var color = FlagBase.ColorOf(Side, _viewerTeam);
            if (_shown == FlagState.Dropped)
            {
                color = new Color(color.r * DroppedBrightness, color.g * DroppedBrightness, color.b * DroppedBrightness, color.a);
            }

            foreach (var material in _materials)
            {
                if (material != null)
                {
                    material.color = color;
                }
            }
        }
    }
}
