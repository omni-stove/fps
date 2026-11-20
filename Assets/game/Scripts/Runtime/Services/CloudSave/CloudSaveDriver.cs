using UnityEngine;

namespace Shooter.Runtime
{
    // 同期は試合やメニューをまたいで続くので、シーンを替えても残るこの部品で回す。
    [DisallowMultipleComponent]
    public sealed class CloudSaveDriver : MonoBehaviour
    {
        private CloudSaveSync _sync;

        public CloudSaveSync Sync => _sync;

        public void Bind(CloudSaveSync sync)
        {
            _sync = sync;
        }

        private void Update()
        {
            // 前の Play の同期が残っていれば、今の同期と二重に回さない。
            if (_sync == null || !ReferenceEquals(_sync, CloudSaveBootstrap.Current))
            {
                Destroy(gameObject);
                return;
            }

            _sync.Tick(Time.unscaledTimeAsDouble);
        }
    }
}
