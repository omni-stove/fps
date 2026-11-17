using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
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

        public FreeForAllDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

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

        public int PointsPerKill
        {
            get => _pointsPerKill;
            set => _pointsPerKill = value;
        }

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
