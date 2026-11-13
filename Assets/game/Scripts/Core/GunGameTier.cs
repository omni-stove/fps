using System;

namespace Fps.Core
{
    /// <summary>ガンゲームの武器の並び (<see cref="GunGameLadder"/>) の 1 段。持たせる武器の Id と、HUD とログに出す名前。</summary>
    public readonly struct GunGameTier
    {
        // default では null になるので、読むときに空文字や Id へ寄せる。
        private readonly string _weaponId;
        private readonly string _displayName;

        /// <summary>武器の Id (<see cref="WeaponSpec.Id"/>)。前後の空白を除いた値。</summary>
        public string WeaponId => _weaponId ?? string.Empty;

        /// <summary>武器の名前。生成時に空なら <see cref="WeaponId"/>。</summary>
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? WeaponId : _displayName;

        /// <param name="weaponId">武器の Id。空白だけは不可。</param>
        /// <param name="displayName">武器の名前。null か空白なら Id を使う。</param>
        /// <exception cref="ArgumentException">weaponId が null か空白。</exception>
        public GunGameTier(string weaponId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                throw new ArgumentException("a gun game tier needs a weapon id", nameof(weaponId));
            }

            _weaponId = weaponId.Trim();
            _displayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        }
    }
}
