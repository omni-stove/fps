using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fps.Core
{
    /// <summary>
    /// 画面の下に出す音の合図の字幕の行。合図は種類 (<see cref="SoundCueKind"/>) ごとに 1 行にまとめ、
    /// 同じ種類が続けて届いたら行を増やさずに向きと距離を新しい合図に書き換え、出しておく時間を延ばす。
    /// 行は最初に出た順に並べ、書き換えても並びは変えない (連射のたびに行が動いて読みにくくならないよう)。
    /// 時刻は呼ぶ側が決めた物差し (秒) で渡す。
    /// </summary>
    public sealed class SubtitleLines
    {
        /// <summary>最後の合図から行を出しておく秒数。</summary>
        public const float DefaultSeconds = 3f;

        private readonly List<SubtitleLine> _lines = new List<SubtitleLine>();
        private readonly ReadOnlyCollection<SubtitleLine> _readOnly;
        private readonly float _seconds;

        /// <param name="seconds">最後の合図から行を出しておく秒数。</param>
        /// <exception cref="ArgumentOutOfRangeException">seconds が 0 以下か有限でない。</exception>
        public SubtitleLines(float seconds = DefaultSeconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "must be a positive finite number of seconds");
            }

            _seconds = seconds;
            _readOnly = _lines.AsReadOnly();
        }

        /// <summary>出している行 (最初に出た順)。</summary>
        public IReadOnlyList<SubtitleLine> Lines => _readOnly;

        /// <summary>
        /// cue を時刻 now に出す。同じ種類の行があればそれを書き換え、無ければ末尾に足す。
        /// 出す文字が変わったか、行を足したら true (時間を延ばしただけなら false)。
        /// </summary>
        public bool Show(SoundCue cue, float now)
        {
            var text = SubtitleText.Format(cue);
            var expiresAt = now + _seconds;
            for (var i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].Kind != cue.Kind)
                {
                    continue;
                }

                var changed = !string.Equals(_lines[i].Text, text, StringComparison.Ordinal);
                _lines[i] = new SubtitleLine(cue.Kind, text, expiresAt);
                return changed;
            }

            _lines.Add(new SubtitleLine(cue.Kind, text, expiresAt));
            return true;
        }

        /// <summary>時刻 now までに出し終えた行を消す。消したら true。</summary>
        public bool Expire(float now)
        {
            return _lines.RemoveAll(line => line.ExpiresAt <= now) > 0;
        }

        /// <summary>行をすべて消す。消したら true。</summary>
        public bool Clear()
        {
            if (_lines.Count == 0)
            {
                return false;
            }

            _lines.Clear();
            return true;
        }
    }
}
