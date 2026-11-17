using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // ルールは GameRulesDefinition の側にあるので持たない。
    [CreateAssetMenu(fileName = "TeamDeathmatch", menuName = "Shooter/Modes/Team Deathmatch")]
    public sealed class TeamDeathmatchDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("勝利に必要なキル数")]
        private int _scoreLimit = TeamDeathmatchRunner.DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = TeamDeathmatchRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = TeamDeathmatchRunner.DefaultRespawnSeconds;

        [SerializeField, Min(2), Tooltip("陣営の数。SideMember の陣営 ID はこれ未満にする")]
        private int _sideCount = TeamDeathmatchSpec.DefaultSideCount;

        [SerializeField, Min(1), Tooltip("1 つの陣営に入れる人数の上限")]
        private int _sideCapacity = TeamDeathmatchSpec.DefaultSideCapacity;

        [SerializeField, Min(1), Tooltip("別の陣営を 1 回倒すごとに入る点")]
        private int _pointsPerKill = TeamDeathmatchSpec.DefaultPointsPerKill;

        [SerializeField, Min(0), Tooltip("別の陣営を倒すのを 1 回アシストするごとに入る点。0 ならアシストでは点が入らない")]
        private int _pointsPerAssist = TeamDeathmatchSpec.DefaultPointsPerAssist;

        public int ScoreLimit
        {
            get => _scoreLimit;
            set => _scoreLimit = value;
        }

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

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

        public TeamDeathmatchSpec ToSpec(GameRules rules = null)
        {
            try
            {
                return new TeamDeathmatchSpec(
                    _scoreLimit, _timeLimitSeconds, _respawnSeconds, rules, _sideCount, _sideCapacity, _pointsPerKill, _pointsPerAssist);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"team deathmatch definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(TeamDeathmatchSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _scoreLimit = spec.ScoreLimit;
            _timeLimitSeconds = spec.TimeLimitSeconds;
            _respawnSeconds = spec.RespawnSeconds;
            _sideCount = spec.SideCount;
            _sideCapacity = spec.SideCapacity;
            _pointsPerKill = spec.PointsPerKill;
            _pointsPerAssist = spec.PointsPerAssist;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed team deathmatch definition)" : name;
    }
}
