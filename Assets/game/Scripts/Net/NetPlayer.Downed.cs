using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetPlayer"/> のうち、ダウンと蘇生。
    /// <list type="bullet">
    /// <item>ダウン中かどうか・出血の残り・蘇生の進みを <see cref="NetDownedState"/> の NetworkVariable で受け取り、
    /// 移動 (<see cref="NetworkPlayerMotor.SetDowned"/>) を止め、見た目を寝かせる (リグは <see cref="CharacterAnimator"/> が
    /// <see cref="IsDowned"/> から Downed の姿勢にする。<see cref="NetStance.BodyName"/> の見た目があれば <see cref="NetStance.SetLying"/> で倒す)。</item>
    /// <item>画面のあるクライアントでは、ダウン中の味方の頭の上に印 (<see cref="DownedMarkerName"/>) を出す。自分と敵には出さない。</item>
    /// <item>持ち主は Interact を押している間、<see cref="ReviveRange"/> 以内でいちばん近いダウン中の味方を選び、
    /// 移動の入力と同じ tick ごとに <see cref="ReviveRpc"/> を送る。距離・チーム・状態の判断はサーバー
    /// (<see cref="NetTeamDeathmatch.ServerRevive"/>) が行う。</item>
    /// </list>
    /// </summary>
    public sealed partial class NetPlayer
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Interact アクション (キーボードは E、ゲームパッドは Y)。</summary>
        public const string InteractActionPath = InputActionPaths.Interact;

        /// <summary>ダウン中の味方の頭の上に出す印の GameObject の名前の頭。プレイヤーの子にはしない。</summary>
        public const string DownedMarkerName = "DownedMarker";

        /// <summary>起こせる距離 (m)。サーバーの検査 (<see cref="ReviveReach.DefaultRange"/>) と同じ値。</summary>
        public const float ReviveRange = ReviveReach.DefaultRange;

        // 印を立ちのカプセルのてっぺんからどれだけ上に出すか (m) と、印の直径 (m)
        private const float _markerClearance = 0.35f;
        private const float _markerSize = 0.25f;

        private static readonly Color _markerColor = new Color(1f, 0.85f, 0.2f);

        // 生成中のプレイヤー。持ち主が起こす相手を探すのに使う
        private static readonly List<NetPlayer> _spawnedPlayers = new List<NetPlayer>();

        private readonly NetworkVariable<NetDownedState> _downedState = new NetworkVariable<NetDownedState>(NetDownedState.None);

        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private NetworkPlayerMotor _tickSource;
        private NetPlayer _reviveTarget;

        private GameObject _downedMarker;
        private Material _downedMarkerMaterial;
        private float _markerHeight;

        /// <summary>このプロセスで生成中のプレイヤー (自分を含む)。観戦 (<see cref="NetDeathCamera"/>) が映す味方を探すのに使う。</summary>
        internal static IReadOnlyList<NetPlayer> SpawnedPlayers => _spawnedPlayers;

        /// <summary>このプロセスの自分のプレイヤー (持ち主)。印を味方にだけ出すのに使う。無ければ null。</summary>
        public static NetPlayer Local { get; private set; }

        /// <summary>ダウン中か。ダウン中も <see cref="IsAlive"/> は true (体力はダウン中の体力)。</summary>
        public bool IsDowned => _downedState.Value.IsDowned;

        /// <summary>サーバーから届いたダウンの状態。</summary>
        public NetDownedState DownedState => _downedState.Value;

        /// <summary><see cref="NetHud"/> が OnValueChanged を購読するダウンの同期値。</summary>
        internal NetworkVariable<NetDownedState> DownedStateVariable => _downedState;

        /// <summary>
        /// 持ち主がいま起こそうとしている味方 (直前の tick で <see cref="ReviveRpc"/> を送った相手)。押していなければ null。
        /// HUD はこの相手の <see cref="DownedState"/> から蘇生の進みを出す。
        /// </summary>
        public NetPlayer ReviveTarget => _reviveTarget;

        /// <summary>頭の上の印。画面のあるクライアントでだけ作る。無ければ null。</summary>
        internal GameObject DownedMarker => _downedMarker;

        /// <summary>
        /// 頭の上の印を出すか。他のプレイヤーで、ダウン中で、自分のプレイヤーと同じチームのときだけ true。
        /// 自分のプレイヤーがまだいない (観戦だけ) ときは誰にも出さない。
        /// </summary>
        internal static bool ShouldShowDownedMarker(bool isOwner, bool isDowned, Team team, Team? localTeam)
        {
            return !isOwner && isDowned && localTeam.HasValue && localTeam.Value == team;
        }

        /// <summary>サーバーから、Session のダウンの状態を書き込む。変わらなければ何もしない (送らない)。</summary>
        internal void ServerSetDowned(NetDownedState state)
        {
            RequireServer();
            if (_downedState.Value.Equals(state))
            {
                return;
            }

            _downedState.Value = state;

            // サーバーが書いたときに OnValueChanged が呼ばれるかに頼らず、サーバーの移動を次の tick から止める
            ApplyDowned();
        }

        // OnNetworkSpawn の最後に呼ぶ。視点と抜け殻の設定より後に印を作る
        private void SpawnDowned()
        {
            if (!_spawnedPlayers.Contains(this))
            {
                _spawnedPlayers.Add(this);
            }

            _downedState.OnValueChanged += HandleDownedStateChanged;
            ApplyDowned();

            if (IsOwner)
            {
                Local = this;
                ResolveInteractAction();
                _tickSource = _motor != null ? _motor : GetComponent<NetworkPlayerMotor>();
                if (_tickSource != null)
                {
                    _tickSource.OwnerTicked += HandleOwnerTicked;
                }
            }

            if (IsClient && DeathBodyPresenter.IsPresentationAvailable)
            {
                CreateDownedMarker();
            }
        }

        private void DespawnDowned()
        {
            _spawnedPlayers.Remove(this);
            _downedState.OnValueChanged -= HandleDownedStateChanged;
            if (_tickSource != null)
            {
                _tickSource.OwnerTicked -= HandleOwnerTicked;
                _tickSource = null;
            }

            if (Local == this)
            {
                Local = null;
            }

            _reviveTarget = null;
            ReleaseInteractAction();
            DestroyDownedMarker();
        }

        private void HandleDownedStateChanged(NetDownedState previous, NetDownedState current)
        {
            ApplyDowned();
            if (previous.IsDowned == current.IsDowned)
            {
                return;
            }

            Debug.Log("[Net] downed state owner=" + OwnerClientId.ToString(CultureInfo.InvariantCulture) + " " + current, this);
            if (current.IsDowned)
            {
                // ダッシュと覗き込みは持ち主にしか状態が無い。他では何もしない
                ReleaseSprint(SprintInput.ReasonDowned);
                if (_ads != null && _ads.IsInitialized)
                {
                    _ads.ResetView();
                }
            }
        }

        private void ApplyDowned()
        {
            var downed = IsDowned;
            var motor = _motor != null ? _motor : GetComponent<NetworkPlayerMotor>();
            if (motor != null)
            {
                motor.SetDowned(downed);
            }

            var stance = Stance;
            if (stance != null)
            {
                stance.SetLying(downed);
            }
        }

        // 持ち主の tick ごと (移動の入力を送った直後) に呼ばれる
        private void HandleOwnerTicked(int tick)
        {
            _reviveTarget = WantsToRevive() ? FindReviveTarget() : null;
            if (_reviveTarget != null)
            {
                ReviveRpc(_reviveTarget.OwnerClientId);
            }
        }

        private bool WantsToRevive()
        {
            return _interactAction != null && _interactAction.IsPressed() && IsAlive && !IsDowned && !IsWatchingDeathCamera;
        }

        /// <summary><see cref="ReviveRange"/> 以内でいちばん近い、ダウン中の味方。いなければ null。</summary>
        private NetPlayer FindReviveTarget()
        {
            var from = NetMath.ToNumerics(transform.position);
            NetPlayer nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var other in _spawnedPlayers)
            {
                if (other == null || other == this || !other.IsDowned || other.Team != Team)
                {
                    continue;
                }

                var to = NetMath.ToNumerics(other.transform.position);
                if (!ReviveReach.IsWithin(from, to, ReviveRange))
                {
                    continue;
                }

                var distance = System.Numerics.Vector3.DistanceSquared(from, to);
                if (distance < nearestDistance)
                {
                    nearest = other;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 起こす要求をサーバーへ送る。targetClientId は起こしたい味方の持ち主のクライアント Id。
        /// 押している間 tick ごとに送り、届かなくなったらサーバーは少し待って止める (<see cref="ReviveHoldTable"/>)。
        /// </summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void ReviveRpc(ulong targetClientId, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning("[Net] ignored revive from client " + rpcParams.Receive.SenderClientId
                    + " for player owned by " + OwnerClientId);
                return;
            }

            var match = NetTeamDeathmatch.Current;
            if (match != null)
            {
                match.ServerRevive(this, targetClientId);
            }
        }

        private void ResolveInteractAction()
        {
            if (Application.isBatchMode)
            {
                // 画面なしでは入力が無い
                return;
            }

            var actions = InputSystem.actions;
            var interact = actions != null ? actions.FindAction(InteractActionPath) : null;
            if (interact == null)
            {
                Debug.LogWarning("[Net] input action '" + InteractActionPath + "' was not found; reviving allies is disabled");
                return;
            }

            _interactAction = interact;
            if (!_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteractAction = true;
            }
        }

        private void ReleaseInteractAction()
        {
            if (_enabledInteractAction && _interactAction != null)
            {
                _interactAction.Disable();
            }

            _enabledInteractAction = false;
            _interactAction = null;
        }

        // プレイヤーの子にすると、やられ演出の抜け殻 (DeathBodyPresenter は子の Renderer を写す) と一人称の体の隠し方に混ざるので、
        // シーンの直下に置いて LateUpdate で頭の上へ動かす
        private void CreateDownedMarker()
        {
            if (_downedMarker != null)
            {
                return;
            }

            var controller = GetComponent<CharacterController>();
            _markerHeight = controller != null
                ? controller.center.y + (controller.height / 2f) + _markerClearance
                : EyeHeight + _markerClearance;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = DownedMarkerName + " (client=" + OwnerClientId.ToString(CultureInfo.InvariantCulture) + ")";
            marker.SetActive(false);
            var markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                // 射撃の Raycast に当たらないよう、当たりは持たせない
                Destroy(markerCollider);
            }

            marker.transform.localScale = Vector3.one * _markerSize;
            var markerRenderer = marker.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                markerRenderer.receiveShadows = false;
                _downedMarkerMaterial = markerRenderer.material;
                _downedMarkerMaterial.color = _markerColor;
            }

            _downedMarker = marker;
        }

        private void RefreshDownedMarker()
        {
            if (_downedMarker == null)
            {
                return;
            }

            var local = Local;
            Team? localTeam = local != null && local.IsSpawned ? local.Team : (Team?)null;
            var show = ShouldShowDownedMarker(IsOwner, IsDowned, Team, localTeam);
            if (show)
            {
                _downedMarker.transform.position = transform.position + (Vector3.up * _markerHeight);
            }

            if (_downedMarker.activeSelf != show)
            {
                _downedMarker.SetActive(show);
            }
        }

        private void DestroyDownedMarker()
        {
            if (_downedMarker != null)
            {
                Destroy(_downedMarker);
            }

            if (_downedMarkerMaterial != null)
            {
                Destroy(_downedMarkerMaterial);
            }

            _downedMarker = null;
            _downedMarkerMaterial = null;
        }
    }
}
