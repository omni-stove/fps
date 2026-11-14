using System;

namespace Fps.Core
{
    /// <summary>ガンゲームの武器の並び (<see cref="GunGameLadder"/>) の 1 段。持たせる武器の Id と、HUD とログに出す名前のキー。</summary>
    public readonly struct GunGameTier
    {
        // default では null になるので、読むときに空文字や Id へ寄せる。
        private readonly string _weaponId;
        private readonly string _nameKey;

        /// <summary>武器の Id (<see cref="WeaponSpec.Id"/>)。前後の空白を除いた値。</summary>
        public string WeaponId => _weaponId ?? string.Empty;

        /// <summary>武器の名前を引く表のキー (<see cref="WeaponSpec.NameKey"/>)。生成時に空なら空文字。</summary>
        public string NameKey => _nameKey ?? string.Empty;

        /// <summary>HUD に出す武器の名前。<see cref="NameKey"/> があればそのキーの文、無ければ <see cref="WeaponId"/> をそのまま出す。</summary>
        public LocalizedText Name => NameKey.Length > 0 ? new LocalizedText(NameKey) : LocalizedText.Literal(WeaponId);

        /// <summary>ログに書く名前。<see cref="NameKey"/> があればキー、無ければ <see cref="WeaponId"/>。</summary>
        public string LogName => NameKey.Length > 0 ? NameKey : WeaponId;

        /// <param name="weaponId">武器の Id。空白だけは不可。</param>
        /// <param name="nameKey">武器の名前を引く表のキー。null か空白なら名前は Id になる。</param>
        /// <exception cref="ArgumentException">weaponId が null か空白。</exception>
        public GunGameTier(string weaponId, string nameKey)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                throw new ArgumentException("a gun game tier needs a weapon id", nameof(weaponId));
            }

            _weaponId = weaponId.Trim();
            _nameKey = string.IsNullOrWhiteSpace(nameKey) ? null : nameKey.Trim();
        }
    }
}
