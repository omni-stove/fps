using System;

namespace Shooter.Core
{
    /// <summary>
    /// ホード (<see cref="Horde"/>) の設定。陣営と通貨の入り方、防衛物を買える時期と費用・操作の届く距離、倒したときの点、
    /// 波の敵の動かし方 (知らせの間隔・出現の最小距離・死体を残す時間)。
    /// </summary>
    public sealed class HordeSpec
    {
        public const int DefaultEnemySideId = 1;
        public const int DefaultStartingCurrency = 50;
        public const int DefaultKillReward = 10;
        public const int DefaultKillRewardPerWave = 2;
        public const int DefaultKillScore = 1;
        public const int DefaultBuildCost = 40;
        public const int DefaultRepairCostPerPiece = 4;
        public const float DefaultInteractRadius = 3.5f;
        public const float DefaultAlertInterval = 3f;
        public const float DefaultMinSpawnDistance = 8f;
        public const float DefaultCorpseSeconds = 5f;

        /// <param name="defenders">守る側 (参加者) の陣営。</param>
        /// <param name="attackers">攻める側 (波の敵) の陣営。defenders と違うこと。</param>
        /// <param name="startingCurrency">始めに持っている通貨。0 以上。</param>
        /// <param name="killReward">1 波目で敵を 1 体倒したときに入る通貨。0 以上。</param>
        /// <param name="killRewardPerWave">波が 1 つ進むごとに、1 体あたりの通貨に足す量。0 以上。</param>
        /// <param name="purchasesOnlyBetweenWaves">true なら防衛物を直す・置くのは波の間 (と最初の波の前) だけ。</param>
        /// <param name="buildCost">まだ置かれていない防衛物を置くときに払う通貨。0 以上。</param>
        /// <param name="repairCostPerPiece">壊れた部品 1 つを直すときに払う通貨。0 以上。</param>
        /// <param name="interactRadius">防衛物からこの水平の距離 (m) 以内で直す・置く操作ができる。0 より大きい有限の値。</param>
        /// <param name="killScore">敵を 1 体倒したときに守る側の得点に足す点。0 以上。</param>
        /// <param name="alertInterval">Idle・Patrol の波の敵をいちばん近い敵対者へ向かわせる間隔 (秒)。0 より大きい有限の値。</param>
        /// <param name="minSpawnDistance">敵対者からこの距離 (m) 以内の出現点は、ほかに使える点があれば使わない。0 以上の有限の値。</param>
        /// <param name="corpseSeconds">倒された敵を消すまでの秒数。0 なら消さない。0 以上の有限の値。</param>
        public HordeSpec(
            Side defenders,
            Side attackers,
            int startingCurrency = DefaultStartingCurrency,
            int killReward = DefaultKillReward,
            int killRewardPerWave = DefaultKillRewardPerWave,
            bool purchasesOnlyBetweenWaves = true,
            int buildCost = DefaultBuildCost,
            int repairCostPerPiece = DefaultRepairCostPerPiece,
            float interactRadius = DefaultInteractRadius,
            int killScore = DefaultKillScore,
            float alertInterval = DefaultAlertInterval,
            float minSpawnDistance = DefaultMinSpawnDistance,
            float corpseSeconds = DefaultCorpseSeconds)
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

            if (buildCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(buildCost), buildCost, "buildCost must not be negative");
            }

            if (repairCostPerPiece < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repairCostPerPiece), repairCostPerPiece, "repairCostPerPiece must not be negative");
            }

            // NaN はどの比較も false なので、否定で書いて NaN も弾く。
            if (!(interactRadius > 0f) || float.IsInfinity(interactRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(interactRadius), interactRadius, "interactRadius must be positive and finite");
            }

            if (killScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killScore), killScore, "killScore must not be negative");
            }

            if (!(alertInterval > 0f) || float.IsInfinity(alertInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(alertInterval), alertInterval, "alertInterval must be positive and finite");
            }

            if (!(minSpawnDistance >= 0f) || float.IsInfinity(minSpawnDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(minSpawnDistance), minSpawnDistance, "minSpawnDistance must be non-negative and finite");
            }

            if (!(corpseSeconds >= 0f) || float.IsInfinity(corpseSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(corpseSeconds), corpseSeconds, "corpseSeconds must be non-negative and finite");
            }

            Defenders = defenders;
            Attackers = attackers;
            StartingCurrency = startingCurrency;
            KillReward = killReward;
            KillRewardPerWave = killRewardPerWave;
            PurchasesOnlyBetweenWaves = purchasesOnlyBetweenWaves;
            BuildCost = buildCost;
            RepairCostPerPiece = repairCostPerPiece;
            InteractRadius = interactRadius;
            KillScore = killScore;
            AlertInterval = alertInterval;
            MinSpawnDistance = minSpawnDistance;
            CorpseSeconds = corpseSeconds;
        }

        public Side Defenders { get; }

        public Side Attackers { get; }

        public int StartingCurrency { get; }

        public int KillReward { get; }

        public int KillRewardPerWave { get; }

        public bool PurchasesOnlyBetweenWaves { get; }

        public int BuildCost { get; }

        public int RepairCostPerPiece { get; }

        public float InteractRadius { get; }

        public int KillScore { get; }

        public float AlertInterval { get; }

        public float MinSpawnDistance { get; }

        public float CorpseSeconds { get; }

        /// <summary>壊れた部品を pieceCount 個直すときに払う通貨。int に収まらなければ <see cref="int.MaxValue"/>。</summary>
        public int RepairCost(int pieceCount)
        {
            if (pieceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceCount), pieceCount, "pieceCount must not be negative");
            }

            return (int)Math.Min(int.MaxValue, (long)RepairCostPerPiece * pieceCount);
        }

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
