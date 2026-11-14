using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="ShellEjector"/> が薬莢のインスタンスに付け、最初の衝突を <see cref="ShellEjector.Land"/> に知らせる。
    /// 衝突は Rigidbody のあるオブジェクトにしか届かないので、薬莢の Rigidbody と同じオブジェクトに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShellCasing : MonoBehaviour
    {
        private ShellEjector _owner;
        private PooledShell _shell;

        /// <summary>この薬莢を出した ShellEjector。結び付ける前は null。</summary>
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
