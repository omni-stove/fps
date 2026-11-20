using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class GunGameHudFormatter : IModeHudFormatter
    {
        public static LocalizedText LastTier => new LocalizedText("mode.gungame.lastTier");

        public static LocalizedText Tier(int tier, int tierCount, LocalizedText weaponName)
        {
            return new LocalizedText("mode.gungame.tier", GunGameDisplayedTier.Of(tier, tierCount), tierCount, Name(weaponName));
        }

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
            lines.Add(Tier(gunGame.GetTier(id), gunGame.Ladder.TierCount, DisplayNames.Of(gunGame.GetWeapon(id))));
            lines.Add(Next(next.HasValue ? DisplayNames.Of(next.Value) : default));
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
