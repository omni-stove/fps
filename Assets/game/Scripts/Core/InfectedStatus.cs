using System;

namespace Shooter.Core
{
    // ModeStatus は出した後も UI が持つので、Infected を指さずに出した時点の値を写して持つ。
    public sealed class InfectedStatus
    {
        public InfectedStatus(bool hasBrokenOut, float secondsUntilOutbreak, int survivorCount, int infectedCount, bool? isLocalPlayerInfected)
        {
            if (!(secondsUntilOutbreak >= 0f) || float.IsInfinity(secondsUntilOutbreak))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(secondsUntilOutbreak), secondsUntilOutbreak, "secondsUntilOutbreak must not be negative and must be finite");
            }

            if (survivorCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(survivorCount), survivorCount, "survivorCount must not be negative");
            }

            if (infectedCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(infectedCount), infectedCount, "infectedCount must not be negative");
            }

            HasBrokenOut = hasBrokenOut;
            SecondsUntilOutbreak = hasBrokenOut ? 0f : secondsUntilOutbreak;
            SurvivorCount = survivorCount;
            InfectedCount = infectedCount;
            IsLocalPlayerInfected = isLocalPlayerInfected;
        }

        public bool HasBrokenOut { get; }

        public float SecondsUntilOutbreak { get; }

        public int SurvivorCount { get; }

        public int InfectedCount { get; }

        // 操作しているプレイヤーがいない (bot だけの) ときは null。
        public bool? IsLocalPlayerInfected { get; }

        public static InfectedStatus Of(Infected infected, int? localPlayerId)
        {
            if (infected == null)
            {
                throw new ArgumentNullException(nameof(infected));
            }

            bool? local = null;
            if (localPlayerId.HasValue && infected.Session.HasPlayer(localPlayerId.Value))
            {
                local = infected.IsInfected(localPlayerId.Value);
            }

            return new InfectedStatus(
                infected.HasBrokenOut, infected.SecondsUntilOutbreak, infected.SurvivorCount, infected.InfectedCount, local);
        }
    }
}
