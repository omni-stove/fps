using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class KeySharingRule
    {
        public KeySharingRule(IReadOnlyList<string> actions, string prefer = null)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            var names = new string[actions.Count];
            for (var i = 0; i < names.Length; i++)
            {
                var name = SpecLists.Id(actions[i], nameof(actions));
                for (var j = 0; j < i; j++)
                {
                    // Input System はアクション名を大文字小文字を区別せずに引くので、綴りの大小だけ違う名前も同じアクションとして断る。
                    if (string.Equals(names[j], name, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException("actions contains '" + name + "' twice", nameof(actions));
                    }
                }

                names[i] = name;
            }

            if (names.Length < 2)
            {
                throw new ArgumentException("actions must name at least two actions that share a key", nameof(actions));
            }

            Actions = names;
            if (string.IsNullOrWhiteSpace(prefer))
            {
                return;
            }

            var preferred = prefer.Trim();
            var index = IndexOf(names, preferred);
            if (index < 0)
            {
                throw new ArgumentException("prefer '" + preferred + "' is not one of the actions", nameof(prefer));
            }

            Prefer = names[index];
        }

        public IReadOnlyList<string> Actions { get; }

        // null なら、キットはどちらも止めず、押し分け (短押しと長押しなど) はゲームがバインドの Interaction で決める。
        public string Prefer { get; }

        public bool Contains(string action)
        {
            return action != null && IndexOf(Actions, action.Trim()) >= 0;
        }

        private static int IndexOf(IReadOnlyList<string> names, string name)
        {
            for (var i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
