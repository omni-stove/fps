using System;

namespace Fps.Core
{
    /// <summary>ホード (<see cref="Horde"/>) の設定。陣営と通貨の入り方、防衛物を買える時期。</summary>
    public sealed class HordeSpec
    {
        public const int DefaultStartingCurrency = 50;
        public const int DefaultKillReward = 10;
        public const int DefaultKillRewardPerWave = 2;

        /// <param name="defenders">守る側 (参加者) の陣営。</param>
        /// <param name="attackers">攻める側 (波の敵) の陣営。defenders と違うこと。</param>
        /// <param name="startingCurrency">始めに持っている通貨。0 以上。</param>
        /// <param name="killReward">1 波目で敵を 1 体倒したときに入る通貨。0 以上。</param>
        /// <param name="killRewardPerWave">波が 1 つ進むごとに、1 体あたりの通貨に足す量。0 以上。</param>
        /// <param name="purchasesOnlyBetweenWaves">true なら防衛物を直す・置くのは波の間 (と最初の波の前) だけ。</param>
        public HordeSpec(
            Side defenders,
            Side attackers,
            int startingCurrency = DefaultStartingCurrency,
            int killReward = DefaultKillReward,
            int killRewardPerWave = DefaultKillRewardPerWave,
            bool purchasesOnlyBetweenWaves = true)
        {
            if (defenders == attackers)
            {
                throw new ArgumentException($"defenders and attackers must differ, but both are {defenders}", nameof(attackers));
            }

            if (startingCurrency < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingCurrency), startingCurrency, "startingCurrency must not be negative");
            }

            if (killReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killReward), killReward, "killReward must not be negative");
            }

            if (killRewardPerWave < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killRewardPerWave), killRewardPerWave, "killRewardPerWave must not be negative");
            }

            Defenders = defenders;
            Attackers = attackers;
            StartingCurrency = startingCurrency;
            KillReward = killReward;
            KillRewardPerWave = killRewardPerWave;
            PurchasesOnlyBetweenWaves = purchasesOnlyBetweenWaves;
        }

        public Side Defenders { get; }

        public Side Attackers { get; }

        public int StartingCurrency { get; }

        public int KillReward { get; }

        public int KillRewardPerWave { get; }

        public bool PurchasesOnlyBetweenWaves { get; }

        /// <summary>
        /// waveNumber 番目 (1 から) の波で敵を 1 体倒したときに入る通貨。<see cref="KillReward"/> に、
        /// 2 波目から <see cref="KillRewardPerWave"/> ずつ足す。int に収まらなければ <see cref="int.MaxValue"/>。
        /// </summary>
        public int KillRewardAt(int waveNumber)
        {
            if (waveNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(waveNumber), waveNumber, "waveNumber must be at least 1");
            }

            var reward = KillReward + ((long)KillRewardPerWave * (waveNumber - 1));
            return reward >= int.MaxValue ? int.MaxValue : (int)reward;
        }
    }
}
