using System.Numerics;

namespace Shooter.Core
{
    // 相手が何で囮を出すか (乗り物のフレア・ほかの仕掛け) は場所で違うので、捕捉と誘導弾には問い合わせ方だけを渡す。
    public interface ILockOnDecoy
    {
        // seeker から target を狙う向きの近くに囮があれば、その囮を返す。
        bool TryLure(Vector3 seeker, Vector3 target, out int decoyId, out Vector3 decoyPoint);

        // 一度誘われた弾が近づくにつれ囮と相手の見える角度が開いても、囮を追い続けられるよう、囮の今の位置を Id で引く。消えていれば false。
        bool TryLocate(int decoyId, out Vector3 decoyPoint);
    }
}
