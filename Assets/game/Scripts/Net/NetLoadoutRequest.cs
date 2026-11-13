using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// 持ち主がサーバーへ送る、自分のロードアウトに選んだ武器の Id の並び (スロット順)。
    /// 数値は送らず Id だけを送り、武器の値と並びが許されるかはサーバーが自分の武器の一覧とルールで決める。
    /// </summary>
    public struct NetLoadoutRequest : INetworkSerializable
    {
        private string[] _weaponIds;

        /// <param name="weaponIds">スロット順の武器の Id。渡した列は複製して持つ。null は空として扱う。</param>
        public NetLoadoutRequest(IReadOnlyList<string> weaponIds)
        {
            if (weaponIds == null)
            {
                _weaponIds = Array.Empty<string>();
                return;
            }

            _weaponIds = new string[weaponIds.Count];
            for (var i = 0; i < weaponIds.Count; i++)
            {
                _weaponIds[i] = weaponIds[i] ?? string.Empty;
            }
        }

        /// <summary>スロット順の武器の Id。既定値では空。</summary>
        public IReadOnlyList<string> WeaponIds => _weaponIds ?? Array.Empty<string>();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            var count = _weaponIds?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
            {
                // 本数はクライアントが決めるので、配列を確保する前に上限で切る
                if (count < 0 || count > NetPlayerWeapon.MaxWeaponCount)
                {
                    throw new InvalidOperationException(
                        "loadout request has " + count + " weapons but at most " + NetPlayerWeapon.MaxWeaponCount + " are allowed");
                }

                _weaponIds = new string[count];
            }

            for (var i = 0; i < count; i++)
            {
                var id = _weaponIds[i] ?? string.Empty;
                serializer.SerializeValue(ref id);
                _weaponIds[i] = id;
            }
        }
    }
}
