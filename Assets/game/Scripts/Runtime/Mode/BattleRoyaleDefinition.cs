using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// バトルロイヤル (<see cref="BattleRoyaleSession"/>) の分隊の数と人数・制限時間・bot の用事を決め直す間隔と探す半径・パラシュートで降ろす高さをアセットとして持つ。
    /// 新しく作ったときの値は <see cref="BattleRoyaleRunner"/> の既定値と同じ。味方を起こせる距離と秒数はルール (<see cref="ActionRules"/>) にあるので持たない。
    /// 武器を手放させるか・縮むエリア・地面と上空のどちらから始めるかはシーンの組み方なので Runner の側に置く。
    /// <see cref="BattleRoyaleRunner.Definition"/> に配線するか入口 (<see cref="GameContent.BattleRoyale"/>) に置くと、Runner は Inspector の値の代わりにこれを読む。
    /// </summary>
    [CreateAssetMenu(fileName = "BattleRoyale", menuName = "Fps/Modes/Battle Royale")]
    public sealed class BattleRoyaleDefinition : ScriptableObject
    {
        [SerializeField, Min(2), Tooltip("分隊の数。参加者の陣営の ID はこれより小さいこと")]
        private int _squadCount = BattleRoyaleRunner.DefaultSquadCount;

        [SerializeField, Min(1), Tooltip("1 隊の人数の上限")]
        private int _squadSize = BattleRoyaleRunner.DefaultSquadSize;

        [SerializeField, Min(0f), Tooltip("制限時間 (秒)。0 なら制限なし (縮むエリアで決着させる)")]
        private float _timeLimitSeconds = BattleRoyaleRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(0.05f), Tooltip("bot の用事 (エリアの中へ移る・拾える物を取る) を決め直す間隔 (秒)")]
        private float _botPlanIntervalSeconds = BattleRoyaleRunner.DefaultBotPlanIntervalSeconds;

        [SerializeField, Min(0f), Tooltip("bot がダウンした味方と拾える物を探す半径 (m)")]
        private float _botSearchRadius = BattleRoyaleBotPlanner.DefaultSearchRadius;

        [SerializeField, Min(0f), Tooltip("パラシュートで降ろすとき、出現点からどれだけ上に出すか (m)")]
        private float _dropHeight = BattleRoyaleRunner.DefaultDropHeight;

        public int SquadCount
        {
            get => _squadCount;
            set => _squadCount = value;
        }

        public int SquadSize
        {
            get => _squadSize;
            set => _squadSize = value;
        }

        /// <summary>制限時間 (秒)。0 なら制限なし。</summary>
        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public float BotPlanIntervalSeconds
        {
            get => _botPlanIntervalSeconds;
            set => _botPlanIntervalSeconds = value;
        }

        public float BotSearchRadius
        {
            get => _botSearchRadius;
            set => _botSearchRadius = value;
        }

        public float DropHeight
        {
            get => _dropHeight;
            set => _dropHeight = value;
        }

        /// <summary>この定義の値と actions でバトルロイヤル (<see cref="BattleRoyaleRunner.CreateBattleRoyale"/>) を組む。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="ArgumentNullException">actions が null。</exception>
        /// <exception cref="InvalidOperationException">値が不正なとき。メッセージにアセット名を含む。</exception>
        public BattleRoyaleSession CreateBattleRoyale(ActionRules actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            RequireFinite(_botPlanIntervalSeconds > 0f, "botPlanIntervalSeconds must be positive", _botPlanIntervalSeconds);
            RequireFinite(_botSearchRadius >= 0f, "botSearchRadius must not be negative", _botSearchRadius);
            RequireFinite(_dropHeight >= 0f, "dropHeight must not be negative", _dropHeight);
            try
            {
                return BattleRoyaleRunner.CreateBattleRoyale(_squadCount, _squadSize, _timeLimitSeconds, actions);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"battle royale definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        private void RequireFinite(bool inRange, string message, float value)
        {
            if (!inRange || float.IsInfinity(value))
            {
                throw new InvalidOperationException($"battle royale definition '{SourceName}' is invalid: {message} and finite (was {value})");
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed battle royale definition)" : name;
    }
}
