using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 射撃場で拾える武器 (WeaponAssetBuilder.LoadRangeWeapons、サンプルの武器の JSON すべて) を、標的の奥 (x = -1.25..18.75, z = 28.5..37.5) に
    // 一覧の順で左から右・手前から奥へ並べる。サンプルの武器を足す issue は JSON を置くだけで、ここに拾える武器が 1 つ増える。
    // 武器のモデルに照準を合わせて拾う (Look) ので、隣の武器と範囲が重なっても狙った 1 本だけを拾える。
    public sealed class RangeWeaponRackFixture : ITpsRangeFixture
    {
        public const string RootName = "WeaponRack";
        public const string PickupNamePrefix = "WeaponPickup-";
        public const int FixtureOrder = 570;

        public const int Columns = 8;
        public const int Rows = 3;
        public const float ColumnSpacing = 2.5f;
        public const float RowSpacing = 3f;
        public const float RespawnSeconds = 5f;

        /// <summary>拾える範囲の半径 (m)。武器の前に立って見下ろせば届く広さ。</summary>
        public const float PickupRadius = 1.5f;

        /// <summary>手前の左端の武器の足元。</summary>
        public static readonly Vector3 FirstSlot = new Vector3(0f, 0f, 30f);

        public static readonly Vector3 Size = new Vector3((Columns - 1) * ColumnSpacing + 2.5f, 2f, (Rows - 1) * RowSpacing + 3f);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(
            FirstSlot + new Vector3((Columns - 1) * ColumnSpacing / 2f, Size.y / 2f, (Rows - 1) * RowSpacing / 2f), Size);

        /// <summary>一覧の index 番目の武器を置く足元。</summary>
        public static Vector3 SlotPosition(int index)
        {
            if (index < 0 || index >= Columns * Rows)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, $"the weapon rack holds {(Columns * Rows).ToString(CultureInfo.InvariantCulture)} weapons");
            }

            return FirstSlot + new Vector3((index % Columns) * ColumnSpacing, 0f, (index / Columns) * RowSpacing);
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            return Place(scene, WeaponAssetBuilder.LoadRangeWeapons().Weapons);
        }

        /// <summary>
        /// weapons を並べた <see cref="RootName"/> を scene の最上位に置く。どれも範囲 (<see cref="PickupRadius"/>) で武器のモデルに照準を合わせて
        /// Interact を押すと拾い、<see cref="RespawnSeconds"/> 後にまた出る。既に持っている武器なら、その武器の弾倉 1 つ分の予備弾になる。
        /// 持てる武器の数の上限 (<see cref="GameRules.MaxWeapons"/>) に届いていれば、今の武器を足元に落として持ち替える。
        /// </summary>
        /// <exception cref="InvalidOperationException">weapons が棚 (<see cref="Columns"/> × <see cref="Rows"/>) に収まらない。何も置かない。</exception>
        public static GameObject Place(Scene scene, IReadOnlyList<WeaponDefinition> weapons)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (weapons.Count > Columns * Rows)
            {
                throw new InvalidOperationException(
                    $"{RootName} holds {(Columns * Rows).ToString(CultureInfo.InvariantCulture)} weapons, but the range lists "
                    + $"{weapons.Count.ToString(CultureInfo.InvariantCulture)}; add a row to {nameof(RangeWeaponRackFixture)}");
            }

            // 中身を先に作って検査し、不正なら何も置かない。
            var contents = new PickupContents[weapons.Count];
            for (var i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weapon == null)
                {
                    throw new ArgumentException($"range weapon #{i.ToString(CultureInfo.InvariantCulture)} is empty", nameof(weapons));
                }

                contents[i] = PickupContents.ForWeapon(weapon, weapon.ToSpec().MagazineSize);
            }

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            for (var i = 0; i < weapons.Count; i++)
            {
                CreatePickup(root.transform, weapons[i].name, contents[i], SlotPosition(i));
            }

            return root;
        }

        public static PickupSpawnPoint CreatePickup(Transform parent, string weaponName, PickupContents contents, Vector3 position)
        {
            var pointObject = new GameObject(PickupNamePrefix + weaponName);
            pointObject.transform.SetParent(parent, false);
            pointObject.transform.position = position;
            var point = pointObject.AddComponent<PickupSpawnPoint>();
            point.Contents = contents;
            point.CollectMode = PickupCollectMode.Look;
            point.Radius = PickupRadius;
            point.RespawnSeconds = RespawnSeconds;
            return point;
        }
    }
}
