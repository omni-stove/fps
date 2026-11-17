using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 書き換えた行を末尾へ移さずその場で書き換えるのは、連射のたびに行が動いて読みにくくならないようにするため。
    public sealed class SubtitleLines
    {
        public const float DefaultSeconds = 3f;

        private readonly List<SubtitleLine> _lines = new List<SubtitleLine>();
        private readonly ReadOnlyCollection<SubtitleLine> _readOnly;
        private readonly float _seconds;

        public SubtitleLines(float seconds = DefaultSeconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "must be a positive finite number of seconds");
            }

            _seconds = seconds;
            _readOnly = _lines.AsReadOnly();
        }

        public IReadOnlyList<SubtitleLine> Lines => _readOnly;

        public bool Show(SoundCue cue, float now)
        {
            var text = SubtitleText.Format(cue);
            var expiresAt = now + _seconds;
            for (var i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].IsSpeech || _lines[i].Kind != cue.Kind)
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

        public bool Say(string speaker, string text, float now)
        {
            if (speaker == null)
            {
                throw new ArgumentNullException(nameof(speaker));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var expiresAt = now + _seconds;
            for (var i = 0; i < _lines.Count; i++)
            {
                if (!_lines[i].IsSpeech || !string.Equals(_lines[i].Speaker, speaker, StringComparison.Ordinal))
                {
                    continue;
                }

                var changed = !string.Equals(_lines[i].Text, text, StringComparison.Ordinal);
                _lines[i] = SubtitleLine.Speech(speaker, text, expiresAt);
                return changed;
            }

            _lines.Add(SubtitleLine.Speech(speaker, text, expiresAt));
            return true;
        }

        public bool Expire(float now)
        {
            return _lines.RemoveAll(line => line.ExpiresAt <= now) > 0;
        }

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
