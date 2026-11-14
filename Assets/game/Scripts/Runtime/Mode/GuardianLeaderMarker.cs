using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ガーディアンのリーダーの頭上に浮かぶ目印。<see cref="Target"/> の当たり判定の上端から <see cref="HeightAboveHead"/> 上に、
    /// チームの色の菱形を回しながら出す。<see cref="GuardianRunner"/> が作り、リーダーが倒れたら隠す。
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

        private SideMember _target;
        private Renderer _renderer;
        private Material _material;
        private float _heightAboveHead = DefaultHeightAboveHead;
        private float _spin;
        private Team? _viewerTeam = TeamColors.OfflineViewer;
        private bool _followsSettings;

        /// <summary>目印を出すリーダー。</summary>
        public SideMember Target => _target;

        /// <summary>目印の色を塗った Renderer。</summary>
        public Renderer Renderer => _renderer;

        public float HeightAboveHead => _heightAboveHead;

        /// <summary>目印が見えているなら true。</summary>
        public bool IsShown => _renderer != null && _renderer.enabled;

        /// <summary>
        /// target の頭上に出す目印を作る。シーンの直下に置き、当たり判定は持たせない (弾と移動を遮らないように)。
        /// </summary>
        public static GuardianLeaderMarker Create(SideMember target, float heightAboveHead = DefaultHeightAboveHead, float size = DefaultSize)
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

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = ObjectNamePrefix + target.name;
            DestroySafely(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * size;

            var marker = go.AddComponent<GuardianLeaderMarker>();
            marker._target = target;
            marker._heightAboveHead = heightAboveHead;
            marker._renderer = go.GetComponent<Renderer>();
            marker._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marker._renderer.receiveShadows = false;

            // 共有のマテリアルを塗り替えると他のプリミティブまで色が変わるので、目印ごとに複製する。
            marker._material = new Material(marker._renderer.sharedMaterial) { name = go.name };
            marker._renderer.sharedMaterial = marker._material;
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

        private void ApplyColor()
        {
            if (_material != null && _target != null)
            {
                _material.color = ColorOf(_target.Side, _viewerTeam);
            }
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
            if (_renderer != null)
            {
                _renderer.enabled = shown;
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

            transform.SetPositionAndRotation(
                new Vector3(center.x, top + _heightAboveHead + (transform.lossyScale.y * 0.5f), center.z),
                Quaternion.Euler(45f, _spin, 45f));
        }

        /// <summary>目印と複製したマテリアルを消す。Edit Mode では OnDestroy が呼ばれないので、マテリアルもここで消す。</summary>
        public void Remove()
        {
            StopFollowingSettings();
            if (_material != null)
            {
                DestroySafely(_material);
                _material = null;
            }

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
            if (_material != null)
            {
                DestroySafely(_material);
                _material = null;
            }
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
