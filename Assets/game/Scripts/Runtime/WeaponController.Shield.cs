using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 盾の武器 (WeaponSpec.Shield) を持っている間は持ち主の前に盾を出し、撃てなくする。攻撃のボタンは殴る合図 (ShieldBashRequested) にする。
    public sealed partial class WeaponController
    {
        public const string SprintLowerCause = "sprint";

        private HeldRiotShield _heldShield;
        private PlayerController _holderMover;

        // 近接攻撃 (MeleeController) が受けて盾の殴りを振る。受け手がいなければ何も起きない。
        public event Action<RiotShieldSpec> ShieldBashRequested;

        public HeldRiotShield HeldShield => _heldShield != null ? _heldShield : null;

        public bool IsShieldWeapon
        {
            get
            {
                var weapon = Weapon;
                return weapon != null && weapon.Spec.IsShield;
            }
        }

        public bool IsShieldRaised => HeldShield != null && _heldShield.IsRaised;

        // 持ち替え中・しまっている間に殴れると、構えていない盾で殴れてしまう。
        public bool IsShieldBashBlocked => IsHolstered || IsSwitching || (FireBlocker != null && FireBlocker());

        // 走って盾を下げている間は掛けない。下げて走るほうが速いのは、盾を構えたまま走れない代わりにするため。
        public float ShieldMoveSpeedMultiplier => IsShieldRaised ? _heldShield.Spec.MoveSpeedMultiplier : 1f;

        private string HolderId
        {
            get
            {
                if (_aim == null)
                {
                    return name;
                }

                var damageable = _aim.root.GetComponent<Damageable>();
                return damageable != null ? damageable.Id : _aim.root.name;
            }
        }

        // 持っている武器・しまったか・走っているかに盾を合わせる。Tick の終わりに毎回呼ぶ。
        public void SyncShield()
        {
            var weapon = Weapon;
            var wanted = _aim != null && weapon != null && weapon.Spec.IsShield && !IsHolstered && !IsSwitching && !IsInputHandedOff;
            if (!wanted)
            {
                PutAwayShield();
                return;
            }

            if (HeldShield != null && (!string.Equals(_heldShield.WeaponId, weapon.Spec.Id, StringComparison.Ordinal)
                || !ReferenceEquals(_heldShield.Spec, weapon.Spec.Shield)))
            {
                PutAwayShield();
            }

            if (HeldShield == null)
            {
                var definition = Definition;
                var prefab = definition != null ? definition.Shield.Prefab : null;
                _heldShield = HeldRiotShield.Create(_aim.root, weapon.Spec.Id, weapon.Spec.Shield, prefab);
                Debug.Log(RiotShieldLog.Out(HolderId, weapon.Spec.Id), this);
            }

            var raised = !IsHolderSprinting();
            if (_heldShield.SetRaised(raised))
            {
                Debug.Log(raised ? RiotShieldLog.Raised(HolderId) : RiotShieldLog.Lowered(HolderId, SprintLowerCause), this);
            }
        }

        private void PutAwayShield()
        {
            if (ReferenceEquals(_heldShield, null))
            {
                return;
            }

            var held = _heldShield;
            _heldShield = null;
            if (held == null)
            {
                return;
            }

            Debug.Log(RiotShieldLog.Away(HolderId, held.WeaponId), this);

            // Destroy はフレームの終わりまで待つので、同じフレームの後の弾を止めないよう先に切る。
            var plate = held.gameObject;
            plate.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(plate);
            }
            else
            {
                DestroyImmediate(plate);
            }
        }

        private bool IsHolderSprinting()
        {
            if (_holderMover == null && _aim != null)
            {
                _holderMover = _aim.root.GetComponent<PlayerController>();
            }

            return _holderMover != null && _holderMover.IsSprinting;
        }

        // 盾の武器は弾を撃たないので、押した瞬間だけ殴る合図を出し、溜めや連射の状態は持たない。
        private bool PullShieldTrigger(Weapon weapon, bool wasPressedThisFrame)
        {
            _trigger.Drop();
            if (wasPressedThisFrame && !IsShieldBashBlocked)
            {
                ShieldBashRequested?.Invoke(weapon.Spec.Shield);
            }

            return false;
        }
    }
}
