using System;
using System.Collections.Generic;

namespace Fps.Runtime
{
    /// <summary><see cref="PlayerBindings.StartRebind"/> の終わり。取り消されたときは <see cref="Completed"/> が false で、割り当ては変わっていない。</summary>
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

        /// <summary>入力を受け取って割り当てを変えたか。取り消し・中断なら false。</summary>
        public bool Completed { get; }

        /// <summary>変える前の上書き (無ければ null)。<see cref="PlayerBindings.RestoreOverride"/> に渡すと元に戻る。</summary>
        public string PreviousOverridePath { get; }

        /// <summary>変える前に効いていた入力のパス。</summary>
        public string PreviousPath { get; }

        /// <summary>今効いている入力のパス (例: "&lt;Keyboard&gt;/f")。</summary>
        public string Path { get; }

        /// <summary>新しい入力と重なったほかの割り当て。</summary>
        public IReadOnlyList<BindingConflict> Conflicts { get; }

        /// <summary>前と違う入力になったか。</summary>
        public bool Changed => Completed && !string.Equals(PreviousPath, Path, StringComparison.OrdinalIgnoreCase);
    }
}
