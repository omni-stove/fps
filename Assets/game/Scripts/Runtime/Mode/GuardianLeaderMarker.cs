using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ガーディアンのリーダーの頭上に浮かぶ目印。<see cref="Target"/> の当たり判定の上端から <see cref="HeightAboveHead"/> 上に、
    /// チームの色の菱形 (<see cref="GuardianRunner.LeaderMarkerPrefab"/> があればその形をチームの色に塗ったもの) を回しながら出す。
    /// <see cref="GuardianRunner"/> が作り、リーダーが倒れたら隠す。
    /// </summary>
    /// <remarks>
    /// リーダーの子にしないのは、bot の拡縮や倒れたときの体の傾きを目印が受けないようにするため。毎フレーム位置だけを追う。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GuardianLeaderMarker : MonoBehaviour
    {
        public const string ObjectNamePrefix = "LeaderMarker-";

        /// <summary>当たり判定が無いときに、足元から頭までとみなす高さ (m)。</summary>
        public const float FallbackHeadHeight = 1.8f;

        public const float DefaultHeightAboveHead = 0.6f;
        public const float DefaultSize = 0.35f;
        public const float SpinDegreesPerSecond = 90f;

        /// <summary>仮の菱形の傾き。立方体を頂点が上下に来るように傾ける。</summary>
        public static readonly Quaternion PlaceholderTilt = Quaternion.Euler(45f, 0f, 45f);

        private readonly List<Material> _materials = new List<Material>();
        private SideMember _target;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private float _heightAboveHead = DefaultHeightAboveHead;
        private float _spin;
        private Quaternion _tilt = PlaceholderTilt;
        private bool _isPlaceholder = true;
        private Team? _viewerTeam = TeamColors.OfflineViewer;
        private bool _followsSettings;

        /// <summary>目印を出すリーダー。</summary>
        public SideMember Target => _target;

        /// <summary>目印の色を塗った Renderer (プレハブから作ったときは最初の 1 つ)。無ければ null。</summary>
        public Renderer Renderer => _renderers.Length > 0 ? _renderers[0] : null;

        /// <summary>目印の色を塗った Renderer すべて。</summary>
        public IReadOnlyList<Renderer> Renderers => _renderers;

        public float HeightAboveHead => _heightAboveHead;

        /// <summary>プレハブが無く仮の菱形で作ったなら true。</summary>
        public bool IsPlaceholder => _isPlaceholder;

        /// <summary>目印が見えているなら true。</summary>
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

        /// <summary>
        /// target の頭上に出す仮の菱形の目印を作る。シーンの直下に置き、当たり判定は持たせない (弾と移動を遮らないように)。
        /// </summary>
        public static GuardianLeaderMarker Create(SideMember target, float heightAboveHead = DefaultHeightAboveHead, float size = DefaultSize)
        {
            return Create(target, null, heightAboveHead, size);
        }

        /// <summary>
        /// target の頭上に出す目印を作る。prefab があれば当たり判定を外して置き (大きさと向きはプレハブのまま、原点を目印の下端として
        /// 頭上 heightAboveHead に置き、縦の軸で回す)、size は使わない。null なら仮の菱形を作る。
        /// どちらも Renderer の材質を複製してチームの色に塗る。
        /// </summary>
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

            if (!(heightAboveHead >= 0f) || float.IsInfinity(heightAboveHead))
            {
                throw new ArgumentOutOfRangeException(nameof(heightAboveHead), heightAboveHead, "heightAboveHead must not be negative and must be finite");
            }

            if (!(size > 0f) || float.IsInfinity(size))
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "size must be positive and finite");
            }

            var markerName = ObjectNamePrefix + target.name;
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
            marker._target = target;
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

        /// <summary>
        /// 画面のプレイヤーのチーム。リーダーがこのチームなら味方の色、違えば敵の色で塗る。既定はオフラインのプレイヤーのチーム
        /// (<see cref="TeamColors.OfflineViewer"/>)。
        /// </summary>
        public Team? ViewerTeam
        {
            get => _viewerTeam;
            set
            {
                _viewerTeam = value;
                ApplyColor();
            }
        }

        /// <summary>
        /// viewer から見た陣営の色 (今の色の組)。Alpha と Bravo は拠点と同じ色 (<see cref="ControlPointZone.ColorOf"/>)、それ以外は中立の色。
        /// </summary>
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
            if (_target == null)
            {
                return;
            }

            var color = ColorOf(_target.Side, _viewerTeam);
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

        /// <summary>目印を見せるか隠す。</summary>
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

        /// <summary>リーダーの頭上へ動かす。Play 中は LateUpdate で毎フレーム呼ぶ。</summary>
        public void Follow()
        {
            if (_target == null)
            {
                return;
            }

            var damageable = _target.Damageable;
            float top;
            Vector3 center;
            if (damageable != null && damageable.TryGetBounds(out var bounds))
            {
                top = bounds.max.y;
                center = bounds.center;
            }
            else
            {
                center = _target.transform.position;
                top = center.y + FallbackHeadHeight;
            }

            // 仮の菱形は中心が原点なので半分持ち上げる。プレハブは原点を下端とする。
            var lift = _isPlaceholder ? transform.lossyScale.y * 0.5f : 0f;
            transform.SetPositionAndRotation(
                new Vector3(center.x, top + _heightAboveHead + lift, center.z),
                Quaternion.Euler(0f, _spin, 0f) * _tilt);
        }

        /// <summary>目印と複製したマテリアルを消す。Edit Mode では OnDestroy が呼ばれないので、マテリアルもここで消す。</summary>
        public void Remove()
        {
            StopFollowingSettings();
            DestroyMaterials();
            DestroySafely(gameObject);
        }

        private void LateUpdate()
        {
            if (_target == null)
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
