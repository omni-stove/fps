using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// チームデスマッチの調整値 (<see cref="TeamDeathmatchSpec"/> の得点・制限時間・リスポーン) をアセットとして持つ。
    /// <see cref="ToSpec"/> で <see cref="TeamDeathmatchSpec"/> に変換する。ルールは <see cref="GameRulesDefinition"/> の側にあるので持たない。
    /// 新しく作ったときの値は <see cref="TeamDeathmatchRunner"/> の既定値と同じ。
    /// <see cref="TeamDeathmatchRunner.Definition"/> に配線すると、Runner は Inspector の値の代わりにこれを読む。
    /// </summary>
    [CreateAssetMenu(fileName = "TeamDeathmatch", menuName = "Fps/Modes/Team Deathmatch")]
    public sealed class TeamDeathmatchDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("勝利に必要なキル数")]
        private int _scoreLimit = TeamDeathmatchRunner.DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = TeamDeathmatchRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = TeamDeathmatchRunner.DefaultRespawnSeconds;

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

        /// <summary>定義と rules (null なら <see cref="GameRules.Default"/>) から <see cref="TeamDeathmatchSpec"/> を作る。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="InvalidOperationException">値が不正なとき。メッセージにアセット名を含む。</exception>
        public TeamDeathmatchSpec ToSpec(GameRules rules = null)
        {
            try
            {
                return new TeamDeathmatchSpec(_scoreLimit, _timeLimitSeconds, _respawnSeconds, rules);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"team deathmatch definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        /// <summary>spec の得点・制限時間・リスポーンを写す。spec は検査済みなので、写した後は必ず <see cref="ToSpec"/> に通る。</summary>
        public void CopyFrom(TeamDeathmatchSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _scoreLimit = spec.ScoreLimit;
            _timeLimitSeconds = spec.TimeLimitSeconds;
            _respawnSeconds = spec.RespawnSeconds;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed team deathmatch definition)" : name;
    }
}
