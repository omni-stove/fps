using Fps.Net;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// NetTest を Play している間に、自分のプレイヤーからロードアウトの選択をサーバーへ送るテスト用のメニュー。
    /// 選択の画面 (倒されている間の LoadoutSelectView) は画面の側で合わない並びを送らないので、ルールに合わない並びはここから送り、
    /// サーバーの判定と次のリスポーンの武器を確かめる。
    /// </summary>
    public static class NetLoadoutDebugMenu
    {
        public const string ValidMenuPath = "Fps/Net/Debug/Request Loadout (Rifle + Handgun)";
        public const string InvalidMenuPath = "Fps/Net/Debug/Request Loadout (Handgun x2, rejected)";

        /// <summary>デフォルトロードアウトのルールに合う並び (メインに rifle、サブに handgun)。</summary>
        public static readonly string[] ValidWeaponIds = { WeaponAssetBuilder.RifleId, WeaponAssetBuilder.HandgunId };

        /// <summary>メインのスロットが handgun を許さないので、サーバーが断る並び。</summary>
        public static readonly string[] InvalidWeaponIds = { WeaponAssetBuilder.HandgunId, WeaponAssetBuilder.HandgunId };

        [MenuItem(ValidMenuPath)]
        public static void RequestValid()
        {
            Request(ValidWeaponIds);
        }

        [MenuItem(InvalidMenuPath)]
        public static void RequestInvalid()
        {
            Request(InvalidWeaponIds);
        }

        [MenuItem(ValidMenuPath, true)]
        [MenuItem(InvalidMenuPath, true)]
        public static bool CanRequest()
        {
            return EditorApplication.isPlaying && FindLocalWeapon() != null;
        }

        private static void Request(string[] weaponIds)
        {
            var weapon = FindLocalWeapon();
            if (weapon == null)
            {
                Debug.LogWarning("[Net] NetLoadoutDebugMenu: no local player is spawned; start NetTest as host or client first");
                return;
            }

            weapon.RequestLoadout(weaponIds);
        }

        private static NetPlayerWeapon FindLocalWeapon()
        {
            foreach (var weapon in Object.FindObjectsByType<NetPlayerWeapon>())
            {
                if (weapon.IsSpawned && weapon.IsOwner)
                {
                    return weapon;
                }
            }

            return null;
        }
    }
}
