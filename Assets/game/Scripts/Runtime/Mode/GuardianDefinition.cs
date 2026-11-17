using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ガーディアン (<see cref="GuardianMode"/>) の制限時間・リスポーン・キル 1 回の点をアセットとして持つ。
    /// 新しく作ったときの値は <see cref="GuardianRunner"/> の既定値と同じ。リーダーを選ぶ乱数の種と目印の見た目は Runner の側にあるので持たない。
    /// <see cref="GuardianRunner.Definition"/> に配線するか入口 (<see cref="GameContent.Guardian"/>) に置くと、Runner は Inspector の値の代わりにこれを読む。
    /// </summary>
    [CreateAssetMenu(fileName = "Guardian", menuName = "Fps/Modes/Guardian")]
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

        /// <summary>この定義の値で、random でリーダーを選ぶモード (<see cref="GuardianMode.CreateSession(float, float, System.Random, out GuardianRespawn, int)"/>) を組む。</summary>
        /// <exception cref="ArgumentNullException">random が null。</exception>
        /// <exception cref="InvalidOperationException">値が不正なとき。メッセージにアセット名を含む。</exception>
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
