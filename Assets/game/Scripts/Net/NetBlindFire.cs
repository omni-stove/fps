using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary><see cref="NetBlindFire.Resolve"/> の結果。</summary>
    public enum BlindFireAim
    {
        /// <summary>ブラインドファイアにならない (ルールが Off・カバーの外・覗いている)。ふだんどおり目から撃つ。</summary>
        Normal,

        /// <summary>ブラインドファイアになる。壁の上か端の横から撃ち、散らばりを足す。</summary>
        Blind,

        /// <summary>ブラインドファイアになるが、高い壁で端にいないので撃てない。</summary>
        Blocked,
    }

    /// <summary>
    /// ネット対戦のブラインドファイア。オフラインの <see cref="Fps.Runtime.BlindFireController"/> と同じく、カバー中で覗いていないときの射撃を
    /// 低い壁では壁の上から、高い壁では端の横から (<see cref="BlindFireShot.TryResolveOrigin"/>)、武器の <see cref="WeaponSpec.BlindFireSpreadDegrees"/> で散らして撃つ。
    /// 始点はカバーの状態 (<see cref="NetCoverState"/>) から決め、サーバーは自分の確定状態を使ってクライアントの始点を信じない。
    /// 散らばりは乱数ではなく種 (プレイヤー Id・サーバーが数えた発射番号) で決め、持ち主の予測 (曳光弾) とサーバーの判定を揃える。
    /// 両側とも端の柱で出る側は、オフラインのように肩ではなく、カバーの状態が持つ横の入力の好み (<see cref="NetCoverState.PreferredSide"/>) で決める
    /// (肩は持ち主の画面にしか無いため)。
    /// </summary>
    public static class NetBlindFire
    {
        /// <summary>
        /// 今撃つとブラインドファイアになるかと、その始点。
        /// </summary>
        /// <param name="enabled">ルールでブラインドファイアが使えるか (<see cref="NetMatchRules.BlindFireEnabled"/>)。</param>
        /// <param name="cover">撃つ人のカバーの状態。</param>
        /// <param name="eye">撃つ人の目 (ふだんの射撃の始点)。</param>
        /// <param name="bodyRadius">撃つ人の体の半径 (m)。0 以上の有限値。</param>
        /// <param name="origin"><see cref="BlindFireAim.Blind"/> ならブラインドファイアの始点、それ以外は eye。</param>
        /// <exception cref="ArgumentOutOfRangeException">eye か bodyRadius が有限でないか、bodyRadius が負のとき。</exception>
        public static BlindFireAim Resolve(bool enabled, NetCoverState cover, Vector3 eye, float bodyRadius, out Vector3 origin)
        {
            origin = eye;
            if (!enabled || !cover.IsInCover || cover.IsPeeking)
            {
                return BlindFireAim.Normal;
            }

            if (!cover.IsValid)
            {
                // 検査を通った状態しか来ないはずだが、壊れた法線で例外にするより撃たせない
                return BlindFireAim.Blocked;
            }

            if (!BlindFireShot.TryResolveOrigin(
                    cover.Height,
                    cover.Edges,
                    cover.PreferredSide,
                    NetMath.ToNumerics(cover.Normal),
                    cover.TopY,
                    bodyRadius,
                    NetMath.ToNumerics(eye),
                    out var resolved))
            {
                return BlindFireAim.Blocked;
            }

            origin = NetMath.ToUnity(resolved);
            return BlindFireAim.Blind;
        }

        /// <summary>
        /// 武器のばらつきを掛けた後の direction に、spec の <see cref="WeaponSpec.BlindFireSpreadDegrees"/> の散らばりを種 (playerId・shotIndex) で足す
        /// (<see cref="BlindFireShot.Spread(System.Numerics.Vector3, float, int, int)"/>)。持ち主の予測とサーバーの判定で同じこの計算を通す。
        /// </summary>
        /// <exception cref="ArgumentNullException">spec が null のとき。</exception>
        /// <exception cref="ArgumentOutOfRangeException">direction が有限でないか長さ 0 のとき。</exception>
        public static Vector3 Spread(Vector3 direction, WeaponSpec spec, int playerId, int shotIndex)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return NetMath.ToUnity(BlindFireShot.Spread(NetMath.ToNumerics(direction), spec.BlindFireSpreadDegrees, playerId, shotIndex));
        }

        /// <summary>サーバーがブラインドファイアを判定したときのログ。例: <c>[Net] blind-fire attacker=1 index=3 spread=8 origin=(0.00,1.25,0.10)</c>。</summary>
        public static string FormatLog(int attackerId, int shotIndex, float spreadDegrees, Vector3 origin)
        {
            return "[Net] blind-fire attacker=" + attackerId.ToString(CultureInfo.InvariantCulture)
                + " index=" + shotIndex.ToString(CultureInfo.InvariantCulture)
                + " spread=" + spreadDegrees.ToString("0.##", CultureInfo.InvariantCulture)
                + " origin=(" + origin.x.ToString("F2", CultureInfo.InvariantCulture)
                + "," + origin.y.ToString("F2", CultureInfo.InvariantCulture)
                + "," + origin.z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }
    }
}
