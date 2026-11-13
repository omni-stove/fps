using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、ロードアウトのアセットを検査して試合の武器にする処理と、出現・リスポーンで武器を配る処理。
    /// </summary>
    public sealed partial class NetTeamDeathmatch
    {
        /// <summary>
        /// 出現・リスポーンで weapon に loadout の武器を全部満タンで渡し直し、先頭 (rifle) を持ち替えの待ち時間なしで構えさせる。
        /// 持ち替え中・リロード中だった状態と、倒れる前の残弾は捨てる。weapon が null なら何もしない。
        /// </summary>
        internal static void ArmForSpawn(ServerLoadout loadout, NetPlayerWeapon weapon)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            // ServerLoadout は開始時に ServerEquip と同じ条件で検査済みなので、ここでは投げない
            if (weapon != null)
            {
                weapon.ServerEquip(loadout.Weapons, loadout.SwitchSeconds);
            }
        }

        /// <summary>
        /// asset のルール (<see cref="LoadoutDefinition.ToRule"/>) と武器 (<see cref="LoadoutDefinition.ToSpecs"/>) を検査し、
        /// 試合で使う <see cref="Fps.Core.ServerLoadout"/> にする。asset が未設定・ルールか武器が不正・ルールに合わない・
        /// <see cref="NetPlayerWeapon.MaxWeaponCount"/> 本より多い・RPC で送れない長さの Id がある、のどれかなら false と理由を返す。
        /// </summary>
        public static bool TryCreateLoadout(LoadoutDefinition asset, out ServerLoadout loadout, out string error)
        {
            loadout = null;

            // 参照切れ (破棄されたアセット) も Unity の == null で拾う
            if (asset == null)
            {
                error = "no loadout is assigned";
                return false;
            }

            LoadoutRule rule;
            IReadOnlyList<WeaponSpec> specs;
            try
            {
                rule = asset.ToRule();
                specs = asset.ToSpecs();
            }
            catch (WeaponDefinitionException e)
            {
                error = e.Message;
                return false;
            }

            if (specs.Count > NetPlayerWeapon.MaxWeaponCount)
            {
                error = "loadout '" + asset.name + "' has " + specs.Count + " weapons but at most "
                    + NetPlayerWeapon.MaxWeaponCount + " can be synced";
                return false;
            }

            foreach (var weapon in specs)
            {
                if (!NetPlayerWeapon.FitsWeaponId(weapon.Id))
                {
                    error = "weapon id '" + weapon.Id + "' in loadout '" + asset.name + "' is longer than "
                        + NetPlayerWeapon.MaxWeaponIdBytes + " bytes and cannot be sent with a shot";
                    return false;
                }
            }

            try
            {
                // ToRule と ToSpecs の間にアセットが書き換わっても、ここで同じルールに合うことを確かめ直す
                loadout = new ServerLoadout(rule, specs, asset.SwitchSeconds);
            }
            catch (ArgumentException e)
            {
                error = "loadout '" + asset.name + "' is invalid: " + e.Message;
                return false;
            }

            error = null;
            return true;
        }
    }
}
