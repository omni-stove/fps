using System;

namespace Shooter.Runtime
{
    public sealed class AbilityDefinitionException : Exception
    {
        public AbilityDefinitionException(string message)
            : base(message)
        {
        }

        public AbilityDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
