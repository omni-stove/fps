using UnityEngine;

namespace Shooter.Runtime
{
    // 衝突は Rigidbody のあるオブジェクトにしか届かないので、薬莢の Rigidbody と同じオブジェクトに付ける。
    [DisallowMultipleComponent]
    public sealed class ShellCasing : MonoBehaviour
    {
        private ShellEjector _owner;
        private PooledShell _shell;

        public ShellEjector Owner => _owner;

        internal void Bind(ShellEjector owner, PooledShell shell)
        {
            _owner = owner;
            _shell = shell;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_owner != null && _shell != null)
            {
                _owner.Land(_shell);
            }
        }
    }
}
