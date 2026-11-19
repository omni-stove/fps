using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BombSite : MonoBehaviour
    {
        public const float DefaultRadius = 3f;
        public const float DefaultHeight = 6f;

        public const string MarkerName = "BombSiteMarker";

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
        private bool _followsSettings;

        public static Color MarkerColorOf(bool planted, TeamPalette palette)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            return TeamColors.ToColor(planted ? palette.Danger : palette.Objective);
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

        public bool IsPlanted => _planted;

        public GameObject Marker => _marker;

        public bool Contains(Vector3 position)
        {
            return Area.Contains(position.ToNumerics());
        }

        public void ShowPlanted(bool planted)
        {
            _planted = planted;
            ApplyMarkerColor();
        }

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
                if (!_followsSettings)
                {
                    AppliedControlSettings.Changed += HandleSettingsChanged;
                    _followsSettings = true;
                }

                ApplyMarkerColor();
            }

            ControlPointZone.FitMarker(_marker.transform, transform.lossyScale, _radius, _markerFromPrefab);
        }

        private void ApplyMarkerColor()
        {
            if (_markerMaterials.Count == 0)
            {
                return;
            }

            var color = MarkerColorOf(_planted, TeamColors.Current);
            foreach (var material in _markerMaterials)
            {
                if (material != null)
                {
                    material.color = color;
                }
            }
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                return;
            }

            ApplyMarkerColor();
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

            ControlPointZone.DestroyMaterials(_markerMaterials);
        }

        private void OnDrawGizmos()
        {
            if (!(_radius > 0f) || !(_height > 0f))
            {
                return;
            }

            Gizmos.color = MarkerColorOf(_planted, TeamPalette.Standard);
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
