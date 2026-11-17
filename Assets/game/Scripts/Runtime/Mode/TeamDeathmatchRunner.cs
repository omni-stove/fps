using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class TeamDeathmatchRunner : ModeRunnerBase
    {
        public const int DefaultSideCount = TeamDeathmatchSpec.DefaultSideCount;
        public const int DefaultSideCapacity = TeamDeathmatchSpec.DefaultSideCapacity;
        public const int DefaultPointsPerKill = TeamDeathmatchSpec.DefaultPointsPerKill;
        public const int DefaultPointsPerAssist = TeamDeathmatchSpec.DefaultPointsPerAssist;
        public const float DefaultTimeLimitSeconds = 300f;
        public const int DefaultScoreLimit = 20;
        public const float DefaultRespawnSeconds = 3f;

        [SerializeField, Min(2), Tooltip("陣営の数。SideMember の陣営 ID はこれ未満にする。Definition があればそちらを読む")]
        private int _sideCount = DefaultSideCount;

        [SerializeField, Min(1), Tooltip("1 つの陣営に入れる人数の上限。Definition があればそちらを読む")]
        private int _sideCapacity = DefaultSideCapacity;

        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると陣営の数と人数・制限時間・キル数・リスポーン・キル 1 回とアシスト 1 回の点の代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private TeamDeathmatchDefinition _definition;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(1), Tooltip("勝利に必要なキル数")]
        private int _scoreLimit = DefaultScoreLimit;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Min(1), Tooltip("別の陣営を 1 回倒すごとに入る点")]
        private int _pointsPerKill = DefaultPointsPerKill;

        [SerializeField, Min(0), Tooltip("別の陣営を倒すのを 1 回アシストするごとに入る点。0 ならアシストでは点が入らない")]
        private int _pointsPerAssist = DefaultPointsPerAssist;

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

        public TeamDeathmatchDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

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

        public int PointsPerKill
        {
            get => _pointsPerKill;
            set => _pointsPerKill = value;
        }

        public int PointsPerAssist
        {
            get => _pointsPerAssist;
            set => _pointsPerAssist = value;
        }

        public static ModeSession CreateSession(
            int sideCount,
            int sideCapacity,
            float timeLimitSeconds,
            int scoreLimit,
            float respawnSeconds,
            int pointsPerKill = DefaultPointsPerKill,
            int pointsPerAssist = DefaultPointsPerAssist)
        {
            return new ModeSession(
                new Squads(sideCount, sideCapacity),
                timeLimitSeconds,
                new IScoreRule[] { new KillScore(pointsPerKill, pointsPerAssist) },
                new IndividualRespawn(respawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(scoreLimit), new TimeLimitWin() });
        }

        public static ModeSession CreateSession(TeamDeathmatchSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return CreateSession(
                spec.SideCount, spec.SideCapacity, spec.TimeLimitSeconds, spec.ScoreLimit, spec.RespawnSeconds, spec.PointsPerKill, spec.PointsPerAssist);
        }

        public TeamDeathmatchSpec CreateSpec()
        {
            var definition = EffectiveDefinition;
            if (definition != null)
            {
                return definition.ToSpec();
            }

            return new TeamDeathmatchSpec(
                _scoreLimit, _timeLimitSeconds, _respawnSeconds, null, _sideCount, _sideCapacity, _pointsPerKill, _pointsPerAssist);
        }

        protected override ModeSession CreateSession() => CreateSession(CreateSpec());

        protected override object HudDetail => Session;
    }
}
