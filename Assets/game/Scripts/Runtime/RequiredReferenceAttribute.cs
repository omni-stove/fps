using System;

namespace Shooter.Runtime
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class RequiredReferenceAttribute : Attribute
    {
    }
}
