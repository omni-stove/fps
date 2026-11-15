using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Server
{
    /// <summary>
    /// 視線を遮る物の集まり。どれか 1 つでも遮れば遮る。何も足していなければ何も遮らない。
    /// </summary>
    /// <remarks>Bot の知覚はこれに問い合わせるので、遮る物を足す側は Bot を知らずに済む。</remarks>
    internal sealed class ServerSightBlockers : ISightBlocker
    {
        private readonly List<ISightBlocker> _blockers = new List<ISightBlocker>();

        public int Count => _blockers.Count;

        public void Add(ISightBlocker blocker)
        {
            _blockers.Add(blocker ?? throw new ArgumentNullException(nameof(blocker)));
        }

        public bool Remove(ISightBlocker blocker)
        {
            return _blockers.Remove(blocker);
        }

        public bool Blocks(Vector3 from, Vector3 to)
        {
            foreach (var blocker in _blockers)
            {
                if (blocker.Blocks(from, to))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
