using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// キルフィードに出している行の待ち行列。古い順に並び、<see cref="Capacity"/> 行を超えたら一番古い行から消え、
    /// 1 行は足してから <see cref="LineSeconds"/> 秒 (<see cref="Tick"/> で進めた時間) で消える。
    /// 行が増えるか減ったときだけ <see cref="Changed"/> を呼ぶ。
    /// </summary>
    public sealed class KillFeed
    {
        /// <summary>同時に出す行の数の既定値。</summary>
        public const int DefaultCapacity = 5;

        /// <summary>1 行を出しておく秒数の既定値。</summary>
        public const float DefaultLineSeconds = 5f;

        private readonly List<KillFeedLine> _lines = new List<KillFeedLine>();
        private readonly List<float> _remaining = new List<float>();

        /// <summary>行が足されたか消えたときに呼ばれる。今の行は <see cref="Lines"/> で読む。</summary>
        public event Action Changed;

        /// <param name="capacity">同時に出す行の数。1 以上。</param>
        /// <param name="lineSeconds">1 行を出しておく秒数。正の有限値。</param>
        public KillFeed(int capacity = DefaultCapacity, float lineSeconds = DefaultLineSeconds)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1");
            }

            if (!(lineSeconds > 0f) || float.IsInfinity(lineSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(lineSeconds), lineSeconds, "lineSeconds must be a positive finite number");
            }

            Capacity = capacity;
            LineSeconds = lineSeconds;
            Lines = _lines.AsReadOnly();
        }

        /// <summary>同時に出す行の数。</summary>
        public int Capacity { get; }

        /// <summary>1 行を出しておく秒数。</summary>
        public float LineSeconds { get; }

        /// <summary>出している行。古い順 (最後が一番新しい)。</summary>
        public IReadOnlyList<KillFeedLine> Lines { get; }

        /// <summary>出している行の数。</summary>
        public int Count => _lines.Count;

        /// <summary>index 番目 (古い順) の行が消えるまでの秒数。</summary>
        public float RemainingSeconds(int index)
        {
            if (index < 0 || index >= _remaining.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be within the lines");
            }

            return _remaining[index];
        }

        /// <summary>line を一番新しい行として足す。<see cref="Capacity"/> 行を超えるなら一番古い行を消す。</summary>
        public void Add(KillFeedLine line)
        {
            while (_lines.Count >= Capacity)
            {
                RemoveOldest();
            }

            _lines.Add(line);
            _remaining.Add(LineSeconds);
            Changed?.Invoke();
        }

        /// <summary>どの行も deltaSeconds 進め、出しておく秒数が尽きた行を消す。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_lines.Count == 0 || deltaSeconds == 0f)
            {
                return;
            }

            var removed = false;
            for (var i = _remaining.Count - 1; i >= 0; i--)
            {
                var left = _remaining[i] - deltaSeconds;
                if (left > 0f)
                {
                    _remaining[i] = left;
                    continue;
                }

                _remaining.RemoveAt(i);
                _lines.RemoveAt(i);
                removed = true;
            }

            if (removed)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>行をすべて消す。行が無ければ何もしない。</summary>
        public void Clear()
        {
            if (_lines.Count == 0)
            {
                return;
            }

            _lines.Clear();
            _remaining.Clear();
            Changed?.Invoke();
        }

        private void RemoveOldest()
        {
            _lines.RemoveAt(0);
            _remaining.RemoveAt(0);
        }
    }
}
