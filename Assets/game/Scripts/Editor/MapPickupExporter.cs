using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    // サーバー (ServerPickups) はシーンの PickupSpawnPoint と AmmoCrate を読めないので、中身・位置・範囲・秒数を書き出す。
    // 調整値 (PickupDefinition) を入れた物は、オフラインの PickupSpawnPoint・AmmoCrate と同じくその値を使う
    public static class MapPickupExporter
    {
        public static NetMapPickups Collect(IReadOnlyList<GameObject> roots)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            var pickups = new List<NetMapPickup>();
            var crates = new List<NetMapAmmoCrate>();
            var pickupIds = new HashSet<string>(StringComparer.Ordinal);
            var crateIds = new HashSet<string>(StringComparer.Ordinal);
            var invalid = new List<string>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var point in root.GetComponentsInChildren<PickupSpawnPoint>(false))
                {
                    try
                    {
                        pickups.Add(ToMapPickup(point, UniqueId(point.transform, pickupIds)));
                    }
                    catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                    {
                        invalid.Add(PathOf(point.transform) + " (" + e.Message + ")");
                    }
                }

                foreach (var crate in root.GetComponentsInChildren<AmmoCrate>(false))
                {
                    try
                    {
                        if (crate.Definition != null)
                        {
                            crate.Definition.ValidateAmmoCrate();
                        }

                        crates.Add(new NetMapAmmoCrate(
                            UniqueId(crate.transform, crateIds), crate.transform.position.ToNumerics(), crate.EffectiveRadius, crate.EffectiveCooldownSeconds));
                    }
                    catch (ArgumentException e)
                    {
                        invalid.Add(PathOf(crate.transform) + " (" + e.Message + ")");
                    }
                }
            }

            // 書き出さないと、オフラインでだけ拾える物になる
            if (invalid.Count > 0)
            {
                throw new InvalidOperationException(MapCollisionExporter.LogTag + " these pickups cannot be exported: " + string.Join(", ", invalid));
            }

            return new NetMapPickups(pickups, crates);
        }

        public static NetMapPickup ToMapPickup(PickupSpawnPoint point, string id)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            var definition = point.Definition;
            if (definition == null && point.Contents == null)
            {
                throw new InvalidOperationException("the spawn point has no contents");
            }

            if (definition != null)
            {
                definition.Validate();
            }

            var spec = definition != null ? definition.ToSpec() : point.Contents.ToSpec();
            var collectMode = definition != null ? definition.CollectMode : point.CollectMode;
            var radius = definition != null ? definition.Radius : point.Radius;
            var respawn = definition != null ? definition.RespawnSeconds : point.RespawnSeconds;

            // 持ち物に入るかを見る武器の重さはサーバーが持ち物の表から足すので、書き出さない
            if (spec.Kind == PickupKind.Weapon)
            {
                spec = PickupSpec.Weapon(spec.WeaponId, spec.Amount, spec.Eligibility);
            }

            return new NetMapPickup(id, spec, point.transform.position.ToNumerics(), radius, respawn, collectMode == PickupCollectMode.Touch);
        }

        // Id はサーバーのログで元の物を探せるよう階層の道筋にする。同じ道筋の物が並べば番号を足して分ける
        private static string UniqueId(Transform transform, HashSet<string> seen)
        {
            var path = PathOf(transform);
            var id = path;
            for (var n = 2; !seen.Add(id); n++)
            {
                id = path + "#" + n;
            }

            return id;
        }

        private static string PathOf(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }
    }
}
