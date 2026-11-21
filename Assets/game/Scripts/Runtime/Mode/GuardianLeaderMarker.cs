using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // リーダーの子にしないのは、bot の拡縮や倒れたときの体の傾きを目印が受けないようにするため。毎フレーム位置だけを追う。
    // オフラインは SideMember を、オンラインは SideMember を持たない相手の体 (Transform) を追うので、追う先と頭の高さの引き方を分けて持つ。
    [DisallowMultipleComponent]
    public sealed class GuardianLeaderMarker : MonoBehaviour
    {
        public const string ObjectNamePrefix = "LeaderMarker-";

        public const float FallbackHeadHeight = 1.8f;

        public const float DefaultHeightAboveHead = 0.6f;
        public const float DefaultSize = 0.35f;
        public const float SpinDegreesPerSecond = 90f;

        // 立方体を頂点が上下に来るように傾けて菱形に見せる。
        public static readonly Quaternion PlaceholderTilt = Quaternion.Euler(45f, 0f, 45f);

        private readonly List<Material> _materials = new List<Material>();
        private SideMember _target;
        private Transform _anchor;
        private Func<Bounds?> _boundsOf;
        private Side _side;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private float _heightAboveHead = DefaultHeightAboveHead;
        private float _spin;
        private Quaternion _tilt = PlaceholderTilt;
        private bool _isPlaceholder = true;
        private Team? _viewerTeam = TeamColors.OfflineViewer;
        private bool _followsSettings;

        public SideMember Target => _target;

        // 追っている先。追う先が消えたら目印も消える
        public Transform Anchor => _anchor;

        public Side Side => _target != null ? _target.Side : _side;

        public Renderer Renderer => _renderers.Length > 0 ? _renderers[0] : null;

        public IReadOnlyList<Renderer> Renderers => _renderers;

        public float HeightAboveHead => _heightAboveHead;

        public bool IsPlaceholder => _isPlaceholder;

        public bool IsShown
        {
            get
            {
                foreach (var markerRenderer in _renderers)
                {
                    if (markerRenderer != null && markerRenderer.enabled)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // 弾と移動を遮らないよう、当たり判定は持たせない。
        public static GuardianLeaderMarker Create(SideMember target, float heightAboveHead = DefaultHeightAboveHead, float size = DefaultSize)
        {
            return Create(target, null, heightAboveHead, size);
        }

        public static GuardianLeaderMarker Create(
            SideMember target,
            GameObject prefab,
            float heightAboveHead = DefaultHeightAboveHead,
            float size = DefaultSize)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return CreateCore(target.transform, target, target.Side, null, prefab, heightAboveHead, size);
        }

        // bounds は頭の高さを決める囲み。null を返す間は追う先の位置から FallbackHeadHeight 上を頭とする
        public static GuardianLeaderMarker Create(
            Transform target,
            Side side,
            Func<Bounds?> bounds,
            GameObject prefab = null,
            float heightAboveHead = DefaultHeightAboveHead,
            float size = DefaultSize)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return CreateCore(target, null, side, bounds, prefab, heightAboveHead, size);
        }

        private static GuardianLeaderMarker CreateCore(
            Transform anchor,
            SideMember member,
            Side side,
            Func<Bounds?> bounds,
            GameObject prefab,
            float heightAboveHead,
            float size)
        {
            if (!(heightAboveHead >= 0f) || float.IsInfinity(heightAboveHead))
            {
                throw new ArgumentOutOfRangeException(nameof(heightAboveHead), heightAboveHead, "heightAboveHead must not be negative and must be finite");
            }

            if (!(size > 0f) || float.IsInfinity(size))
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "size must be positive and finite");
            }

            var markerName = ObjectNamePrefix + anchor.name;
            GameObject go;
            if (prefab != null)
            {
                go = VisualPrefab.Instantiate(prefab, null, markerName, stripColliders: true);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = markerName;
                DestroySafely(go.GetComponent<Collider>());
                go.transform.localScale = Vector3.one * size;
            }

            var marker = go.AddComponent<GuardianLeaderMarker>();
            marker._target = member;
            marker._anchor = anchor;
            marker._side = side;
            marker._boundsOf = bounds;
            marker._heightAboveHead = heightAboveHead;
            marker._isPlaceholder = prefab == null;
            marker._tilt = prefab != null ? prefab.transform.localRotation : PlaceholderTilt;
            marker._renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var markerRenderer in marker._renderers)
            {
                markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                markerRenderer.receiveShadows = false;
                marker.CopyMaterials(markerRenderer);
            }

            marker.ApplyColor();
            AppliedControlSettings.Changed += marker.HandleSettingsChanged;
            marker._followsSettings = true;

            marker.Follow();
            return marker;
        }

        public Team? ViewerTeam
        {
            get => _viewerTeam;
            set
            {
                _viewerTeam = value;
                ApplyColor();
            }
        }

        public static Color ColorOf(Side side, Team? viewer = TeamColors.OfflineViewer)
        {
            return ControlPointZone.ColorOf(side.TryGetTeam(out var team) ? team : (Team?)null, viewer);
        }

        // 共有のマテリアルを塗り替えると他のプリミティブやプレハブの元の材質まで色が変わるので、目印ごとに複製する。
        private void CopyMaterials(Renderer markerRenderer)
        {
            var shared = markerRenderer.sharedMaterials;
            for (var i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null)
                {
                    continue;
                }

                var copy = new Material(shared[i]) { name = name };
                _materials.Add(copy);
                shared[i] = copy;
            }

            markerRenderer.sharedMaterials = shared;
        }

        private void ApplyColor()
        {
            if (_anchor == null)
            {
                return;
            }

            var color = ColorOf(Side, _viewerTeam);
            foreach (var material in _materials)
            {
                if (material != null)
                {
                    material.color = color;
                }
            }
        }

        private void DestroyMaterials()
        {
            foreach (var material in _materials)
            {
                DestroySafely(material);
            }

            _materials.Clear();
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            if (this != null)
            {
                ApplyColor();
            }
        }

        private void StopFollowingSettings()
        {
            if (_followsSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsSettings = false;
            }
        }

        public void SetShown(bool shown)
        {
            foreach (var markerRenderer in _renderers)
            {
                if (markerRenderer != null)
                {
                    markerRenderer.enabled = shown;
                }
            }
        }

        public void Follow()
        {
            if (_anchor == null)
            {
                return;
            }

            float top;
            Vector3 center;
            if (TryGetHeadBounds(out var bounds))
            {
                top = bounds.max.y;
                center = bounds.center;
            }
            else
            {
                center = _anchor.position;
                top = center.y + FallbackHeadHeight;
            }

            // 仮の菱形は中心が原点なので半分持ち上げる。プレハブは原点を下端とする。
            var lift = _isPlaceholder ? transform.lossyScale.y * 0.5f : 0f;
            transform.SetPositionAndRotation(
                new Vector3(center.x, top + _heightAboveHead + lift, center.z),
                Quaternion.Euler(0f, _spin, 0f) * _tilt);
        }

        private bool TryGetHeadBounds(out Bounds bounds)
        {
            if (_target != null)
            {
                var damageable = _target.Damageable;
                if (damageable != null && damageable.TryGetBounds(out bounds))
                {
                    return true;
                }

                bounds = default;
                return false;
            }

            var found = _boundsOf?.Invoke();
            bounds = found ?? default;
            return found.HasValue;
        }

        // Edit Mode では OnDestroy が呼ばれないので、複製したマテリアルもここで消す。
        public void Remove()
        {
            StopFollowingSettings();
            DestroyMaterials();
            DestroySafely(gameObject);
        }

        private void LateUpdate()
        {
            if (_anchor == null)
            {
                Destroy(gameObject);
                return;
            }

            _spin = Mathf.Repeat(_spin + (SpinDegreesPerSecond * Time.deltaTime), 360f);
            Follow();
        }

        private void OnDestroy()
        {
            StopFollowingSettings();
            DestroyMaterials();
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
