using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroy"/>) の調整値をアセットとして持つ。<see cref="ToSpec"/> で <see cref="SearchAndDestroySpec"/> に変換する。
    /// 設置地点の数はシーンが決めるので持たない。新しく作ったときの値は <see cref="SearchAndDestroySpec"/> の既定値と同じ。
    /// <see cref="SearchAndDestroyRunner.Definition"/> に配線すると、Runner は Inspector の値の代わりにこれを読む。
    /// </summary>
    [CreateAssetMenu(fileName = "SearchAndDestroy", menuName = "Fps/Modes/Search And Destroy")]
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

        /// <summary>地点が siteCount 個の <see cref="SearchAndDestroySpec"/> を作る。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="InvalidOperationException">値か siteCount が不正なとき。メッセージにアセット名を含む。</exception>
        public SearchAndDestroySpec ToSpec(int siteCount)
        {
            try
            {
                return new SearchAndDestroySpec(
                    siteCount, _roundSeconds, _plantSeconds, _defuseSeconds, _fuseSeconds, _winsToWin, _preparationSeconds, _swapEveryRounds);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"search and destroy definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        /// <summary>spec の値 (地点の数を除く) をすべて写す。spec は検査済みなので、写した後は必ず <see cref="ToSpec"/> に通る。</summary>
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
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed search and destroy definition)" : name;
    }
}
