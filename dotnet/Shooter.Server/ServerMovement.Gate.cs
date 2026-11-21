using Shooter.Core;

namespace Shooter.Server
{
    // 入力を絞る口 (スタミナ・よろけ)。無ければ入力をそのまま使う
    internal sealed partial class ServerMovement
    {
        internal IServerInputGate InputGate { get; set; }

        private CharacterMotorInput Restrain(Mover mover, CharacterMotorInput input)
        {
            var gate = InputGate;
            return gate == null ? input : gate.Restrain(mover.Player.PlayerId, input);
        }

        private void Stepped(Mover mover, CharacterMotorInput input, CharacterMotorState previous, CharacterMotorState current)
        {
            InputGate?.Stepped(mover.Player.PlayerId, input, previous, current, _tickSeconds);
        }
    }
}
