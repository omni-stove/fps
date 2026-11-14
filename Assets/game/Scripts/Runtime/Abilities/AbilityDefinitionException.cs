using System;

namespace Fps.Runtime
{
    /// <summary><see cref="AbilityDefinition"/> の内容が不正で変換できない。</summary>
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
