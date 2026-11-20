using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class Pickup
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Transform _visual;
        private BoxCollider _lookTarget;

        private void EnsureParts()
        {
            var trigger = GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = gameObject.AddComponent<SphereCollider>();
            }

            trigger.isTrigger = true;
            ApplyRadius();

            var body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;

            _visual = transform.Find(VisualName);
            if (_visual == null && _contents != null)
            {
                _visual = CreateVisual(_contents.Kind);
            }

            var lookTarget = _visual != null ? _visual.Find(LookTargetName) : null;
            _lookTarget = lookTarget != null ? lookTarget.GetComponent<BoxCollider>() : null;

            ApplyVisualPose();
        }

        // Look は寝かせて照準の判定を付け、それ以外は浮かせて回す。拾い方は作った後にも変えられるので、変えるたびに置き直す。
        private void ApplyVisualPose()
        {
            if (_visual == null)
            {
                return;
            }

            if (_collectMode != PickupCollectMode.Look)
            {
                _visual.localRotation = Quaternion.identity;
                _visual.localPosition = new Vector3(0f, VisualHeight, 0f);
                RemoveLookTarget();
                return;
            }

            _visual.localRotation = Quaternion.Euler(0f, 0f, LyingRollDegrees);
            _visual.localPosition = Vector3.zero;
            var hasBounds = TryLocalBounds(_visual, out var bounds);
            if (hasBounds)
            {
                var lowest = float.PositiveInfinity;
                foreach (var corner in Corners(bounds))
                {
                    lowest = Mathf.Min(lowest, _visual.TransformPoint(corner).y);
                }

                _visual.position += Vector3.up * (transform.position.y + LyingGap - lowest);
            }
            else
            {
                bounds = new Bounds(Vector3.zero, Vector3.one * VisualSize);
                _visual.position += Vector3.up * (VisualSize / 2f + LyingGap);
            }

            EnsureLookTarget(bounds);
        }

        private void EnsureLookTarget(Bounds bounds)
        {
            if (_lookTarget == null)
            {
                var target = new GameObject(LookTargetName);
                target.transform.SetParent(_visual, false);

                // 親の Rigidbody にまとまると、この判定への出入りが範囲の球の出入りとして Pickup に届くので、別の体にする。
                var body = target.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                // 照準の線にだけ当てる。トリガーなので移動と射撃は遮らない。
                _lookTarget = target.AddComponent<BoxCollider>();
                _lookTarget.isTrigger = true;
            }

            _lookTarget.center = bounds.center;
            _lookTarget.size = bounds.size;
        }

        private void RemoveLookTarget()
        {
            if (_lookTarget == null)
            {
                return;
            }

            DestroySafely(_lookTarget.gameObject);
            _lookTarget = null;
        }

        private static bool TryLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var corner in Corners(renderer.localBounds))
                {
                    var point = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
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

            return found;
        }

        private static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            for (var i = 0; i < 8; i++)
            {
                yield return new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            }
        }

        private void ApplyRadius()
        {
            var trigger = GetComponent<SphereCollider>();
            if (trigger == null)
            {
                return;
            }

            trigger.radius = _radius;

            // 足元から球が立つように、中心を半径だけ上げる。
            trigger.center = new Vector3(0f, _radius, 0f);
        }

        private Transform CreateVisual(PickupKind kind)
        {
            var prefab = _visualPrefab != null ? _visualPrefab : WeaponModelOf(_contents);
            if (prefab != null)
            {
                // 拾う範囲はトリガーの球で決めるので、見た目の当たり判定は外す (残すと固い物として移動と弾を遮る)。
                var instance = Shooter.Runtime.VisualPrefab.Instantiate(prefab, transform, VisualName, stripColliders: true);
                instance.transform.localPosition = new Vector3(0f, VisualHeight, 0f);
                AttachVisuals(instance.transform);
                return instance.transform;
            }

            if (kind == PickupKind.Weapon)
            {
                var gun = CreateGunShape();
                AttachVisuals(gun);
                return gun;
            }

            var visual = CreatePart(PrimitiveType.Cube, VisualName, transform, Vector3.one * VisualSize, ColorOf(kind));
            visual.localPosition = new Vector3(0f, VisualHeight, 0f);
            return visual;
        }

        private void AttachVisuals(Transform visual)
        {
            if (_contents != null && _contents.Attachments.Count > 0)
            {
                WeaponAttachmentModels.Apply(visual, _contents.Attachments, KitTuning.Items, AttachmentModelCatalog.Load());
            }
        }

        public static GameObject WeaponModelOf(PickupContents contents)
        {
            if (contents == null || contents.Kind != PickupKind.Weapon || contents.Weapon == null)
            {
                return null;
            }

            var catalog = WeaponExtrasCatalog.Load();
            return catalog != null && catalog.TryGetWeapon(contents.Weapon.Id, out var assets) ? assets.PickupModel : null;
        }

        private Transform CreateGunShape()
        {
            var root = new GameObject(VisualName).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, VisualHeight, 0f);
            var color = PlayerMockBody.GunColor;
            CreatePart(PrimitiveType.Cube, "Body", root, GunBodySize, color);
            var barrel = CreatePart(PrimitiveType.Cube, "Barrel", root, GunBarrelSize, color);
            barrel.localPosition = new Vector3(0f, GunBodySize.y / 4f, (GunBodySize.z + GunBarrelSize.z) / 2f);
            var grip = CreatePart(PrimitiveType.Cube, "Grip", root, GunGripSize, color);
            grip.localPosition = new Vector3(0f, -(GunBodySize.y + GunGripSize.y) / 2f, -GunBodySize.z / 6f);
            return root;
        }

        private static Transform CreatePart(PrimitiveType shape, string partName, Transform parent, Vector3 size, Color color)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;

            // Destroy は次のフレームまで残り、その間に物理に固い箱として現れるので、すぐに外す。
            DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localScale = size;

            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 共有マテリアルを塗り替えると他のプリミティブまで色が変わるので、この物だけの上書きで色を付ける。
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
            return part.transform;
        }

        public static Color ColorOf(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Weapon:
                    return new Color(0.75f, 0.75f, 0.8f);
                case PickupKind.ReserveAmmo:
                    return new Color(0.95f, 0.75f, 0.2f);
                case PickupKind.Health:
                    return new Color(0.3f, 0.9f, 0.4f);
                case PickupKind.Armor:
                    return new Color(0.3f, 0.6f, 1f);
                case PickupKind.Item:
                    return new Color(0.7f, 0.45f, 0.95f);
                default:
                    return new Color(0.95f, 0.3f, 0.3f);
            }
        }
    }
}
