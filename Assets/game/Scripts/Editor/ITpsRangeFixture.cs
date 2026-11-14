using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場 (<see cref="TpsRangeSceneBuilder"/>) に置く物 1 つ分。public な具象クラスで引数なしのコンストラクターを持てば、
    /// 射撃場を作るときに <see cref="ExtensionPoints.CreateAll{T}"/> が見つけて <see cref="Order"/> の順に <see cref="Place"/> を呼ぶので、
    /// 新しいアクションの試し場所はこれを実装した型を 1 つ足すだけで置ける。
    /// </summary>
    public interface ITpsRangeFixture
    {
        /// <summary>置く順。小さいほど先。同じ値なら型の完全名の順。</summary>
        int Order { get; }

        /// <summary>
        /// <see cref="Place"/> が返す最上位の GameObject の名前。smoke の手順が <see cref="GameObject.Find(string)"/> で探せるよう、
        /// 射撃場の組み込みの物や、ほかの置く物と重ならない名前にする。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 置く物が床の上で占める範囲 (ワールド座標)。射撃場の床 (<see cref="TpsRangeSceneBuilder.FloorHalfExtent"/>) に収め、
        /// 開けた場所 (<see cref="TpsRangeSceneBuilder.OpenAreaCenter"/>) やほかの物と重ねない (<see cref="TpsRangeSceneBuilder.Footprints()"/> で確かめる)。
        /// </summary>
        Bounds Footprint { get; }

        /// <summary>
        /// scene の最上位に <see cref="Name"/> の GameObject を作って置き、それを返す。子はその下に置く。
        /// rules は射撃場に配線するルールで、使うかどうかで見た目や振る舞いを変えてよい。
        /// </summary>
        GameObject Place(Scene scene, GameRules rules);
    }
}
