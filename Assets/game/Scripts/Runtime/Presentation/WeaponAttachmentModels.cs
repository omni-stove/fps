using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class WeaponAttachmentModels
    {
        public const string SocketPrefix = "Socket_";

        public const string RootPrefix = "Attachment_";

        public static void Apply(Transform model, IReadOnlyList<string> attachments, ItemCatalog items, AttachmentModelCatalog models)
        {
            if (model == null)
            {
                return;
            }

            Remove(model);
            if (attachments == null || attachments.Count == 0 || items == null)
            {
                return;
            }

            var bounds = LocalBounds(model);
            foreach (var itemId in attachments)
            {
                if (!items.TryFindAttachment(itemId, out var item))
                {
                    continue;
                }

                AttachmentModelEntry entry = null;
                if (models != null)
                {
                    models.TryGet(itemId, out entry);
                }

                var anchor = entry != null ? entry.Anchor : AttachmentAnchor.Top;
                var slot = item.Attachment.Slot;
                var socket = FindOwnSocket(model, SocketPrefix + slot);
                var root = new GameObject(RootPrefix + slot).transform;
                root.SetParent(socket != null ? socket : model, false);
                if (socket == null)
                {
                    root.localPosition = AnchorPoint(bounds, anchor);
                }

                root.gameObject.AddComponent<WeaponAttachmentModel>().ItemId = itemId;
                if (entry != null && entry.Prefab != null)
                {
                    VisualPrefab.Instantiate(entry.Prefab, root, entry.Prefab.name, stripColliders: true);
                }
                else
                {
                    BuildPlaceholder(root, anchor, entry != null ? entry.PlaceholderSize : AttachmentModelEntry.DefaultPlaceholderSize);
                }

                VisualPrefab.SetLayer(root.gameObject, model.gameObject.layer);
            }
        }

        public static void Remove(Transform model)
        {
            if (model == null)
            {
                return;
            }

            foreach (var attached in model.GetComponentsInChildren<WeaponAttachmentModel>(true))
            {
                var go = attached.gameObject;

                // Destroy はフレームの終わりまで残り、同じフレームに探すソケットに見つかるので、先に外して隠す
                go.transform.SetParent(null, false);
                go.SetActive(false);
                if (Application.isPlaying)
                {
                    Object.Destroy(go);
                }
                else
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        public static Vector3 AnchorPoint(Bounds bounds, AttachmentAnchor anchor)
        {
            var center = bounds.center;
            switch (anchor)
            {
                case AttachmentAnchor.Top:
                    return new Vector3(center.x, bounds.max.y, center.z);
                case AttachmentAnchor.Front:
                    return new Vector3(center.x, center.y, bounds.max.z);
                case AttachmentAnchor.Bottom:
                    return new Vector3(center.x, bounds.min.y, center.z + (bounds.extents.z * 0.4f));
                default:
                    return center;
            }
        }

        // 付けた物の下は見ない。付け直すときに前の付けた物のソケットへ付けないため
        private static Transform FindOwnSocket(Transform root, string name)
        {
            if (root.GetComponent<WeaponAttachmentModel>() != null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindOwnSocket(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // 付けた物を除いた描画物の外形を、モデルの根の向きと大きさで測る
        private static Bounds LocalBounds(Transform model)
        {
            var found = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<WeaponAttachmentModel>(true) != null)
                {
                    continue;
                }

                var world = renderer.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = model.InverseTransformPoint(new Vector3(
                        (corner & 1) == 0 ? world.min.x : world.max.x,
                        (corner & 2) == 0 ? world.min.y : world.max.y,
                        (corner & 4) == 0 ? world.min.z : world.max.z));
                    if (!found)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return bounds;
        }

        private static void BuildPlaceholder(Transform root, AttachmentAnchor anchor, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "AttachmentBox";

            // Destroy は次のフレームまで残り、その間に射撃の判定に当たるので、すぐ消す
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(root, false);
            box.transform.localScale = size;
            box.transform.localPosition = Vector3.Scale(Outward(anchor) * 0.5f, size);
            if (anchor == AttachmentAnchor.Front)
            {
                AddSocket(root, WeaponSockets.MuzzleName, new Vector3(0f, 0f, size.z));
            }
            else if (anchor == AttachmentAnchor.Top)
            {
                AddSocket(root, WeaponSockets.SightName, new Vector3(0f, size.y, -size.z * 0.5f));
            }
        }

        private static Vector3 Outward(AttachmentAnchor anchor)
        {
            switch (anchor)
            {
                case AttachmentAnchor.Top:
                    return Vector3.up;
                case AttachmentAnchor.Front:
                    return Vector3.forward;
                case AttachmentAnchor.Bottom:
                    return Vector3.down;
                default:
                    return Vector3.zero;
            }
        }

        private static void AddSocket(Transform parent, string name, Vector3 position)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            socket.localPosition = position;
        }
    }
}
