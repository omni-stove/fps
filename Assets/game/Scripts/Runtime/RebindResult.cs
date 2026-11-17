using System;
using System.Collections.Generic;

namespace Shooter.Runtime
{
    public sealed class RebindResult
    {
        public RebindResult(
            RebindSlot slot,
            bool completed,
            string previousOverridePath,
            string previousPath,
            string path,
            IReadOnlyList<BindingConflict> conflicts)
        {
            Slot = slot;
            Completed = completed;
            PreviousOverridePath = previousOverridePath;
            PreviousPath = previousPath;
            Path = path;
            Conflicts = conflicts ?? Array.Empty<BindingConflict>();
        }

        public RebindSlot Slot { get; }

        public bool Completed { get; }

        public string PreviousOverridePath { get; }

        public string PreviousPath { get; }

        public string Path { get; }

        public IReadOnlyList<BindingConflict> Conflicts { get; }

        public bool Changed => Completed && !string.Equals(PreviousPath, Path, StringComparison.OrdinalIgnoreCase);
    }
}
