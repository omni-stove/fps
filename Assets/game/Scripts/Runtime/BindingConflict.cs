namespace Shooter.Runtime
{
    public readonly struct BindingConflict
    {
        public BindingConflict(string actionName, string part, int bindingIndex)
        {
            ActionName = actionName;
            Part = part;
            BindingIndex = bindingIndex;
        }

        public string ActionName { get; }

        public string Part { get; }

        public int BindingIndex { get; }

        public override string ToString()
        {
            return Part == null ? $"{ActionName}[{BindingIndex}]" : $"{ActionName}/{Part}[{BindingIndex}]";
        }
    }
}
