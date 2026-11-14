using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="ModeRunnerBase"/> の最小の派生。<see cref="SideCount"/> 個の陣営 (<see cref="Squads"/>) でキル数を競い、
    /// どこかが <see cref="ScoreLimit"/> に届くか制限時間が切れたら終わる。倒れた人は <see cref="RespawnSeconds"/> 後に自陣営の出現点に戻る。
    /// 陣営が 2 つならチームデスマッチ、3 つ以上なら bot の陣営どうしも撃ち合う乱戦になる。
    /// </summary>
    public sealed class TeamDeathmatchRunner : ModeRunnerBase
    {
        public const int DefaultSideCount = 2;
        public const int DefaultSideCapacity = 8;
        public const float DefaultTimeLimitSeconds = 300f;
        public const int DefaultScoreLimit = 20;
        public const float DefaultRespawnSeconds = 3f;

        [SerializeField, Min(2), Tooltip("陣営の数。SideMember の陣営 ID はこれ未満にする")]
        private int _sideCount = DefaultSideCount;

        [SerializeField, Min(1), Tooltip("1 つの陣営に入れる人数の上限")]
        private int _sideCapacity = DefaultSideCapacity;

        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の制限時間・キル数・リスポーンの代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private TeamDeathmatchDefinition _definition;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(1), Tooltip("勝利に必要なキル数")]
        private int _scoreLimit = DefaultScoreLimit;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        public int SideCount
        {
            get => _sideCount;
            set => _sideCount = value;
        }

        public int SideCapacity
        {
            get => _sideCapacity;
            set => _sideCapacity = value;
        }

        /// <summary>調整値のアセット。null なら Inspector の値 (既定は Default* の値) で始める。</summary>
        public TeamDeathmatchDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        /// <summary>配線した <see cref="Definition"/>、空なら入口 (<see cref="GameContent"/>) のもの。どちらも無ければ null。</summary>
        public TeamDeathmatchDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.TeamDeathmatch);

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

        /// <summary>
        /// 陣営 sideCount 個 (1 つ sideCapacity 人まで)・1 キル 1 点・倒れてから respawnSeconds 後に戻る・scoreLimit 点か時間切れで終わるモードを作る。
        /// 値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public static ModeSession CreateSession(int sideCount, int sideCapacity, float timeLimitSeconds, int scoreLimit, float respawnSeconds)
        {
            return new ModeSession(
                new Squads(sideCount, sideCapacity),
                timeLimitSeconds,
                new IScoreRule[] { new KillScore() },
                new IndividualRespawn(respawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(scoreLimit), new TimeLimitWin() });
        }

        /// <summary>
        /// 得点・制限時間・リスポーンを、<see cref="Definition"/> があればその値で、無ければ Inspector の値で <see cref="TeamDeathmatchSpec"/> にする。
        /// ルールは <see cref="GameRules.Default"/> (この Runner はルールを読まない)。
        /// アセットの値が不正なら <see cref="InvalidOperationException"/>、Inspector の値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public TeamDeathmatchSpec CreateSpec()
        {
            var definition = EffectiveDefinition;
            if (definition != null)
            {
                return definition.ToSpec();
            }

            return new TeamDeathmatchSpec(_scoreLimit, _timeLimitSeconds, _respawnSeconds);
        }

        protected override ModeSession CreateSession()
        {
            var spec = CreateSpec();
            return CreateSession(_sideCount, _sideCapacity, spec.TimeLimitSeconds, spec.ScoreLimit, spec.RespawnSeconds);
        }
    }
}
