using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // スライディングは持ち主の予測と同じ CharacterMotorSimulation で滑らせる。使えるかはルールの actions.slide (CharacterMotorInput.WithRules) で、
    // 速さと減速はルールのファイルの motor に書き出した traversal の値 (ServerMotorSettings.Slide) で決める。
    // 滑り出したいかだけをクライアントの Slide ボタンで受け、走っている速さ・立っているか・接地はサーバーの知る状態で確かめる。
    internal sealed partial class ServerMovement
    {
        public bool IsSliding(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) && mover.State.IsSliding;
        }

        private static CharacterMotorInput WithSlideInput(CharacterMotorInput input, InputFrame frame)
        {
            return input.With(slide: frame.Has(InputButtons.Slide));
        }

        private void ConfigureSlide(CharacterMotorSimulation simulation)
        {
            simulation.Slide = _settings.Slide;
        }

        private void ReportSlide(int playerId, int tick, CharacterMotorSimulation simulation)
        {
            var start = simulation.LastSlideStart;
            if (start.IsSliding)
            {
                _log.WriteLine(ServerMovementLog.SlideStart(playerId, tick, start.Speed, start.Direction));
            }

            if (simulation.LastSlideEnd != SlideEnd.None)
            {
                _log.WriteLine(ServerMovementLog.SlideEnd(
                    playerId, tick, simulation.LastSlideEnd, simulation.LastSlideEndSpeed, simulation.LastSlideEndSeconds, simulation.LastSlideEndDistance));
            }
        }
    }
}
