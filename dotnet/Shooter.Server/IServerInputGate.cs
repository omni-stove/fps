using Shooter.Core;

namespace Shooter.Server
{
    // 移動のモジュールがほかのモジュールを知らずに、入力を体へ当てる前に絞らせ、当てた後の動きを知らせる口。
    // どちらも入力 1 つごとに、ServerHost の 1 つのスレッドから呼ぶ
    internal interface IServerInputGate
    {
        CharacterMotorInput Restrain(int playerId, CharacterMotorInput input);

        void Stepped(int playerId, CharacterMotorInput input, CharacterMotorState previous, CharacterMotorState current, float deltaSeconds);
    }
}
