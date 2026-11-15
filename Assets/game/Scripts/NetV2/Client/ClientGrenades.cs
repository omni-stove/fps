using System;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// ネット対戦で投げるグレネードの種類。持っている種類 (<see cref="GrenadePouchSync"/>) はサーバーが決めるので、ここは選んだ種類の Id だけを持つ。
    /// 選んだ種類を持っていなければ (出撃し直して持つ種類が変わったなど)、持っている最初の種類を投げる。
    /// </summary>
    public sealed class ClientGrenades
    {
        private string _selected = "";

        /// <summary>今投げる種類の Id。何も持っていなければ空文字。</summary>
        public string Current(GrenadePouchSync pouch)
        {
            if (pouch == null || pouch.Count == 0)
            {
                return "";
            }

            return IndexOf(pouch, _selected) >= 0 ? _selected : pouch.TypeIdAt(0);
        }

        /// <summary>持っている次の種類を選ぶ。持つ種類が 1 つ以下なら何もせず false。</summary>
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
