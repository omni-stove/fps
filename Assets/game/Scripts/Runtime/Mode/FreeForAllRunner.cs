using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// フリーフォーオール (<see cref="FreeForAllMode"/>) をシーンで進める。参加者 (<see cref="SideMember"/>) は 1 人ずつ別の陣営にすること
    /// (同じ陣営が 2 人いると始められない)。モードの状態 (<see cref="ModeStatus"/>) の <see cref="ModeStatus.Detail"/> には、
    /// この画面のプレイヤーの順位と 1 位との差 (<see cref="FreeForAllMode.Standing"/>) を入れる。プレイヤーがいなければ null。
    /// 制限時間・点の上限・リスポーン・キル 1 回の点は、<see cref="Definition"/>、入口 (<see cref="GameContent.FreeForAll"/>)、Inspector の値の順で読む。
    /// </summary>
    public sealed class FreeForAllRunner : ModeRunnerBase
    {
        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の制限時間・キル数・リスポーン・キル 1 回の点の代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private FreeForAllDefinition _definition;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = FreeForAllMode.DefaultTimeLimitSeconds;

        [SerializeField, Min(1), Tooltip("勝利に必要なキル数")]
        private int _scoreLimit = FreeForAllMode.DefaultScoreLimit;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = FreeForAllMode.DefaultRespawnSeconds;

        [SerializeField, Min(1), Tooltip("別の人を 1 回倒すごとに入る点")]
        private int _pointsPerKill = FreeForAllMode.DefaultPointsPerKill;

        /// <summary>調整値のアセット。null なら入口 (<see cref="GameContent"/>) のもの、それも無ければ Inspector の値で始める。</summary>
        public FreeForAllDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        /// <summary>配線した <see cref="Definition"/>、空なら入口 (<see cref="GameContent"/>) のもの。どちらも無ければ null。</summary>
        public FreeForAllDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.FreeForAll);

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public int ScoreLimit
        {
            get => _scoreLimit;
            set => _scoreLimit = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        /// <summary>Inspector のキル 1 回の点。<see cref="EffectiveDefinition"/> があるときは使わない。</summary>
        public int PointsPerKill
        {
            get => _pointsPerKill;
            set => _pointsPerKill = value;
        }

        /// <summary>
        /// <see cref="EffectiveDefinition"/> があればその値で、無ければ Inspector の値でモードを組む。
        /// アセットの値が不正なら <see cref="InvalidOperationException"/>、Inspector の値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public ModeSession CreateConfiguredSession()
        {
            var definition = EffectiveDefinition;
            return definition != null
                ? definition.CreateSession()
                : FreeForAllMode.Create(_timeLimitSeconds, _scoreLimit, _respawnSeconds, _pointsPerKill);
        }

        protected override ModeSession CreateSession() => CreateConfiguredSession();

        protected override object HudDetail
        {
            get
            {
                var player = LocalPlayer;
                return player != null && Session != null ? FreeForAllMode.Standing(Session, player.Side) : (object)null;
            }
        }
    }
}
