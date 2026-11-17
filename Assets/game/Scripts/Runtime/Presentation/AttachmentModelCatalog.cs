using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "AttachmentModelCatalog", menuName = "Shooter/Attachment Model Catalog")]
    public sealed class AttachmentModelCatalog : ScriptableObject
    {
        public const string CatalogName = "AttachmentModel";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("付ける物ごとの見た目。Id は重複させない")]
        private List<AttachmentModelEntry> _attachments = new List<AttachmentModelEntry>();

        public IReadOnlyList<AttachmentModelEntry> Attachments => _attachments ??= new List<AttachmentModelEntry>();

        public static AttachmentModelCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.AttachmentModels);
            return fromContent != null ? fromContent : Resources.Load<AttachmentModelCatalog>(ResourcePath);
        }

        public void SetAttachments(IEnumerable<AttachmentModelEntry> attachments)
        {
            _attachments = attachments == null ? new List<AttachmentModelEntry>() : new List<AttachmentModelEntry>(attachments);
        }

        public bool TryGet(string itemId, out AttachmentModelEntry entry)
        {
            foreach (var candidate in Attachments)
            {
                if (candidate != null && string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }

    public enum AttachmentAnchor
    {
        Top,

        Front,

        Bottom,

        Center,
    }

    [Serializable]
    public sealed class AttachmentModelEntry
    {
        public static readonly Vector3 DefaultPlaceholderSize = new Vector3(0.04f, 0.04f, 0.1f);

        [SerializeField, Tooltip("持ち物のアイテムの Id (Tuning/inventory.json の items の id)")]
        private string _itemId;

        [SerializeField, Tooltip("見た目。原点を取り付ける点に、+Z を銃口の向きにする。空ならコードで箱を作る")]
        private GameObject _prefab;

        [SerializeField, Tooltip("武器のモデルに取り付け口のソケットが無いときに置く位置")]
        private AttachmentAnchor _anchor = AttachmentAnchor.Top;

        [SerializeField, Tooltip("プレハブが空のときにコードで作る箱の大きさ (m)")]
        private Vector3 _placeholderSize = DefaultPlaceholderSize;

        public AttachmentModelEntry(string itemId, GameObject prefab, AttachmentAnchor anchor, Vector3 placeholderSize)
        {
            _itemId = itemId;
            _prefab = prefab;
            _anchor = anchor;
            _placeholderSize = placeholderSize;
        }

        public string ItemId => _itemId;

        public GameObject Prefab => _prefab;

        public AttachmentAnchor Anchor => _anchor;

        public Vector3 PlaceholderSize => _placeholderSize;
    }
}
