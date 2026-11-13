namespace Fps.Runtime
{
    /// <summary>新しい割り当てと同じ入力を使っている、Player マップのほかの割り当て。<see cref="PlayerBindings.FindConflicts"/> が返す。</summary>
    public readonly struct BindingConflict
    {
        public BindingConflict(string actionName, string part, int bindingIndex, string label)
        {
            ActionName = actionName;
            Part = part;
            BindingIndex = bindingIndex;
            Label = label;
        }

        /// <summary>重なったアクションの名前 (例: "Reload")。</summary>
        public string ActionName { get; }

        /// <summary>合成の部品の名前 (例: "up")。合成でなければ null。</summary>
        public string Part { get; }

        /// <summary>重なったアクションの中の割り当ての番号。</summary>
        public int BindingIndex { get; }

        /// <summary>画面に出す名前 (例: "リロード")。</summary>
        public string Label { get; }

        public override string ToString()
        {
            return Part == null ? $"{ActionName}[{BindingIndex}]" : $"{ActionName}/{Part}[{BindingIndex}]";
        }
    }
}
