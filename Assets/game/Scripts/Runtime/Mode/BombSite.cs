using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroyRunner"/>) の設置地点 1 つ。地点の番号 (<see cref="Index"/>、0 始まりで HUD とログでは A, B, ...) と
    /// 範囲の半径・高さを持ち、この GameObject の位置を中心とした円柱 (<see cref="ControlPointArea"/>) を範囲にする。
    /// 誰が中にいるかは Runner が <see cref="Contains"/> で調べる。試合中に地点を動かすことは考えない。
    /// Play 中は足元に範囲を示す薄い円盤 (<see cref="MarkerPrefab"/> があればその形) を作り、設置されたら <see cref="PlantedColor"/> で塗る (<see cref="ShowPlanted"/>)。
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

        [SerializeField, Tooltip("範囲を示す印の見た目。原点を中心にした直径 1m の形にする (水平に範囲の直径まで拡大し、高さはそのまま)。当たり判定は外し、材質は設置されたかの色で塗る。空なら仮の薄い円盤を作る")]
        private GameObject _markerPrefab;

        private readonly List<Material> _markerMaterials = new List<Material>();
        private GameObject _marker;
        private bool _markerFromPrefab;
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

        /// <summary>範囲を示す印の見た目。null なら仮の薄い円盤。円盤を作る (<see cref="RefreshMarker"/>) 前に設定したものを使う。</summary>
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

        /// <summary>今の位置での範囲。Inspector の値が不正 (半径・高さが 0 以下) なら <see cref="ArgumentOutOfRangeException"/>。</summary>
        public ControlPointArea Area => new ControlPointArea(_index, transform.position.ToNumerics(), _radius, _height);

        /// <summary>この地点に設置されているか (<see cref="ShowPlanted"/> で最後に渡した値)。</summary>
        public bool IsPlanted => _planted;

        /// <summary>Play 中に作った円盤 (<see cref="MarkerName"/>)。作っていなければ null。</summary>
        public GameObject Marker => _marker;

        /// <summary>position が範囲の中 (境界を含む) なら true。</summary>
        public bool Contains(Vector3 position)
        {
            return Area.Contains(position.ToNumerics());
        }

        /// <summary>設置されているかを覚え、円盤があればその色で塗る。</summary>
        public void ShowPlanted(bool planted)
        {
            _planted = planted;
            ApplyMarkerColor();
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
                _markerFromPrefab = _markerPrefab != null;
                _marker = ControlPointZone.CreateMarkerObject(_markerPrefab, transform, MarkerName, _markerMaterials);
                ApplyMarkerColor();
            }

            ControlPointZone.FitMarker(_marker.transform, transform.lossyScale, _radius, _markerFromPrefab);
        }

        private void ApplyMarkerColor()
        {
            foreach (var material in _markerMaterials)
            {
                if (material != null)
                {
                    material.color = _planted ? PlantedColor : IdleColor;
                }
            }
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
            ControlPointZone.DestroyMaterials(_markerMaterials);
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
    }
}
