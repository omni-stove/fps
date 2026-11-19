using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class StaminaText
    {
        public const string ReadingKey = "stamina.reading";

        public const string DepletedKey = "stamina.depleted";

        public static float FillPercent(StaminaStatus status)
        {
            return Math.Clamp(status.Fraction, 0f, 1f) * 100f;
        }

        public static int RoundedPercent(StaminaStatus status)
        {
            return (int)Math.Round(FillPercent(status), MidpointRounding.AwayFromZero);
        }

        // 量が 0 より上でも尽きた扱いは続くので、文の分け方は量からではなく Depleted で決める。
        public static LocalizedText Reading(StaminaStatus status)
        {
            return new LocalizedText(status.Depleted ? DepletedKey : ReadingKey, RoundedPercent(status));
        }

        // 尽きた扱いの間は満タンに戻っても走れないことを伝えたいので、満タンで隠すのは尽きていないときだけにする。
        public static bool IsShown(StaminaStatus? status)
        {
            return status.HasValue && (status.Value.Depleted || status.Value.Current < status.Value.Max);
        }
    }
}
