using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>弾が当たったことを照準の周りに出す印。</summary>
    public interface IHitMarker
    {
        /// <summary>confirm の命中の印を出し直す。HUD の量で印を出さないときは何もしない。</summary>
        void Show(HitConfirm confirm);
    }
}
