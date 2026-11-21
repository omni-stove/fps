using System;

namespace Shooter.Core
{
    public sealed class LoadoutPreset
    {
        // 名前が空なのは、プレイヤーが名前を付けていない (前の版から移した組も含む) こと。何と出すかは UI が決める。
        public LoadoutPreset(string name, LoadoutChoice choice)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Choice = choice ?? throw new ArgumentNullException(nameof(choice));
        }

        public string Name { get; }

        public LoadoutChoice Choice { get; }

        public bool HasName => Name.Length > 0;
    }
}
