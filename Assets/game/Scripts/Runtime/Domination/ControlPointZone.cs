using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 拠点制圧の拠点 1 つ。拠点の番号 (<see cref="Index"/>、0 始まりで HUD とログでは A, B, ...) と範囲の半径・高さを持ち、
    /// この GameObject の位置を中心とした円柱 (<see cref="ControlPointArea"/>) を範囲にする。
    /// 範囲にいる生きた参加者は <see cref="DominationRunner"/> が毎フレーム <see cref="Scan"/> で調べ直す (このコンポーネントは自分では調べない。
    /// 誰が参加者かを知っているのは Runner なので)。試合中に拠点を動かすことは考えない。
    /// Play 中は足元に範囲を示す薄い円盤を作り、持ち主のチームの色 (<see cref="ShowOwner"/>) で塗る。色は画面のプレイヤーのチーム
    /// (<see cref="ViewerTeam"/>) から見た敵味方の色で、設定の色の組 (<see cref="TeamColors"/>) が変わったら塗り直す。Scene ビューには範囲を線で描く。
    /// ハードポイントのように有効な拠点が 1 つだけのモードは、<see cref="SetHighlight"/> で有効な拠点の円盤を光らせ、他を暗くする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControlPointZone : MonoBehaviour
    {
        public const float DefaultRadius = 5f;
        public const float DefaultHeight = 6f;

        /// <summary>Play 中に作る、範囲を示す円盤の名前。</summary>
        public const string MarkerName = "ControlPointMarker";

        /// <summary>円盤の厚み (m)。床に埋もれず、上に立っても段差にならない薄さにする。</summary>
        public const float MarkerThickness = 0.04f;

        /// <summary>光らせていない (<see cref="SetHighlight"/> に false を渡した) 拠点の円盤の明るさ。</summary>
        public const float DimmedBrightness = 0.3f;

        /// <summary>光らせた拠点の円盤の発光の強さ (チームの色に掛ける)。</summary>
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

        private readonly List<Damageable> _occupants = new List<Damageable>();
        private Renderer _marker;
        private Material _markerMaterial;
        private Team? _owner;
        private bool? _highlight;
        private Team? _viewerTeam = TeamColors.OfflineViewer;
        private bool _followsSettings;

        /// <summary>
        /// 画面のプレイヤーのチーム。持ち主がこのチームなら味方の色、違えば敵の色で塗る。既定はオフラインのプレイヤーのチーム
        /// (<see cref="TeamColors.OfflineViewer"/>)。null なら Alpha を味方の色にする。
        /// </summary>
        public Team? ViewerTeam
        {
            get => _viewerTeam;
            set
            {
                _viewerTeam = value;
                ApplyMarkerColor();
            }
        }

        /// <summary>拠点の番号。0 以上。</summary>
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

        /// <summary>範囲の水平の半径 (m)。0 より大きい有限値。円盤を出した後に変えたら <see cref="RefreshMarker"/> で大きさを合わせる。</summary>
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

        /// <summary>範囲の高さ (m)。この GameObject の位置から上下に半分ずつ。0 より大きい有限値。</summary>
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

        /// <summary>Play 中に範囲を示す円盤を出すか。開始後に変えても、作った円盤はそのまま。</summary>
        public bool ShowsMarker
        {
            get => _showMarker;
            set => _showMarker = value;
        }

        /// <summary>今の位置での範囲。Inspector の値が不正 (半径・高さが 0 以下) なら <see cref="ArgumentOutOfRangeException"/>。</summary>
        public ControlPointArea Area => new ControlPointArea(_index, transform.position.ToNumerics(), _radius, _height);

        /// <summary>最後の <see cref="Scan"/> で範囲にいた、生きている参加者。</summary>
        public IReadOnlyList<Damageable> Occupants => _occupants;

        /// <summary><see cref="ShowOwner"/> で最後に渡された持ち主。中立なら null。</summary>
        public Team? Owner => _owner;

        /// <summary>
        /// <see cref="SetHighlight"/> で最後に渡された値。true は光らせている、false は暗くしている、null はどちらでもない (既定。拠点制圧の見た目)。
        /// </summary>
        public bool? Highlight => _highlight;

        /// <summary>Play 中に作った円盤。作っていなければ null。</summary>
        public Renderer Marker => _marker;

        /// <summary>position が範囲の中 (境界を含む) なら true。</summary>
        public bool Contains(Vector3 position)
        {
            return Area.Contains(position.ToNumerics());
        }

        /// <summary>
        /// candidates のうち、生きていて位置 (Transform) が範囲の中にいるものを <see cref="Occupants"/> に入れ直す。
        /// null と倒れているものは数えない。
        /// </summary>
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

        /// <summary>持ち主を覚え、円盤があればその色で塗る。中立なら null。</summary>
        public void ShowOwner(Team? owner)
        {
            _owner = owner;
            ApplyMarkerColor();
        }

        /// <summary>
        /// 円盤を光らせる (true)、暗くする (false)、どちらでもない元の色に戻す (null)。円盤がまだ無くても覚えておき、作ったときに合わせる。
        /// </summary>
        public void SetHighlight(bool? highlighted)
        {
            _highlight = highlighted;
            ApplyMarkerColor();
        }

        /// <summary>
        /// 持ち主と光らせ方に応じた円盤の色 (今の色の組で、viewer から見た色)。暗くするときは <see cref="DimmedBrightness"/> を掛ける (不透明度はそのまま)。
        /// </summary>
        public static Color MarkerColorOf(Team? owner, bool? highlight, Team? viewer = TeamColors.OfflineViewer)
        {
            var color = ColorOf(owner, viewer);
            if (highlight == false)
            {
                color = new Color(color.r * DimmedBrightness, color.g * DimmedBrightness, color.b * DimmedBrightness, color.a);
            }

            return color;
        }

        /// <summary>今の色の組で、viewer のチームから見た持ち主の色。中立 (null) なら組の中立の色。</summary>
        public static Color ColorOf(Team? owner, Team? viewer = TeamColors.OfflineViewer)
        {
            return TeamColors.Of(owner, viewer);
        }

        /// <summary>
        /// 範囲を示す円盤を作るか、作ってあれば今の半径に大きさを合わせる。当たり判定は持たせない (弾と移動を遮らないように)。
        /// <see cref="ShowsMarker"/> が false なら何もしない。Play 中は Start で呼ぶ。
        /// </summary>
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

            // 親の拡縮を打ち消し、世界で直径 2r・厚み MarkerThickness にする (Cylinder は高さ 2・直径 1)。
            var parentScale = transform.lossyScale;
            _marker.transform.localScale = new Vector3(
                SafeDivide(_radius * 2f, parentScale.x),
                SafeDivide(MarkerThickness * 0.5f, parentScale.y),
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
            if (_followsSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsSettings = false;
            }

            if (_markerMaterial != null)
            {
                DestroySafely(_markerMaterial);
                _markerMaterial = null;
            }
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

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = MarkerName;
            DestroySafely(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);

            _marker = marker.GetComponent<Renderer>();
            _marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _marker.receiveShadows = false;

            // 共有のマテリアルを塗り替えると他のプリミティブまで色が変わるので、この拠点用に複製する。
            _markerMaterial = new Material(_marker.sharedMaterial) { name = MarkerName };
            _marker.sharedMaterial = _markerMaterial;
            ApplyMarkerColor();
        }

        private void ApplyMarkerColor()
        {
            if (_markerMaterial == null)
            {
                return;
            }

            _markerMaterial.color = MarkerColorOf(_owner, _highlight, _viewerTeam);

            // 発光を持たないシェーダーでは色だけ変える。
            if (!_markerMaterial.HasProperty(EmissionColorId))
            {
                return;
            }

            if (_highlight == true)
            {
                _markerMaterial.EnableKeyword(EmissionKeyword);
                _markerMaterial.SetColor(EmissionColorId, ColorOf(_owner, _viewerTeam) * HighlightEmission);
            }
            else
            {
                _markerMaterial.DisableKeyword(EmissionKeyword);
                _markerMaterial.SetColor(EmissionColorId, Color.black);
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
