using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class KeySharingSpec
    {
        private static readonly string[] _none = Array.Empty<string>();

        // 押している間は毎フレーム引かれるので、読み込みのときに組んでおく。
        private readonly Dictionary<string, string[]> _yieldsTo;

        public KeySharingSpec(IReadOnlyList<KeySharingRule> shared)
        {
            if (shared == null)
            {
                throw new ArgumentNullException(nameof(shared));
            }

            var copy = new KeySharingRule[shared.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = shared[i] ?? throw new ArgumentException("shared must not contain null", nameof(shared));
            }

            Shared = copy;
            _yieldsTo = BuildYieldsTo(copy);
        }

        // どの組も共有を許さないのが、キーの重なりを警告する前からの振る舞い。
        public static KeySharingSpec Default { get; } = new KeySharingSpec(Array.Empty<KeySharingRule>());

        public IReadOnlyList<KeySharingRule> Shared { get; }

        public bool Allows(string action, string other)
        {
            if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(other)
                || string.Equals(action.Trim(), other.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (var rule in Shared)
            {
                if (rule.Contains(action) && rule.Contains(other))
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<string> YieldsTo(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return _none;
            }

            return _yieldsTo.TryGetValue(action.Trim(), out var preferred) ? preferred : _none;
        }

        private static Dictionary<string, string[]> BuildYieldsTo(IReadOnlyList<KeySharingRule> rules)
        {
            var lists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in rules)
            {
                if (rule.Prefer == null)
                {
                    continue;
                }

                foreach (var action in rule.Actions)
                {
                    if (string.Equals(action, rule.Prefer, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!lists.TryGetValue(action, out var preferred))
                    {
                        preferred = new List<string>();
                        lists[action] = preferred;
                    }

                    if (!preferred.Exists(p => string.Equals(p, rule.Prefer, StringComparison.OrdinalIgnoreCase)))
                    {
                        preferred.Add(rule.Prefer);
                    }
                }
            }

            var built = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in lists)
            {
                built[pair.Key] = pair.Value.ToArray();
            }

            return built;
        }
    }
}
