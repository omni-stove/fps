using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 設置地点の数はシーンが決めるので持たず、ToSpec に渡す。
    [CreateAssetMenu(fileName = "SearchAndDestroy", menuName = "Shooter/Modes/Search And Destroy")]
    public sealed class SearchAndDestroyDefinition : ScriptableObject
    {
        [SerializeField, Min(1f), Tooltip("1 ラウンドの制限時間 (秒)。設置すると止まる")]
        private float _roundSeconds = SearchAndDestroySpec.DefaultRoundSeconds;

        [SerializeField, Min(0.1f), Tooltip("設置し終えるまで長押しを続ける秒数")]
        private float _plantSeconds = SearchAndDestroySpec.DefaultPlantSeconds;

        [SerializeField, Min(0.1f), Tooltip("解除し終えるまで長押しを続ける秒数")]
        private float _defuseSeconds = SearchAndDestroySpec.DefaultDefuseSeconds;

        [SerializeField, Min(1f), Tooltip("設置してから起爆するまでの秒数")]
        private float _fuseSeconds = SearchAndDestroySpec.DefaultFuseSeconds;

        [SerializeField, Min(1), Tooltip("試合に勝つのに要るラウンドの勝ち数")]
        private int _winsToWin = SearchAndDestroySpec.DefaultWinsToWin;

        [SerializeField, Min(0f), Tooltip("各ラウンドの開始前の待ち (秒)。この間は動けない")]
        private float _preparationSeconds = SearchAndDestroySpec.DefaultPreparationSeconds;

        [SerializeField, Min(0), Tooltip("何ラウンドごとに攻守を入れ替えるか。0 なら入れ替えない")]
        private int _swapEveryRounds = SearchAndDestroySpec.DefaultSwapEveryRounds;

        [SerializeField, Min(0.1f), Tooltip("攻撃側が落ちた爆弾を拾う距離 (m、水平)")]
        private float _bombPickupRadius = SearchAndDestroySpec.DefaultBombPickupRadius;

        [SerializeField, Min(0.1f), Tooltip("防衛側が設置された爆弾を解除できる距離 (m、水平)")]
        private float _defuseRadius = SearchAndDestroySpec.DefaultDefuseRadius;

        [SerializeField, Tooltip("ラウンド制の購入経済。空なら経済なし (お金も購入も無く、持っている武器のまま)")]
        private EconomyDefinition _economy;

        public EconomyDefinition Economy
        {
            get => _economy;
            set => _economy = value;
        }

        public float RoundSeconds
        {
            get => _roundSeconds;
            set => _roundSeconds = value;
        }

        public float PlantSeconds
        {
            get => _plantSeconds;
            set => _plantSeconds = value;
        }

        public float DefuseSeconds
        {
            get => _defuseSeconds;
            set => _defuseSeconds = value;
        }

        public float FuseSeconds
        {
            get => _fuseSeconds;
            set => _fuseSeconds = value;
        }

        public int WinsToWin
        {
            get => _winsToWin;
            set => _winsToWin = value;
        }

        public float PreparationSeconds
        {
            get => _preparationSeconds;
            set => _preparationSeconds = value;
        }

        public int SwapEveryRounds
        {
            get => _swapEveryRounds;
            set => _swapEveryRounds = value;
        }

        public float BombPickupRadius
        {
            get => _bombPickupRadius;
            set => _bombPickupRadius = value;
        }

        public float DefuseRadius
        {
            get => _defuseRadius;
            set => _defuseRadius = value;
        }

        public SearchAndDestroySpec ToSpec(int siteCount)
        {
            try
            {
                return new SearchAndDestroySpec(
                    siteCount, _roundSeconds, _plantSeconds, _defuseSeconds, _fuseSeconds, _winsToWin, _preparationSeconds, _swapEveryRounds,
                    _bombPickupRadius, _defuseRadius);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"search and destroy definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(SearchAndDestroySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _roundSeconds = spec.RoundSeconds;
            _plantSeconds = spec.PlantSeconds;
            _defuseSeconds = spec.DefuseSeconds;
            _fuseSeconds = spec.FuseSeconds;
            _winsToWin = spec.WinsToWin;
            _preparationSeconds = spec.PreparationSeconds;
            _swapEveryRounds = spec.SwapEveryRounds;
            _bombPickupRadius = spec.BombPickupRadius;
            _defuseRadius = spec.DefuseRadius;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed search and destroy definition)" : name;
    }
}
