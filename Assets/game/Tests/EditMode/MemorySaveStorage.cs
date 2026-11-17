using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;

namespace Shooter.Tests.EditMode
{
    /// <summary>スロットごとに置き、書き込みを失敗させられるメモリ上の <see cref="ISaveStorage"/>。</summary>
    internal sealed class MemorySaveStorage : ISaveStorage
    {
        public const string FailureMessage = "disk full";

        private readonly Dictionary<string, string> _slots = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool FailWrites { get; set; }

        public int WriteCount { get; private set; }

        public bool Has(string slot)
        {
            return _slots.ContainsKey(slot);
        }

        public string Text(string slot)
        {
            return _slots.TryGetValue(slot, out var text) ? text : null;
        }

        public bool TryRead(string slot, out string text)
        {
            return _slots.TryGetValue(slot, out text);
        }

        public void Write(string slot, string text)
        {
            if (FailWrites)
            {
                throw new IOException(FailureMessage);
            }

            _slots[slot] = text;
            WriteCount++;
        }

        public void Delete(string slot)
        {
            _slots.Remove(slot);
        }
    }
}
