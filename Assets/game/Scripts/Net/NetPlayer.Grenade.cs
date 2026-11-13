using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetPlayer"/> のうち、グレネード。持ち主は Grenade (<see cref="GrenadeActionPath"/>) を押すと、投げる向き
    /// (<see cref="AimDirection"/>) だけを <see cref="ThrowGrenadeRpc"/> でサーバーへ送る。数・間隔・位置・初速の判断と、グレネードを出すのは
    /// サーバー (<see cref="NetTeamDeathmatch"/> の投げる処理) が行う。残りの数はサーバーが <see cref="Grenades"/> で持ち主にだけ同期する。
    /// </summary>
    /// <remarks>
    /// プレハブには <see cref="CharacterComponents.Attach"/> がオフラインと同じ <see cref="GrenadeThrower"/> を付けているが、ネット対戦では
    /// Configure しないので入力を読まず投げもしない (ローカルにだけグレネードを出してしまうため)。
    /// </remarks>
    public sealed partial class NetPlayer
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Grenade アクション (キーボードの G、ゲームパッドの左ショルダー)。</summary>
        public const string GrenadeActionPath = InputActionPaths.Grenade;

        // 他人の数は表示に要らず、送ると相手が投げられるかが分かってしまうので、持ち主 (とサーバー) にだけ送る
        private readonly NetworkVariable<NetGrenadeState> _grenades = new NetworkVariable<NetGrenadeState>(
            NetGrenadeState.None, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        private InputAction _grenadeAction;
        private bool _enabledGrenadeAction;

        // 持ち主が最後に送ってから GrenadeThrower.ThrowSeconds の残り。サーバーに断られる連打を送らないためだけに使う
        private float _grenadeRequestCooldown;

        /// <summary>サーバーから届いたグレネードの状態。持ち主とサーバーでだけ読める (他では <see cref="NetGrenadeState.None"/>)。</summary>
        public NetGrenadeState Grenades => _grenades.Value;

        /// <summary><see cref="NetHud"/> が OnValueChanged を購読するグレネードの同期値。</summary>
        internal NetworkVariable<NetGrenadeState> GrenadesVariable => _grenades;

        /// <summary>
        /// 持ち主が投げてから <see cref="GrenadeThrower.ThrowSeconds"/> 秒の間か。この間は射撃の入力を送らない (オフラインの <see cref="GameBootstrap"/> と同じ)。
        /// </summary>
        public bool IsThrowingGrenade => _grenadeRequestCooldown > 0f;

        /// <summary>サーバーから、グレネードの状態を書き込む。変わらなければ何もしない (送らない)。</summary>
        internal void ServerSetGrenades(NetGrenadeState state)
        {
            RequireServer();
            if (!_grenades.Value.Equals(state))
            {
                _grenades.Value = state;
            }
        }

        /// <summary>
        /// 持ち主が今投げる要求を送ってよいか。生きていて・ダウン中でなく・デスカムを映しておらず・届いた状態で残りがあり・直前に送っていなければ true。
        /// 判断はサーバーがやり直すので、ここは断られると分かっている要求を送らないためだけのもの。
        /// </summary>
        internal static bool CanRequestGrenade(bool alive, bool downed, bool watchingDeathCamera, NetGrenadeState state, float cooldownSeconds)
        {
            return alive && !downed && !watchingDeathCamera && state.CanThrow && !(cooldownSeconds > 0f);
        }

        // 持ち主の Update から毎フレーム呼ぶ
        private void UpdateGrenadeInput(float deltaSeconds)
        {
            _grenadeRequestCooldown = Mathf.Max(0f, _grenadeRequestCooldown - deltaSeconds);
            if (_grenadeAction == null || !_grenadeAction.WasPressedThisFrame())
            {
                return;
            }

            if (!CanRequestGrenade(IsAlive, IsDowned, IsWatchingDeathCamera, _grenades.Value, _grenadeRequestCooldown))
            {
                return;
            }

            var direction = AimDirection();
            if (!NetMath.IsFinite(direction) || direction.sqrMagnitude < 1e-8f)
            {
                return;
            }

            _grenadeRequestCooldown = GrenadeThrower.ThrowSeconds;
            Debug.Log(GrenadeThrower.LogTag + " net request remaining=" + _grenades.Value.Remaining, this);
            ThrowGrenadeRpc(direction.normalized);
            ReleaseSprint(SprintInput.ReasonFire);
        }

        /// <summary>
        /// グレネードを投げる要求をサーバーへ送る。direction は目から画面中央が指す点への向き。位置と初速はサーバーが自分の知る目の位置から決める。
        /// </summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void ThrowGrenadeRpc(Vector3 direction, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning("[Net] ignored grenade from client " + rpcParams.Receive.SenderClientId
                    + " for player owned by " + OwnerClientId);
                return;
            }

            var match = NetTeamDeathmatch.Current;
            if (match != null)
            {
                match.ServerThrowGrenade(this, direction);
            }
        }

        private void ResolveGrenadeAction()
        {
            if (Application.isBatchMode)
            {
                // 画面なしでは入力が無い
                return;
            }

            var actions = InputSystem.actions;
            var grenade = actions != null ? actions.FindAction(GrenadeActionPath) : null;
            if (grenade == null)
            {
                Debug.LogWarning("[Net] input action '" + GrenadeActionPath + "' was not found; throwing grenades is disabled");
                return;
            }

            _grenadeAction = grenade;
            if (!_grenadeAction.enabled)
            {
                _grenadeAction.Enable();
                _enabledGrenadeAction = true;
            }
        }

        private void ReleaseGrenadeAction()
        {
            if (_enabledGrenadeAction && _grenadeAction != null)
            {
                _grenadeAction.Disable();
            }

            _enabledGrenadeAction = false;
            _grenadeAction = null;
            _grenadeRequestCooldown = 0f;
        }
    }
}
