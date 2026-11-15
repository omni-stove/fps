using UnityEngine;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class StatusEffectReceiver : MonoBehaviour
    {
    }
}
