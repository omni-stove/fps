using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // PlayerController 自身に ICharacterMotorStateSource を持たせないのは、同じ体に別の読み元 (テストや差し替え) を足したとき、
    // ViewRig・PlayerMockBody・CharacterAnimator の TryGetComponent が先に付いた PlayerController を拾い、足した方が読まれなくなるため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerMotorStateSource : MonoBehaviour, ICharacterMotorStateSource
    {
        private PlayerController _mover;

        public PlayerController Mover
        {
            get
            {
                if (_mover == null)
                {
                    _mover = GetComponent<PlayerController>();
                }

                return _mover;
            }
            set => _mover = value;
        }

        public MotorSpec Spec => Mover != null ? Mover.Spec : null;

        public bool TryGetMotorState(out CharacterMotorState state)
        {
            var mover = Mover;
            if (mover == null)
            {
                state = default;
                return false;
            }

            return mover.TryGetMotorState(out state);
        }
    }
}
