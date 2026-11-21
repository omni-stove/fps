using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 人の入力を予算の内で 1 つずつ体に当て、動いた結果を共有の体と検知へ渡す
    internal sealed partial class ServerMovement
    {
        private const float _warningIntervalSeconds = 1f;

        private void Receive(Mover mover, InputMessage input)
        {
            for (var i = 0; i < input.Count; i++)
            {
                var frame = input[i];
                if (frame.Tick <= mover.LastProcessedTick)
                {
                    continue;
                }

                if (!CharacterMotorInput.IsValid(frame.Move, frame.Yaw))
                {
                    Warn(mover, "[Net] dropped non-finite input from client=" + Text(mover.Player.Connection) + " tick=" + Text(frame.Tick));
                    continue;
                }

                if (mover.InputBudget < 1f)
                {
                    // 警告の行は間引くが、違反の累積は間引かずに数えるので、間引く前に出す
                    _signals?.RaiseInputOverBudget(mover.Player.PlayerId);
                    Warn(
                        mover,
                        "[Net] dropped input from client=" + Text(mover.Player.Connection) + " tick=" + Text(frame.Tick)
                            + ": sending faster than the tick rate");
                    return;
                }

                mover.InputBudget -= 1f;
                Apply(mover, frame);

                // Bot の入力 (ApplyBotInput) は出さない。サーバーが作る向きを検知に数えても意味が無いため。
                // 撃った pitch は 1 つの入力にだけ載せる。続く入力にも載せると、撃っていない間の縦の向きを古い値で偽るため
                var pitch = mover.ShotPitch;
                mover.ShotPitch = null;
                _signals?.RaiseLooked(mover.Player.PlayerId, frame.Tick, mover.Yaw, pitch);
            }
        }

        private void Apply(Mover mover, InputFrame frame)
        {

            var input = new CharacterMotorInput(
                    frame.Move,
                    frame.Yaw,
                    frame.Has(InputButtons.Sprint),
                    frame.Has(InputButtons.Jump),
                    frame.Has(InputButtons.Crouch),
                    mover.Downed,
                    frame.Has(InputButtons.Cover),
                    frame.Has(InputButtons.Aim),
                    frame.Has(InputButtons.Dodge),
                    false,
                    frame.Has(InputButtons.Prone),
                    false,
                    false,
                    CarrySpeedMultiplier(mover),
                    lean: LeanInput.Combine(frame.Has(InputButtons.LeanLeft), frame.Has(InputButtons.LeanRight)),
                    openParachute: frame.Has(InputButtons.OpenParachute),
                    closeParachute: frame.Has(InputButtons.CloseParachute))
                .WithRules(_settings.Rules.Actions);

            if (mover.Riding)
            {
                mover.Yaw = input.Yaw;
                mover.Aiming = input.Restrained().Aim;
                mover.LastProcessedTick = frame.Tick;
                Publish(mover);
                return;
            }

            var previous = mover.State;
            var mantled = TryMantle(mover, frame.Tick, input);
            var current = mantled ? mover.State : mover.Simulation.Step(mover.State, input);
            mover.State = current;
            mover.Yaw = input.Yaw;
            mover.Aiming = input.Restrained().Aim;
            mover.LastProcessedTick = frame.Tick;
            Publish(mover);
            Report(mover, frame.Tick, previous, current, mantled);
        }

        private float CarrySpeedMultiplier(Mover mover)
        {
            return _context.Bodies.TryGet(mover.Player.PlayerId, out var body)
                ? _settings.Motor.CarryLoad.SpeedMultiplier(body.HeldWeapon, body.CarriedWeapons, body.CarriedItemWeight)
                : 1f;
        }

        private void Warn(Mover mover, string text)
        {
            if (_now < mover.NextWarning)
            {
                return;
            }

            mover.NextWarning = _now + _warningIntervalSeconds;
            _log.WriteLine(text);
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Text(ConnectionId connection)
        {
            return connection.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
