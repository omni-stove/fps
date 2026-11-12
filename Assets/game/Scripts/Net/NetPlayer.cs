using System;
using System.Globalization;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// 接続したクライアントごとのプレイヤー。チームと体力をサーバーから受け取って見た目に反映し、
    /// サーバーの指示でテレポートする。Attack で撃つと射撃を <see cref="FireServerRpc"/> で送るだけで、
    /// 当たり判定 (ラグ補償) とダメージを含む試合の判断はすべて <see cref="NetTeamDeathmatch"/> (その先の
    /// <see cref="TeamDeathmatchSession"/>) が行う。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetPlayer : NetworkBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Attack アクション。</summary>
        public const string AttackActionPath = "Player/Attack";

        /// <summary>参加前を表す <see cref="ServerPlayerId"/>。</summary>
        public const int NotJoined = -1;

        /// <summary>射撃の始点の、本体 (カプセルの中心) からの高さ (m)。</summary>
        public const float EyeHeight = 0.6f;

        public static readonly Color AlphaColor = new Color(0.25f, 0.5f, 1f);
        public static readonly Color BravoColor = new Color(1f, 0.3f, 0.25f);

        /// <summary>
        /// サーバーにテレポートさせられたとき、所有クライアント (ホストなら自分) で呼ばれる。
        /// 予測中の位置は <see cref="NetworkPlayerMotor"/> がサーバーの確定状態との照合で置き直すので、ここでは通知だけ受け取れる。
        /// </summary>
        public event Action<Vector3, Quaternion> Teleported;

        private readonly NetworkVariable<Team> _team = new NetworkVariable<Team>(Team.Alpha);

        // 0 は倒れているか、まだ試合に参加していないことを表す
        private readonly NetworkVariable<int> _health = new NetworkVariable<int>(0);

        private NetworkPlayerMotor _motor;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Collider[] _colliders = Array.Empty<Collider>();
        private Material[] _ownedMaterials = Array.Empty<Material>();
        private InputAction _attackAction;
        private bool _enabledAttackAction;

        public Team Team => _team.Value;
        public int Health => _health.Value;
        public bool IsAlive => _health.Value > 0;

        /// <summary>サーバーでだけ使う、試合でのプレイヤー id。参加前は <see cref="NotJoined"/>。</summary>
        public int ServerPlayerId { get; internal set; } = NotJoined;

        private void Awake()
        {
            _motor = GetComponent<NetworkPlayerMotor>();
            _renderers = GetComponentsInChildren<Renderer>(true);

            // CharacterController の有効・無効は NetworkPlayerMotor が役割ごとに決めるので、ここでは触らない
            _colliders = Array.FindAll(GetComponentsInChildren<Collider>(true), c => !(c is CharacterController));
        }

        public override void OnNetworkSpawn()
        {
            _team.OnValueChanged += HandleTeamChanged;
            _health.OnValueChanged += HandleHealthChanged;
            ApplyVisuals();

            if (IsOwner)
            {
                ResolveAttackAction();
            }
        }

        public override void OnNetworkDespawn()
        {
            _team.OnValueChanged -= HandleTeamChanged;
            _health.OnValueChanged -= HandleHealthChanged;
            ReleaseAttackAction();

            if (IsServer && NetTeamDeathmatch.Current != null)
            {
                NetTeamDeathmatch.Current.ServerLeave(this);
            }
        }

        public override void OnDestroy()
        {
            foreach (var material in _ownedMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            _ownedMaterials = Array.Empty<Material>();
            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _attackAction == null)
            {
                return;
            }

            if (_attackAction.WasPressedThisFrame() && IsAlive)
            {
                Fire();
            }
        }

        /// <summary>
        /// 1 発撃つ。目の高さから照準の向きへ、いま見ている時点 (サーバーの時刻 − 補間の遅れ) を添えてサーバーに送るだけで、
        /// 当たり判定とダメージはサーバーが行う。
        /// </summary>
        private void Fire()
        {
            var serverTime = NetworkManager.ServerTime.Time;
            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime) || serverTime < 0d)
            {
                Debug.LogWarning("[Net] cannot fire before the server time is known", this);
                return;
            }

            var origin = transform.position + (Vector3.up * EyeHeight);
            var direction = AimDirection(origin);

            // ホストは他人をサーバー上の今の位置で見ている (補間しない) ので遅れは 0
            var delay = IsServer ? 0 : NetworkPlayerMotor.InterpolationDelayTicks;
            var time = FireTiming.ViewTime(serverTime, NetworkPlayerMotor.TickRate, delay);
            Debug.Log("[Net] fire tick=" + time.Tick.ToString(CultureInfo.InvariantCulture)
                + " alpha=" + time.Alpha.ToString("F2", CultureInfo.InvariantCulture), this);
            FireServerRpc(origin, direction, time.Tick, time.Alpha);
        }

        /// <summary>
        /// 照準の向き。画面のカメラからマウスの位置へ伸ばした線が、目の高さの水平面と交わる点へ向ける
        /// (NetTest シーンは見下ろしのカメラなので、狙いたい相手をクリックすればよい)。
        /// カメラかマウスが無いか、交わらなければ本体の正面。
        /// </summary>
        private Vector3 AimDirection(Vector3 origin)
        {
            var camera = Camera.main;
            var mouse = Mouse.current;
            if (camera != null && mouse != null)
            {
                var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
                var eyePlane = new Plane(Vector3.up, origin);
                if (eyePlane.Raycast(ray, out var enter))
                {
                    var toPoint = ray.GetPoint(enter) - origin;
                    toPoint.y = 0f;
                    if (toPoint.sqrMagnitude > 1e-6f)
                    {
                        return toPoint.normalized;
                    }
                }
            }

            return transform.forward;
        }

        /// <summary>サーバーから、試合で決まったチームと体力を書き込む。</summary>
        internal void ServerSetState(Team team, int health)
        {
            RequireServer();
            _team.Value = team;
            _health.Value = health;
        }

        /// <summary>
        /// サーバーで position へ瞬間移動させる。位置の同期は <see cref="NetworkPlayerMotor"/> に任せ
        /// (予測の照合・補間の飛ばしもそちら)、所有クライアントには <see cref="Teleported"/> を出させる。
        /// </summary>
        internal void ServerTeleport(Vector3 position)
        {
            RequireServer();
            var rotation = transform.rotation;
            if (_motor != null && _motor.IsSpawned)
            {
                _motor.ServerTeleport(position);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            TeleportedRpc(position, rotation);
        }

        [Rpc(SendTo.Owner)]
        private void TeleportedRpc(Vector3 position, Quaternion rotation)
        {
            Teleported?.Invoke(position, rotation);
        }

        /// <summary>
        /// 射撃をサーバーへ送る。clientTick + interpolationAlpha は撃った側が他人を見ていた時点 (サーバーの tick)。
        /// サーバーはその時点まで巻き戻して当たり判定をする (<see cref="NetTeamDeathmatch.ServerFire"/>)。
        /// </summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void FireServerRpc(Vector3 origin, Vector3 direction, int clientTick, float interpolationAlpha, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning("[Net] ignored shot from client " + rpcParams.Receive.SenderClientId
                    + " for player owned by " + OwnerClientId);
                return;
            }

            var match = NetTeamDeathmatch.Current;
            if (match != null)
            {
                match.ServerFire(this, origin, direction, new ShotTime(clientTick, interpolationAlpha));
            }
        }

        private void HandleTeamChanged(Team previous, Team current)
        {
            ApplyVisuals();
        }

        private void HandleHealthChanged(int previous, int current)
        {
            ApplyVisuals();
        }

        /// <summary>チームの色を塗り、倒れている間は見た目と当たりを消す。</summary>
        private void ApplyVisuals()
        {
            var alive = IsAlive;
            var color = _team.Value == Team.Bravo ? BravoColor : AlphaColor;

            if (_ownedMaterials.Length != _renderers.Length)
            {
                // renderer.material は複製を作るので、自分で破棄できるよう控えておく
                _ownedMaterials = new Material[_renderers.Length];
                for (var i = 0; i < _renderers.Length; i++)
                {
                    _ownedMaterials[i] = _renderers[i].material;
                }
            }

            for (var i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = alive;
                _ownedMaterials[i].color = color;
            }

            foreach (var hitbox in _colliders)
            {
                hitbox.enabled = alive;
            }
        }

        private void ResolveAttackAction()
        {
            if (Application.isBatchMode)
            {
                // 画面なしでは入力が無い
                return;
            }

            var actions = InputSystem.actions;
            var attack = actions != null ? actions.FindAction(AttackActionPath) : null;
            if (attack == null)
            {
                Debug.LogWarning("[Net] input action '" + AttackActionPath + "' was not found; firing is disabled");
                return;
            }

            _attackAction = attack;
            if (!_attackAction.enabled)
            {
                _attackAction.Enable();
                _enabledAttackAction = true;
            }
        }

        private void ReleaseAttackAction()
        {
            if (_enabledAttackAction && _attackAction != null)
            {
                _attackAction.Disable();
            }

            _enabledAttackAction = false;
            _attackAction = null;
        }

        private void RequireServer()
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("only the server may change " + name);
            }
        }
    }
}
