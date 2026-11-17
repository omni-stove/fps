using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class WeaponAttachmentModel : MonoBehaviour
    {
        [SerializeField]
        private string _itemId;

        public string ItemId
        {
            get => _itemId;
            set => _itemId = value;
        }
    }
}
