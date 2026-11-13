using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroyRunner"/>) の設置地点 1 つ。地点の番号 (<see cref="Index"/>、0 始まりで HUD とログでは A, B, ...) と
    /// 範囲の半径・高さを持ち、この GameObject の位置を中心とした円柱 (<see cref="ControlPointArea"/>) を範囲にする。
    /// 誰が中にいるかは Runner が <see cref="Contains"/> で調べる。試合中に地点を動かすことは考えない。
    /// Play 中は足元に範囲を示す薄い円盤を作り、設置されたら <see cref="PlantedColor"/> で塗る (<see cref="ShowPlanted"/>)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BombSite : MonoBehaviour
    {
        public const float DefaultRadius = 3f;
        public const float DefaultHeight = 6f;

        /// <summary>Play 中に作る、範囲を示す円盤の名前。</summary>
        public const string MarkerName = "BombSiteMarker";

        public static readonly Color IdleColor = new Color(1f, 0.65f, 0.1f);
        public static readonly Color PlantedColor = new Color(1f, 0.15f, 0.1f);

        private const int GizmoSegments = 32;

        [SerializeField, Min(0), Tooltip("地点の番号。シーンの地点で 0 から抜けなく 1 つずつ振る。HUD とログでは 0 = A, 1 = B")]
        private int _index;

        [SerializeField, Min(0.1f), Tooltip("範囲の水平の半径 (m)")]
        private float _radius = DefaultRadius;

        [SerializeField, Min(0.1f), Tooltip("範囲の高さ (m)。この GameObject の位置から上下に半分ずつ")]
        private float _height = DefaultHeight;

        [SerializeField, Tooltip("Play 中に範囲を示す円盤を足元に出すか")]
        private bool _showMarker = true;

        private Renderer _marker;
        private Material _markerMaterial;
        private bool _planted;

        /// <summary>地点の番号。0 以上。</summary>
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

        /// <summary>範囲の水平の半径 (m)。0 より大きい有限値。</summary>
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

        /// <summary>範囲の高さ (m)。0 より大きい有限値。</summary>
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

        public bool ShowsMarker
        {
            get => _showMarker;
            set => _showMarker = value;
        }

        /// <summary>今の位置での範囲。Inspector の値が不正 (半径・高さが 0 以下) なら <see cref="ArgumentOutOfRangeException"/>。</summary>
        public ControlPointArea Area => new ControlPointArea(_index, transform.position.ToNumerics(), _radius, _height);

        /// <summary>この地点に設置されているか (<see cref="ShowPlanted"/> で最後に渡した値)。</summary>
        public bool IsPlanted => _planted;

        /// <summary>Play 中に作った円盤。作っていなければ null。</summary>
        public Renderer Marker => _marker;

        /// <summary>position が範囲の中 (境界を含む) なら true。</summary>
        public bool Contains(Vector3 position)
        {
            return Area.Contains(position.ToNumerics());
        }

        /// <summary>設置されているかを覚え、円盤があればその色で塗る。</summary>
        public void ShowPlanted(bool planted)
        {
            _planted = planted;
            if (_markerMaterial != null)
            {
                _markerMaterial.color = planted ? PlantedColor : IdleColor;
            }
        }

        /// <summary>範囲を示す円盤を作るか、作ってあれば今の半径に大きさを合わせる。当たり判定は持たせない。<see cref="ShowsMarker"/> が false なら何もしない。</summary>
        public void RefreshMarker()
        {
            if (!_showMarker)
            {
                return;
            }

            if (_marker == null)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = MarkerName;
                DestroySafely(marker.GetComponent<Collider>());
                marker.transform.SetParent(transform, false);

                _marker = marker.GetComponent<Renderer>();
                _marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _marker.receiveShadows = false;

                // 共有のマテリアルを塗り替えると他のプリミティブまで色が変わるので、この地点用に複製する。
                _markerMaterial = new Material(_marker.sharedMaterial) { name = MarkerName };
                _marker.sharedMaterial = _markerMaterial;
                _markerMaterial.color = _planted ? PlantedColor : IdleColor;
            }

            // 親の拡縮を打ち消し、世界で直径 2r・厚み ControlPointZone.MarkerThickness にする (Cylinder は高さ 2・直径 1)。
            var parentScale = transform.lossyScale;
            _marker.transform.localScale = new Vector3(
                SafeDivide(_radius * 2f, parentScale.x),
                SafeDivide(ControlPointZone.MarkerThickness * 0.5f, parentScale.y),
                SafeDivide(_radius * 2f, parentScale.z));
            _marker.transform.localPosition = Vector3.zero;
            _marker.transform.localRotation = Quaternion.identity;
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
            if (_markerMaterial != null)
            {
                DestroySafely(_markerMaterial);
                _markerMaterial = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (!(_radius > 0f) || !(_height > 0f))
            {
                return;
            }

            Gizmos.color = _planted ? PlantedColor : IdleColor;
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
