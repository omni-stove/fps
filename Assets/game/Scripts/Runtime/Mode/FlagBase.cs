using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// 旗取り (<see cref="CaptureTheFlagRunner"/>) の陣営の旗の台。陣営 (<see cref="Side"/>) と、運んできた人が着いたことにする範囲の半径 (<see cref="Radius"/>) を持ち、
    /// この GameObject の位置 (足元) に旗が立つ。Runner は始めるときに半径を設定の <see cref="CaptureTheFlagSpec.CaptureRadius"/> に揃える。
    /// 試合中に台を動かすことは考えない。Play 中は足元に範囲を示す薄い円盤 (<see cref="MarkerPrefab"/> があればその形) を作り、陣営の色で塗る。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlagBase : MonoBehaviour
    {
        /// <summary>Play 中に作る、範囲を示す円盤の名前。</summary>
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

        /// <summary>この台の陣営。</summary>
        public Side Side
        {
            get => new Side(Mathf.Max(0, _sideId));
            set
            {
                _sideId = value.Id;
                ApplyMarkerColor();
            }
        }

        /// <summary>範囲の水平の半径 (m)。0 より大きい有限値。円盤があれば大きさを合わせ直す。</summary>
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

        /// <summary>旗が立つ足元。</summary>
        public Vector3 Position => transform.position;

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

        /// <summary>Play 中に作った円盤 (<see cref="MarkerName"/>)。作っていなければ null。</summary>
        public GameObject Marker => _marker;

        /// <summary>陣営の色。Alpha と Bravo は今の色の組の陣営の色、それ以外は中立の色。</summary>
        public Color Color => ColorOf(Side);

        /// <summary>
        /// 足元 ground が範囲の中 (水平に <see cref="Radius"/> 以内、上下に <see cref="CaptureTheFlagRunner.ReachHeight"/> 以内) なら true。
        /// </summary>
        public bool Contains(Vector3 ground)
        {
            return CaptureTheFlagRunner.IsWithin(ground, Position, _radius);
        }

        /// <summary>side の旗・台の色。Alpha と Bravo は今の色の組の、オフラインのプレイヤーから見た陣営の色、それ以外は中立の色。</summary>
        public static Color ColorOf(Side side)
        {
            return side.TryGetTeam(out var team) ? TeamColors.Of(team, TeamColors.OfflineViewer) : TeamColors.Neutral;
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
