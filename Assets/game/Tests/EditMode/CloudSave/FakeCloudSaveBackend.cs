using System;
using System.Collections.Generic;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    // 2 台の端末から同じアカウントのクラウドを見る場面を作れるよう、ファイルの表は外から渡して共有できるようにする。
    internal sealed class FakeCloudSaveBackend : ICloudSaveBackend
    {
        public const string OfflineError = "offline";

        private readonly List<Action> _held = new List<Action>();

        public FakeCloudSaveBackend(Dictionary<string, string> files = null)
        {
            Files = files ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public Dictionary<string, string> Files { get; }

        public List<string> Calls { get; } = new List<string>();

        public string SignedInAccount { get; set; }

        public int MaxFileNameLength { get; set; } = 64;

        public bool Offline { get; set; }

        public bool Hold { get; set; }

        public int HeldCount => _held.Count;

        public int Ticks { get; private set; }

        public void Tick()
        {
            Ticks++;
        }

        public void List(Action<IReadOnlyList<string>, string> done)
        {
            Calls.Add("list");
            Run(() =>
            {
                if (Offline)
                {
                    done(null, OfflineError);
                    return;
                }

                var names = new List<string>(Files.Keys);
                names.Sort(StringComparer.Ordinal);
                done(names, null);
            });
        }

        public void Read(string fileName, Action<string, string> done)
        {
            Calls.Add("read " + fileName);
            Run(() =>
            {
                if (Offline)
                {
                    done(null, OfflineError);
                    return;
                }

                done(Files.TryGetValue(fileName, out var text) ? text : null, null);
            });
        }

        public void Write(string fileName, string text, Action<string> done)
        {
            Calls.Add("write " + fileName);
            Run(() =>
            {
                if (Offline)
                {
                    done(OfflineError);
                    return;
                }

                Files[fileName] = text;
                done(null);
            });
        }

        public void ReleaseHeld()
        {
            var held = _held.ToArray();
            _held.Clear();
            foreach (var action in held)
            {
                action();
            }
        }

        private void Run(Action action)
        {
            if (Hold)
            {
                _held.Add(action);
                return;
            }

            action();
        }
    }
}
