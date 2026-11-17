using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ガンゲーム (<see cref="GunGame"/>) の HUD の行 (表のキー <c>mode.gungame.*</c> の <see cref="LocalizedText"/>)。段は 1 から数えて書く。
    /// </summary>
    public sealed class GunGameHudFormatter : IModeHudFormatter
    {
        /// <summary>最後の段の人に出す、次の武器の HUD の行。</summary>
        public static LocalizedText LastTier => new LocalizedText("mode.gungame.lastTier");

        /// <summary>
        /// 今の段と武器の HUD の行。tier は 0 から数えた段で、最後の段を終えた人 (段の数) は最後の段として書く。
        /// weaponName は武器の名前 (<see cref="GunGameTier.Name"/>) で、<c>default</c> なら <c>-</c> を出す。
        /// </summary>
        /// <example><c>Tier(1, 3, new LocalizedText("weapon.launcher"))</c> は <c>段 2/3 Launcher</c></example>
        public static LocalizedText Tier(int tier, int tierCount, LocalizedText weaponName)
        {
            return new LocalizedText("mode.gungame.tier", GunGameText.DisplayedTier(tier, tierCount), tierCount, Name(weaponName));
        }

        /// <summary>次の武器の HUD の行。最後の段なら (nextWeaponName が <c>default</c>) <see cref="LastTier"/>。</summary>
        /// <example><c>Next(new LocalizedText("weapon.handgun"))</c> は <c>次: Handgun</c>、<c>Next(default)</c> は <c>次: なし (この武器で倒せば勝ち)</c></example>
        public static LocalizedText Next(LocalizedText nextWeaponName)
        {
            return nextWeaponName.IsEmpty ? LastTier : new LocalizedText("mode.gungame.next", nextWeaponName);
        }

        public LocalizedText Time(ModeStatus status) => ModeStatusFormat.Remaining(status);

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) => ModeStatusFormat.Scores(status);

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            ModeStatusFormat.AppendRespawn(status, lines);
            if (!(status.Detail is GunGameStatus detail) || !detail.LocalPlayerId.HasValue)
            {
                return;
            }

            var gunGame = detail.GunGame;
            var id = detail.LocalPlayerId.Value;
            var next = gunGame.GetNextWeapon(id);
            lines.Add(Tier(gunGame.GetTier(id), gunGame.Ladder.TierCount, gunGame.GetWeapon(id).Name));
            lines.Add(Next(next.HasValue ? next.Value.Name : default));
        }

        public LocalizedText Result(ModeStatus status) => ModeStatusFormat.Result(status);

        private static object Name(LocalizedText weaponName)
        {
            return weaponName.IsEmpty ? (object)"-" : weaponName;
        }

        // SubsystemRegistration は BeforeSceneLoad より先に走るので、ゲームが BeforeSceneLoad で登録した書式役がこれを置き換えられる
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDefault()
        {
            ModeHudFormatters.Register<GunGameStatus>(new GunGameHudFormatter());
        }
    }
}
