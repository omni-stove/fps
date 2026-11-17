using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FlagBase : MonoBehaviour
    {
        public const string MarkerName = "FlagBaseMarker";

        private const int GizmoSegments = 32;

        [SerializeField, Min(0), Tooltip("この台の陣営の ID。0 = Alpha、1 = Bravo")]
        private int _sideId;

        [SerializeField, Min(0.1f), Tooltip("範囲の水平の半径 (m)。始めるときに Runner が設定の着いたことにする距離に揃える")]
        private float _radius = CaptureTheFlagSpec.DefaultCaptureRadius;

        [SerializeField, Tooltip("Play 中に範囲を示す円盤を足元に出すか")]
        private bool _showMarker = true;

        [SerializeField, Tooltip("範囲を示す印の見た目。原点を中心にした直径 1m の形にする (水平に範囲の直径まで拡大し、高さはそのまま)。当たり判定は外し、材質は陣営の色で塗る。空なら仮の薄い円盤を作る")]
        private GameObject _markerPrefab;

        private readonly List<Material> _markerMaterials = new List<Material>();
        private GameObject _marker;
        private bool _markerFromPrefab;
        private bool _followsSettings;
        private Team? _viewerTeam = TeamColors.OfflineViewer;

        public Side Side
        {
            get => new Side(Mathf.Max(0, _sideId));
            set
            {
                _sideId = value.Id;
                ApplyMarkerColor();
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
                if (_marker != null)
                {
                    ControlPointZone.FitMarker(_marker.transform, transform.lossyScale, _radius, _markerFromPrefab);
                }
            }
        }

        public Vector3 Position => transform.position;

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

        public GameObject Marker => _marker;

        public Color Color => ColorOf(Side, _viewerTeam);

        public Team? ViewerTeam
        {
            get => _viewerTeam;
            set
            {
                _viewerTeam = value;
                ApplyMarkerColor();
            }
        }

        public bool Contains(Vector3 ground)
        {
            return CaptureTheFlagRunner.IsWithin(ground, Position, _radius);
        }

        public static Color ColorOf(Side side, Team? viewer = TeamColors.OfflineViewer)
        {
            return side.TryGetTeam(out var team) ? TeamColors.Of(team, viewer) : TeamColors.Neutral;
        }

        public void RefreshMarker()
        {
            if (!_showMarker)
            {
                return;
            }

            if (_marker == null)
            {
                if (!_followsSettings)
                {
                    AppliedControlSettings.Changed += HandleSettingsChanged;
                    _followsSettings = true;
                }

                _markerFromPrefab = _markerPrefab != null;
                _marker = ControlPointZone.CreateMarkerObject(_markerPrefab, transform, MarkerName, _markerMaterials);
                ApplyMarkerColor();
            }

            ControlPointZone.FitMarker(_marker.transform, transform.lossyScale, _radius, _markerFromPrefab);
        }

        private void ApplyMarkerColor()
        {
            var color = Color;
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
            if (this != null)
            {
                ApplyMarkerColor();
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
            if (_followsSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsSettings = false;
            }

            ControlPointZone.DestroyMaterials(_markerMaterials);
        }

        private void OnDrawGizmos()
        {
            if (!(_radius > 0f))
            {
                return;
            }

            Gizmos.color = ColorOf(Side);
            var center = transform.position;
            var previous = center + new Vector3(_radius, 0f, 0f);
            for (var i = 1; i <= GizmoSegments; i++)
            {
                var angle = i * Mathf.PI * 2f / GizmoSegments;
                var next = center + new Vector3(Mathf.Cos(angle) * _radius, 0f, Mathf.Sin(angle) * _radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            Gizmos.DrawLine(center, center + (Vector3.up * FlagObject.PoleHeight));
        }
    }
}
