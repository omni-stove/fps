using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // リーダーを選ぶ乱数の種と目印の見た目は Runner の側にあるので持たない。
    [CreateAssetMenu(fileName = "Guardian", menuName = "Shooter/Modes/Guardian")]
    public sealed class GuardianDefinition : ScriptableObject
    {
        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = GuardianRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("リーダーが生きている間、倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = GuardianRunner.DefaultRespawnSeconds;

        [SerializeField, Min(1), Tooltip("別のチームを 1 回倒すごとに入る点。時間切れでリーダーの生き残りで決まらなければ、点の多いチームが勝つ")]
        private int _pointsPerKill = GuardianMode.DefaultPointsPerKill;

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

        public int PointsPerKill
        {
            get => _pointsPerKill;
            set => _pointsPerKill = value;
        }

        public ModeSession CreateSession(System.Random random, out GuardianRespawn leaders)
        {
            try
            {
                return GuardianMode.CreateSession(_timeLimitSeconds, _respawnSeconds, random, out leaders, _pointsPerKill);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"guardian definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed guardian definition)" : name;
    }
}
