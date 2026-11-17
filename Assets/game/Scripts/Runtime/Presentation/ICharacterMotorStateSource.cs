using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// キャラの本体が、移動の Core の今の状態 (<see cref="CharacterMotorState"/>) を見た目 (<see cref="CharacterAnimator"/>・<see cref="PlayerMockBody"/>) に見せる。
    /// 見た目はここからリーン・水泳・壁走りを読む。本体と同じ GameObject に置けば、見た目の部品が自分で見つける。
    /// </summary>
    public interface ICharacterMotorStateSource
    {
        /// <summary>状態を作った移動の調整値。リーンで体を傾け切る角度 (<see cref="MotorSpec.LeanAngleDegrees"/>) をここから読む。null にはしない。</summary>
        MotorSpec Spec { get; }

        /// <summary>今の状態があれば state に入れて true を返す。まだ動き出していなければ false。</summary>
        bool TryGetMotorState(out CharacterMotorState state);
    }

    internal static class CharacterMotorStateSources
    {
        // 破棄された部品はインターフェイスの参照のままでは == null にならないので、UnityEngine.Object として比べる。
        public static bool IsMissing(ICharacterMotorStateSource source)
        {
            return source == null || (source is UnityEngine.Object unityObject && unityObject == null);
        }
    }
}
