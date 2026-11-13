using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetPlayer"/> のうち、持ち主の射撃の入力 (Attack と射撃モード) を読み、<see cref="FireServerRpc"/> でサーバーへ送る処理。
    /// </summary>
    public sealed partial class NetPlayer
    {
        private void Update()
        {
            if (!IsSpawned || !IsOwner || _attackAction == null || IsWatchingDeathCamera)
            {
                return;
            }

            var isPressed = _attackAction.IsPressed();
            var wasPressed = _attackAction.WasPressedThisFrame();
            var trigger = ResolveTrigger();
            if (trigger == null)
            {
                if (wasPressed)
                {
                    // 射撃モードと連射間隔が分からない間は、サーバーに弾かれない頻度が分からないので撃たない
                    WarnUnknownWeapon();
                }

                return;
            }

            trigger.Tick(Time.deltaTime);

            // 届いている弾数で撃てないと分かっている射撃は送らない (弾倉が空ならリロードを頼む)。判断はサーバーが行う。
            // ダウン中の射撃はサーバーが捨てるので送らない
            if (IsAlive && !IsDowned && trigger.Pull(isPressed, wasPressed) && (_weapon == null || _weapon.PrepareFire()))
            {
                Fire();
            }
        }

        /// <summary>
        /// 装備中の武器 (<see cref="NetPlayerWeapon.WeaponId"/>) の射撃モードと連射間隔で判断する <see cref="FireTrigger"/>。
        /// 試合 (<see cref="NetTeamDeathmatch.Current"/>) の <see cref="NetTeamDeathmatch.Loadout"/> から引き、武器か Loadout が変わったら作り直す。
        /// 武器がまだ届いていない・Loadout に無い・Loadout が不正なら null。
        /// </summary>
        private FireTrigger ResolveTrigger()
        {
            var weaponId = _weapon != null ? _weapon.WeaponId : string.Empty;
            var match = NetTeamDeathmatch.Current;
            var loadout = match != null ? match.Loadout : null;
            // 引けなかったときも覚えておき、毎フレーム Loadout の武器を作り直さない
            if (_triggerResolved && _triggerLoadout == loadout && string.Equals(_triggerWeaponId, weaponId, StringComparison.Ordinal))
            {
                return _trigger;
            }

            _trigger = null;
            _triggerSpec = null;
            _triggerWeaponId = weaponId;
            _triggerLoadout = loadout;
            _triggerError = null;
            _triggerResolved = true;
            if (string.IsNullOrEmpty(weaponId))
            {
                _triggerError = "before the server equips a weapon";
                return null;
            }

            if (loadout == null)
            {
                _triggerError = "without the match's loadout";
                return null;
            }

            IReadOnlyList<WeaponSpec> specs;
            try
            {
                specs = loadout.ToSpecs();
            }
            catch (WeaponDefinitionException e)
            {
                _triggerError = "with an invalid loadout: " + e.Message;
                return null;
            }

            foreach (var spec in specs)
            {
                if (string.Equals(spec.Id, weaponId, StringComparison.Ordinal))
                {
                    _trigger = new FireTrigger(spec);
                    _triggerSpec = spec;
                    return _trigger;
                }
            }

            _triggerError = "with weapon '" + weaponId + "' missing from '" + loadout.name + "'";
            return null;
        }

        /// <summary>
        /// 装備中の武器の値。<see cref="ResolveTrigger"/> と同じく試合の Loadout から引く。引けなければ null
        /// (<see cref="AimDownSights"/> は共通の初期値の視野角を使う)。
        /// </summary>
        internal WeaponSpec ResolveEquippedSpec()
        {
            ResolveTrigger();
            return _triggerSpec;
        }

        private void WarnUnknownWeapon()
        {
            Debug.LogWarning("[Net] cannot fire " + (_triggerError ?? "without a weapon"), this);
        }

        /// <summary>
        /// 1 発撃つ。目から画面中央が指す点への向きへ、いま見ている時点 (サーバーの時刻 − 補間の遅れ) と装備中の武器の Id を添えて
        /// サーバーに送るだけで、武器の値 (ダメージ・射程など) は送らない。当たり判定とダメージはサーバーが今持たせている武器の値で行う。
        /// </summary>
        private void Fire()
        {
            var serverTime = NetworkManager.ServerTime.Time;
            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime) || serverTime < 0d)
            {
                Debug.LogWarning("[Net] cannot fire before the server time is known", this);
                return;
            }

            // 装備中の武器はサーバーから届く。届く前 (参加直後) や長すぎる Id は送ってもサーバーが捨てるので送らない
            var weaponId = _weapon != null ? _weapon.WeaponId : string.Empty;
            if (string.IsNullOrEmpty(weaponId) || !NetPlayerWeapon.FitsWeaponId(weaponId))
            {
                Debug.LogWarning("[Net] cannot fire before the server equips a weapon", this);
                return;
            }

            var origin = AimOrigin();
            var direction = AimDirection();

            // ホストは他人をサーバー上の今の位置で見ている (補間しない) ので遅れは 0
            var delay = IsServer ? 0 : NetworkPlayerMotor.InterpolationDelayTicks;
            var time = FireTiming.ViewTime(serverTime, NetworkPlayerMotor.TickRate, delay);
            Debug.Log("[Net] fire weapon=" + weaponId + " tick=" + time.Tick.ToString(CultureInfo.InvariantCulture)
                + " alpha=" + time.Alpha.ToString("F2", CultureInfo.InvariantCulture), this);
            FireServerRpc(new FixedString64Bytes(weaponId), origin, direction, time.Tick, time.Alpha);
            ReleaseSprint(SprintInput.ReasonFire);
        }

        /// <summary>
        /// 射撃をサーバーへ送る。weaponId は撃った武器の Id で、武器の値は送らない (サーバーが今持たせている武器の値を使う)。
        /// 試合のロードアウトに無い・サーバーが今持たせている武器と違う Id の射撃は捨てられる。
        /// clientTick + interpolationAlpha は撃った側が他人を見ていた時点 (サーバーの tick)。
        /// サーバーはその時点まで巻き戻して当たり判定をする (<see cref="NetTeamDeathmatch.ServerFire"/>)。
        /// </summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void FireServerRpc(
            FixedString64Bytes weaponId, Vector3 origin, Vector3 direction, int clientTick, float interpolationAlpha, RpcParams rpcParams = default)
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
                match.ServerFire(this, weaponId.ToString(), origin, direction, new ShotTime(clientTick, interpolationAlpha));
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
    }
}
