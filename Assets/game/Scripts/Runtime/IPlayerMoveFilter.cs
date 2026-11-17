using UnityEngine;

namespace Shooter.Runtime
{
    public interface IPlayerMoveFilter
    {
        void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed);
    }
}
