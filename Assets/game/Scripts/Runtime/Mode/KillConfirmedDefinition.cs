using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キルコンファームド (<see cref="KillConfirmed"/>) の調整値をアセットとして持つ。<see cref="ToSpec"/> で <see cref="KillConfirmedSpec"/> に変換する。
    /// 新しく作ったときの値は <see cref="KillConfirmedRunner"/> の既定値と同じ。
    /// <see cref="KillConfirmedRunner.Definition"/> に配線すると、Runner は Inspector の値の代わりにこれを読む。
    /// </summary>
    [CreateAssetMenu(fileName = "KillConfirmed", menuName = "Fps/Modes/Kill Confirmed")]
    public sealed class KillConfirmedDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("どちらかのチームがこの確定数に届いたら試合が終わる")]
        private int _scoreLimit = KillConfirmedRunner.DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = KillConfirmedRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = KillConfirmedRunner.DefaultRespawnSeconds;

        [SerializeField, Min(0.01f), Tooltip("落ちたタグが誰にも拾われずに消えるまでの秒数")]
        private float _tagLifetimeSeconds = KillConfirmedRunner.DefaultTagLifetimeSeconds;

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

        public float TagLifetimeSeconds
        {
            get => _tagLifetimeSeconds;
            set => _tagLifetimeSeconds = value;
        }

        /// <summary>定義を <see cref="KillConfirmedSpec"/> に変換する。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="InvalidOperationException">値が不正なとき。メッセージにアセット名を含む。</exception>
        public KillConfirmedSpec ToSpec()
        {
            try
            {
                return new KillConfirmedSpec(_scoreLimit, _timeLimitSeconds, _respawnSeconds, _tagLifetimeSeconds);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"kill confirmed definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        /// <summary>spec の値をすべて写す。spec は検査済みなので、写した後は必ず <see cref="ToSpec"/> に通る。</summary>
        public void CopyFrom(KillConfirmedSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _scoreLimit = spec.ScoreLimit;
            _timeLimitSeconds = spec.TimeLimitSeconds;
            _respawnSeconds = spec.RespawnSeconds;
            _tagLifetimeSeconds = spec.TagLifetimeSeconds;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed kill confirmed definition)" : name;
    }
}
