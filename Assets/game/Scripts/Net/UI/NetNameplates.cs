using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Net
{
    /// <summary>
    /// オンラインの HUD に、他のプレイヤーの頭の上のネームプレート (名前) を出す。<see cref="NetHud.Create"/> が HUD に付ける。
    /// 出すかは <see cref="NameplateRule"/> (味方は壁越しでも、敵は見えているときだけ) で決め、色と印は設定の色の組
    /// (<see cref="TeamPalette.ColorOf(TeamRelation)"/>・<see cref="TeamPalette.NameplateText"/>) から自分のチームから見た敵味方で引く。
    /// 色を見分けにくい人向けの組では、味方に ◆・敵に ▲ を名前の前に付ける。
    /// </summary>
    /// <remarks>
    /// HUD の UIDocument の木に、何も受け取らない (<see cref="PickingMode.Ignore"/>) 層 (<see cref="LayerName"/>) を一番下に差し込み、
    /// 毎フレーム (<see cref="LateUpdate"/>) 頭の上の点を自分のカメラからパネルの座標へ写して Label を動かす。
    /// 木が作り直されたら層を差し込み直す。自分の視点が無い・デスカムを映している間は何も出さない (映しているカメラと位置が合わないため)。
    /// 名前はキルログと同じ <see cref="KillFeedFormat.Player"/> (名前を同期していないので Id)。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NetNameplates : MonoBehaviour
    {
        /// <summary>ネームプレートを入れる層の name。</summary>
        public const string LayerName = "nameplates";

        /// <summary>層の USS クラス。</summary>
        public const string LayerClass = "nameplates";

        /// <summary>1 つのネームプレートの USS クラス。</summary>
        public const string PlateClass = "nameplate";

        /// <summary>頭の上に出すときの、目からの高さ (m)。立っているプレイヤーの頭のてっぺんより少し上。</summary>
        public const float HeightAboveEye = 0.55f;

        // 視線を遮るものを探すバッファ。壁の手前に自分や相手の当たり判定が並んでも足りる数にする
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly Dictionary<NetPlayer, Label> _plates = new Dictionary<NetPlayer, Label>();
        private readonly List<NetPlayer> _gone = new List<NetPlayer>();

        private NetPlayer _owner;
        private UIDocument _document;
        private VisualElement _layer;

        /// <summary>ネームプレートの層。まだ木が無ければ null。</summary>
        public VisualElement Layer => _layer;

        /// <summary>このプロセスの自分のプレイヤー。この人のカメラとチームで出す。</summary>
        public NetPlayer Owner => _owner;

        /// <summary>今出しているネームプレートの数。</summary>
        public int ShownCount
        {
            get
            {
                var count = 0;
                foreach (var plate in _plates.Values)
                {
                    if (plate.style.display.value == DisplayStyle.Flex)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>owner の視点で出す。同じ GameObject の UIDocument (HUD) に層を差し込む。</summary>
        internal void Initialize(NetPlayer owner)
        {
            _owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
            _document = GetComponent<UIDocument>();
            EnsureLayer();
        }

        /// <summary>
        /// ネームプレートの文字と色。viewer のチームから見た team の立場で palette から引く。
        /// </summary>
        internal static (string Text, Color Color) Describe(TeamPalette palette, Team team, Team? viewer, ulong clientId)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            var relation = TeamPalette.RelationOf(team, viewer);
            var name = clientId <= int.MaxValue ? KillFeedFormat.Player((int)clientId) : "?";
            return (palette.NameplateText(relation, name), TeamColors.ToColor(palette.ColorOf(relation)));
        }

        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>今の位置とチームで、出すネームプレートを決め直す。Play 中は LateUpdate が呼ぶ。</summary>
        internal void Refresh()
        {
            if (!EnsureLayer())
            {
                return;
            }

            var camera = _owner != null ? _owner.ViewCamera : null;
            var viewing = _owner != null && _owner.HasLocalView && !_owner.IsWatchingDeathCamera
                && camera != null && camera.isActiveAndEnabled;

            ForgetGonePlayers();
            if (!viewing || _layer.panel == null)
            {
                HideAll();
                return;
            }

            var palette = TeamColors.Current;
            var viewer = (Team?)_owner.Team;
            var eye = camera.transform.position;
            foreach (var player in NetPlayer.SpawnedPlayers)
            {
                if (player == null || player == _owner)
                {
                    continue;
                }

                var head = player.EyePosition() + (Vector3.up * HeightAboveEye);
                var relation = TeamPalette.RelationOf(player.Team, viewer);
                var distance = Vector3.Distance(eye, head);
                var inFront = camera.WorldToViewportPoint(head).z > 0f;

                // 壁越しにも出す味方は視線を調べなくてよいので、敵だけ光線を飛ばす
                var inSight = relation != TeamRelation.Enemy || IsInSight(eye, head, player);
                var show = inFront && NameplateRule.ShouldShow(relation, distance, inSight, player.IsAlive);
                var plate = PlateOf(player);
                if (!show)
                {
                    plate.style.display = DisplayStyle.None;
                    continue;
                }

                var (text, color) = Describe(palette, player.Team, viewer, player.OwnerClientId);
                plate.text = text;
                plate.style.color = color;
                var point = RuntimePanelUtils.CameraTransformWorldToPanel(_layer.panel, head, camera);
                plate.style.left = point.x;
                plate.style.top = point.y;
                plate.style.display = DisplayStyle.Flex;
            }
        }

        private void OnDestroy()
        {
            _layer?.RemoveFromHierarchy();
            _layer = null;
            _plates.Clear();
        }

        // UIDocument は有効にし直すたびに木を作り直すので、層が今の木に付いていなければ差し込み直す
        private bool EnsureLayer()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                return false;
            }

            if (_layer != null && _layer.parent == root)
            {
                return true;
            }

            _layer?.RemoveFromHierarchy();
            _plates.Clear();
            _layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            _layer.AddToClassList(LayerClass);
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0f;
            _layer.style.top = 0f;
            _layer.style.right = 0f;
            _layer.style.bottom = 0f;

            // 照準やメニューより奥に描く
            root.Insert(0, _layer);
            return true;
        }

        private Label PlateOf(NetPlayer player)
        {
            if (_plates.TryGetValue(player, out var plate))
            {
                return plate;
            }

            plate = new Label { pickingMode = PickingMode.Ignore };
            plate.AddToClassList(PlateClass);

            // 層は Hud.uxml の外に差し込むので、Hud.uss が届かなくても読めるよう見た目もここで決める
            plate.style.position = Position.Absolute;
            plate.style.marginLeft = 0f;
            plate.style.marginRight = 0f;
            plate.style.marginTop = 0f;
            plate.style.marginBottom = 0f;
            plate.style.fontSize = 20f;
            plate.style.unityFontStyleAndWeight = FontStyle.Bold;
            plate.style.whiteSpace = WhiteSpace.NoWrap;
            plate.style.textShadow = new TextShadow
            {
                offset = new Vector2(1f, 1f),
                blurRadius = 2f,
                color = new Color(0f, 0f, 0f, 0.8f),
            };
            plate.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-100f));
            plate.style.display = DisplayStyle.None;
            _layer.Add(plate);
            _plates.Add(player, plate);
            return plate;
        }

        private void ForgetGonePlayers()
        {
            _gone.Clear();
            foreach (var player in _plates.Keys)
            {
                if (player == null || !player.IsSpawned)
                {
                    _gone.Add(player);
                }
            }

            foreach (var player in _gone)
            {
                _plates[player].RemoveFromHierarchy();
                _plates.Remove(player);
            }
        }

        private void HideAll()
        {
            foreach (var plate in _plates.Values)
            {
                plate.style.display = DisplayStyle.None;
            }
        }

        // 自分と相手の当たり判定は遮るものとみなさない。トリガー (拠点の範囲など) も見通せる
        private bool IsInSight(Vector3 eye, Vector3 head, NetPlayer target)
        {
            var toHead = head - eye;
            var distance = toHead.magnitude;
            if (distance < 1e-4f)
            {
                return true;
            }

            var count = Physics.RaycastNonAlloc(
                eye, toHead / distance, _hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i].collider;
                if (hit == null)
                {
                    continue;
                }

                var hitTransform = hit.transform;
                if (hitTransform.IsChildOf(target.transform) || hitTransform.IsChildOf(_owner.transform))
                {
                    continue;
                }

                return false;
            }

            // バッファが埋まったら、その先に壁があるか分からないので見えていないとみなす (敵の位置を漏らさない側に倒す)
            return count < _hits.Length;
        }
    }
}
