using System;
using System.Collections.Generic;
using Fps.Runtime;

namespace Fps.Tests.EditMode
{
    // ScriptableObject.CreateInstance が型を見つけられるよう、ファイル名と同じ名前の最上位の型にする。
    public sealed class RecordingAbilityDefinition : AbilityDefinition
    {
        public readonly List<string> Calls = new List<string>();

        public bool Allows = true;

        public bool KeepRunning = true;

        public bool ThrowOnTick;

        public bool ReturnNull;

        public IMovementOverride Movement;

        public override IAbility CreateAbility()
        {
            return ReturnNull ? null : new Recording(this);
        }

        private sealed class Recording : IAbility
        {
            private readonly RecordingAbilityDefinition _owner;

            public Recording(RecordingAbilityDefinition owner)
            {
                _owner = owner;
            }

            public bool CanUse(AbilityContext context)
            {
                return _owner.Allows;
            }

            public void Begin(AbilityContext context)
            {
                _owner.Calls.Add("begin");
                if (_owner.Movement != null)
                {
                    context.OverrideMovement(_owner.Movement);
                }
            }

            public bool Tick(AbilityContext context, float deltaSeconds)
            {
                _owner.Calls.Add(context.EffectStartedThisFrame ? "tick:effect" : "tick:" + context.Phase);
                if (_owner.ThrowOnTick)
                {
                    throw new InvalidOperationException("boom");
                }

                return _owner.KeepRunning;
            }

            public void End(AbilityContext context, bool interrupted)
            {
                _owner.Calls.Add(interrupted ? "end:interrupted" : "end");
            }
        }
    }
}
