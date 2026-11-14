using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="IAbility"/> に渡す、使っている枠とその持ち主。<see cref="AbilityHost"/> が枠に能力を入れるたびに作る。
    /// </summary>
    public sealed class AbilityContext
    {
        private AbilityMovementOverride _movement;

        internal AbilityContext(AbilityHost host, int slotIndex, AbilityDefinition definition, AbilitySlot slot)
        {
            Host = host;
            SlotIndex = slotIndex;
            Definition = definition;
            Slot = slot;
        }

        /// <summary>この能力を持つ枠の持ち主。</summary>
        public AbilityHost Host { get; }

        /// <summary>持ち主の GameObject (プレイヤー)。</summary>
        public GameObject Owner => Host.gameObject;

        /// <summary>持ち主の移動。無ければ null (移動を変える能力は使えない)。</summary>
        public PlayerController Mover => Host.Mover;

        /// <summary>枠の番号 (0 から。0 が Ability1)。</summary>
        public int SlotIndex { get; }

        /// <summary>枠に入っている能力の定義。</summary>
        public AbilityDefinition Definition { get; }

        /// <summary>枠の回数と時間。</summary>
        public AbilitySlot Slot { get; }

        /// <summary>今どこにいるか (<see cref="AbilitySlot.Phase"/>)。</summary>
        public AbilityPhase Phase => Slot.Phase;

        /// <summary>効果が出ているか。</summary>
        public bool IsEffective => Slot.Phase == AbilityPhase.Active;

        /// <summary>この <see cref="IAbility.Tick"/> が、効果が出てから最初の Tick か。効果を 1 度だけ出す能力はここで出す。</summary>
        public bool EffectStartedThisFrame { get; internal set; }

        /// <summary>移動を変えているか (<see cref="OverrideMovement"/> した後で、まだ外していない)。</summary>
        public bool IsOverridingMovement => _movement != null;

        /// <summary>
        /// 能力を使っている間、movement で持ち主の移動を横取りする (<see cref="PlayerController.RegisterMovementOverride"/>)。
        /// 選ばれている間は movement の <see cref="IMovementOverride.Conditions"/> に <see cref="PlayerActionConditions.AbilityMoving"/> を足す。
        /// 能力が終わると外れる。前に渡したものがあれば外してから替える。持ち主に移動が無ければ false。
        /// </summary>
        /// <exception cref="ArgumentNullException">movement が null。</exception>
        /// <exception cref="InvalidOperationException">能力を使っていない。</exception>
        public bool OverrideMovement(IMovementOverride movement)
        {
            if (movement == null)
            {
                throw new ArgumentNullException(nameof(movement));
            }

            if (!Slot.IsRunning)
            {
                throw new InvalidOperationException("the ability can only override movement while it is running");
            }

            StopMovementOverride();
            var mover = Mover;
            if (mover == null)
            {
                return false;
            }

            _movement = new AbilityMovementOverride(movement);
            mover.RegisterMovementOverride(_movement);
            return true;
        }

        /// <summary><see cref="OverrideMovement"/> で渡したものを外す。選ばれていれば、その場で <see cref="IMovementOverride.End"/> が呼ばれる。</summary>
        public void StopMovementOverride()
        {
            var movement = _movement;
            if (movement == null)
            {
                return;
            }

            _movement = null;
            var mover = Mover;
            if (mover != null)
            {
                mover.UnregisterMovementOverride(movement);
            }
        }
    }
}
