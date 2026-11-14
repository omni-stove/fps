using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ハードポイント (<see cref="HardpointRunner"/>) の調整値をアセットとして持つ。拠点の数はシーンの <see cref="ControlPointZone"/> で決まるので持たず、
    /// <see cref="ToSpec"/> に渡す。新しく作ったときの値は <see cref="HardpointRunner"/> の既定値と同じ。
    /// 中身は Fps.Editor.ModeTuningAssetBuilder が無ければ作る。
    /// </summary>
    [CreateAssetMenu(fileName = "Hardpoint", menuName = "Fps/Hardpoint Tuning")]
    public sealed class HardpointDefinition : ScriptableObject
    {
        // Validate は拠点の数を知らないので、HardpointSpec が受け付ける最小の数で確かめる。
        private const int _minimumPointCount = 2;

        [SerializeField, Min(0.01f), Tooltip("有効な拠点が次の拠点へ移るまでの秒数")]
        private float _rotateSeconds = HardpointRunner.DefaultRotateSeconds;

        [SerializeField, Min(0.01f), Tooltip("有効な拠点に自チームだけがいる間、1 秒あたり入るポイント")]
        private float _pointsPerSecond = HardpointRunner.DefaultPointsPerSecond;

        [SerializeField, Min(1), Tooltip("どちらかのチームがこのポイントに届いたら試合が終わる")]
        private int _scoreLimit = HardpointRunner.DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = HardpointRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = HardpointRunner.DefaultRespawnSeconds;

        public float RotateSeconds
        {
            get => _rotateSeconds;
            set => _rotateSeconds = value;
        }

        public float PointsPerSecond
        {
            get => _pointsPerSecond;
            set => _pointsPerSecond = value;
        }

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

        /// <summary>拠点が pointCount 個の <see cref="HardpointSpec"/> を作る。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">pointCount か、どれかの値が不正なとき。メッセージにアセット名を含む。</exception>
        public HardpointSpec ToSpec(int pointCount)
        {
            try
            {
                return new HardpointSpec(pointCount, _rotateSeconds, _pointsPerSecond, _scoreLimit, _timeLimitSeconds, _respawnSeconds);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new ArgumentOutOfRangeException(e.ParamName, e.ActualValue, $"hardpoint tuning '{SourceName}' is invalid: {e.Message}");
            }
        }

        /// <summary>拠点の数に依らない値がすべて <see cref="ToSpec"/> に通ることを確かめる。</summary>
        /// <exception cref="ArgumentOutOfRangeException">どれかの値が不正なとき。メッセージにアセット名を含む。</exception>
        public void Validate()
        {
            ToSpec(_minimumPointCount);
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed hardpoint tuning)" : name;
    }
}
