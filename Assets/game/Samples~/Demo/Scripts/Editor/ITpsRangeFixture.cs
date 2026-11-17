using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 射撃場を作るときに ExtensionPoints.CreateAll がリフレクションで見つけるので、実装は public な具象クラスにして引数なしのコンストラクターを持たせる。
    public interface ITpsRangeFixture
    {
        int Order { get; }

        // smoke の手順が GameObject.Find で探せるよう、射撃場の組み込みの物や、ほかの置く物と重ならない名前にする。
        string Name { get; }

        // 射撃場の床 (TpsRangeSceneBuilder.FloorHalfExtent) に収め、開けた場所やほかの物と重ねない (TpsRangeSceneBuilder.Footprints で確かめる)。
        Bounds Footprint { get; }

        GameObject Place(Scene scene, GameRules rules);
    }
}
