using Shooter.Core;

namespace Shooter.Runtime
{
    public interface ICharacterMotorStateSource
    {
        MotorSpec Spec { get; }

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
