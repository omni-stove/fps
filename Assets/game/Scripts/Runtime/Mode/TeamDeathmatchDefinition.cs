using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// チームデスマッチの調整値 (<see cref="TeamDeathmatchSpec"/> の得点・制限時間・リスポーン・陣営の数と人数・キル 1 回の点) をアセットとして持つ。
    /// <see cref="ToSpec"/> で <see cref="TeamDeathmatchSpec"/> に変換する。ルールは <see cref="GameRulesDefinition"/> の側にあるので持たない。
    /// 新しく作ったときの値は <see cref="TeamDeathmatchRunner"/> の既定値と同じ。
    /// <see cref="TeamDeathmatchRunner.Definition"/> に配線すると、Runner は Inspector の値の代わりにこれを読む。
    /// </summary>
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

        /// <summary>定義と rules (null なら <see cref="GameRules.Default"/>) から <see cref="TeamDeathmatchSpec"/> を作る。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="InvalidOperationException">値が不正なとき。メッセージにアセット名を含む。</exception>
        public TeamDeathmatchSpec ToSpec(GameRules rules = null)
        {
            try
            {
                return new TeamDeathmatchSpec(_scoreLimit, _timeLimitSeconds, _respawnSeconds, rules, _sideCount, _sideCapacity, _pointsPerKill);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"team deathmatch definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        /// <summary>spec の得点・制限時間・リスポーン・陣営の数と人数・キル 1 回の点を写す。spec は検査済みなので、写した後は必ず <see cref="ToSpec"/> に通る。</summary>
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
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed team deathmatch definition)" : name;
    }
}
