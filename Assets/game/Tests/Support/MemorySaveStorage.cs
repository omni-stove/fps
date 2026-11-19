using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;

namespace Shooter.Tests.EditMode
{
    public sealed class MemorySaveStorage : ISaveStorage
    {
        public const string FailureMessage = "disk full";

        public const string ReadOnlyMessage = "the save folder is read-only";

        public Dictionary<string, string> Slots { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool FailWrites { get; set; }

        public bool FailDeletes { get; set; }

        public int WriteCount { get; private set; }

        public bool Has(string slot)
        {
            return Slots.ContainsKey(slot);
        }

        public string Text(string slot)
        {
            return Slots.TryGetValue(slot, out var text) ? text : null;
        }

        public bool TryRead(string slot, out string text)
        {
            return Slots.TryGetValue(slot, out text);
        }

        public void Write(string slot, string text)
        {
            if (FailWrites)
            {
                throw new IOException(FailureMessage);
            }

            Slots[slot] = text;
            WriteCount++;
        }

        public void Delete(string slot)
        {
            if (FailDeletes)
            {
                throw new IOException(ReadOnlyMessage);
            }

            Slots.Remove(slot);
        }
    }
}
