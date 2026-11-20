using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 一覧を値で出さずに集める口にするのは、相手は補間で毎フレーム動き、見通しも見る目の位置で変わるため。
    public interface INameplateContacts
    {
        // 手元のプレイヤー自身は入れない。
        void CopyTo(Vector3 eye, List<NameplateContact> into);
    }
}
