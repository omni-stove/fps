using System;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 持っている種類 (GrenadePouchSync) はサーバーが決めるので、ここは選んだ種類の Id だけを持つ。
    public sealed class ClientGrenades
    {
        private string _selected = "";

        public string Current(GrenadePouchSync pouch)
        {
            if (pouch == null || pouch.Count == 0)
            {
                return "";
            }

            return IndexOf(pouch, _selected) >= 0 ? _selected : pouch.TypeIdAt(0);
        }

        public bool SelectNext(GrenadePouchSync pouch)
        {
            if (pouch == null || pouch.Count < 2)
            {
                return false;
            }

            var at = IndexOf(pouch, Current(pouch));
            _selected = pouch.TypeIdAt((at + 1) % pouch.Count);
            return true;
        }

        private static int IndexOf(GrenadePouchSync pouch, string typeId)
        {
            for (var i = 0; i < pouch.Count; i++)
            {
                if (string.Equals(pouch.TypeIdAt(i), typeId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
