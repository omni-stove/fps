using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、ロードアウトのアセットと武器の一覧を検査して試合の武器にする処理、
    /// プレイヤーが選んだ並びを控える処理と、出現・リスポーンで武器を配る処理。
    /// </summary>
    public sealed partial class NetTeamDeathmatch
    {
        /// <summary>
        /// 出現・リスポーンで weapon に loadout の既定の武器を全部満タンで渡し直し、先頭 (rifle) を持ち替えの待ち時間なしで構えさせる。
        /// 持ち替え中・リロード中だった状態と、倒れる前の残弾は捨てる。予備弾は rules の倍率を掛けた数で、
        /// <see cref="GameRules.InfiniteReserve"/> なら減らない。weapon が null なら何もしない。
        /// </summary>
        internal static void ArmForSpawn(ServerLoadout loadout, GameRules rules, NetPlayerWeapon weapon)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            Arm(loadout.Weapons, loadout.SwitchSeconds, rules, weapon);
        }

        /// <summary>
        /// <see cref="ArmForSpawn(ServerLoadout, GameRules, NetPlayerWeapon)"/> と同じだが、playerId が選んだ並び
        /// (<see cref="ServerLoadout.WeaponsFor"/>) があればそれを渡す。
        /// </summary>
        internal static void ArmForSpawn(ServerLoadout loadout, GameRules rules, NetPlayerWeapon weapon, int playerId)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            Arm(loadout.WeaponsFor(playerId), loadout.SwitchSeconds, rules, weapon);
        }

        private static void Arm(IReadOnlyList<WeaponSpec> weapons, float switchSeconds, GameRules rules, NetPlayerWeapon weapon)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            // 既定の並びも選んだ並びも、TryCreateLoadout で ServerEquip と同じ条件 (本数・Id の長さ) を検査済みなので、ここでは投げない
            if (weapon != null)
            {
                weapon.ServerEquip(weapons, switchSeconds, rules);
            }
        }

        /// <summary>
        /// サーバーで、player が選んだ weaponIds (スロット順の武器の Id) を武器の一覧と既定のロードアウトのルールで検査し
        /// (<see cref="ServerLoadout.TryChoose"/>)、通ればその人の次の出現から配る並びとして控えて true。
        /// 試合が始まっていない・player が試合に入っていない・ルールに合わないなら理由を reason に入れて false (前に控えた並びはそのまま)。
        /// 控えた並びは次の試合でも残り、退出で消える。今持っている武器は変えない。
        /// </summary>
        /// <exception cref="InvalidOperationException">サーバー以外で呼んだ。</exception>
        internal bool ServerChooseLoadout(NetPlayer player, IReadOnlyList<string> weaponIds, out string reason)
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("loadouts can only be chosen on the server");
            }

            if (_serverLoadout == null)
            {
                reason = "the match is not running on the server";
                return false;
            }

            if (player == null || !IsJoined(player))
            {
                reason = "the player has not joined the match yet";
                return false;
            }

            return _serverLoadout.TryChoose(player.ServerPlayerId, weaponIds, out reason);
        }

        /// <summary>
        /// サーバーで、player に次の出現から配る武器 (スロット順)。選んでいなければ既定の並び。
        /// 試合が始まっていない・player が試合に入っていないなら null。
        /// </summary>
        internal IReadOnlyList<WeaponSpec> ServerNextWeapons(NetPlayer player)
        {
            if (_serverLoadout == null || player == null || !IsJoined(player))
            {
                return null;
            }

            return _serverLoadout.WeaponsFor(player.ServerPlayerId);
        }

        /// <summary>
        /// <see cref="TryCreateLoadout(LoadoutDefinition, WeaponCatalogAsset, out ServerLoadout, out string)"/> を武器の一覧なし
        /// (プレイヤーは選べない) で呼ぶ。
        /// </summary>
        public static bool TryCreateLoadout(LoadoutDefinition asset, out ServerLoadout loadout, out string error)
        {
            return TryCreateLoadout(asset, null, out loadout, out error);
        }

        /// <summary>
        /// asset のルール (<see cref="LoadoutDefinition.ToRule"/>) と武器 (<see cref="LoadoutDefinition.ToSpecs"/>)、
        /// プレイヤーが選べる武器の一覧 catalog (<see cref="WeaponCatalogAsset.ToCatalog"/>、null なら選べない) を検査し、
        /// 試合で使う <see cref="Fps.Core.ServerLoadout"/> にする。asset が未設定・ルールか武器か一覧が不正・ルールに合わない・
        /// <see cref="NetPlayerWeapon.MaxWeaponCount"/> 本より多い・RPC で送れない長さの Id がある、のどれかなら false と理由を返す。
        /// </summary>
        public static bool TryCreateLoadout(LoadoutDefinition asset, WeaponCatalogAsset catalog, out ServerLoadout loadout, out string error)
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

            WeaponCatalog choices = null;
            if (catalog != null)
            {
                try
                {
                    choices = catalog.ToCatalog();
                }
                catch (WeaponDefinitionException e)
                {
                    error = e.Message;
                    return false;
                }

                foreach (var weapon in choices.All)
                {
                    if (!NetPlayerWeapon.FitsWeaponId(weapon.Id))
                    {
                        error = "weapon id '" + weapon.Id + "' in weapon catalog '" + catalog.name + "' is longer than "
                            + NetPlayerWeapon.MaxWeaponIdBytes + " bytes and cannot be sent with a shot";
                        return false;
                    }
                }
            }

            try
            {
                // ToRule と ToSpecs の間にアセットが書き換わっても、ここで同じルールに合うことを確かめ直す
                loadout = new ServerLoadout(rule, specs, choices, asset.SwitchSeconds);
            }
            catch (ArgumentException e)
            {
                error = "loadout '" + asset.name + "' is invalid: " + e.Message;
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// クライアントで、サーバーが持たせた weaponId の武器の値を、試合の既定のロードアウト loadout、なければ武器の一覧 catalog から引く。
        /// 引けたら true。どちらにも無い・どちらも未設定・アセットが不正なら false と、"cannot fire " に続けて読める理由を返す。
        /// </summary>
        internal static bool TryFindWeaponSpec(
            LoadoutDefinition loadout, WeaponCatalogAsset catalog, string weaponId, out WeaponSpec spec, out string error)
        {
            spec = null;
            if (loadout == null && catalog == null)
            {
                error = "without the match's loadout";
                return false;
            }

            string loadoutError = null;
            if (loadout != null)
            {
                try
                {
                    foreach (var candidate in loadout.ToSpecs())
                    {
                        if (string.Equals(candidate.Id, weaponId, StringComparison.Ordinal))
                        {
                            spec = candidate;
                            error = null;
                            return true;
                        }
                    }
                }
                catch (WeaponDefinitionException e)
                {
                    // 一覧の側にあれば撃てるので、ここでは諦めない
                    loadoutError = "with an invalid loadout: " + e.Message;
                }
            }

            if (catalog != null)
            {
                try
                {
                    if (catalog.ToCatalog().TryGet(weaponId, out spec))
                    {
                        error = null;
                        return true;
                    }
                }
                catch (WeaponDefinitionException e)
                {
                    error = "with an invalid weapon catalog: " + e.Message;
                    return false;
                }
            }

            error = loadoutError ?? "with weapon '" + weaponId + "' missing from "
                + (loadout != null ? "'" + loadout.name + "'" : "the match's loadout")
                + (catalog != null ? " and '" + catalog.name + "'" : string.Empty);
            return false;
        }
    }
}
