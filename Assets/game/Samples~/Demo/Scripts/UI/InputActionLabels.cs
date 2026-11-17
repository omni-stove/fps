using System;

namespace Fps.Runtime
{
    public static class InputActionLabels
    {
        public const string ActionLabelKeyPrefix = "settings.input.action.";

        public const string MoveLabelKeyPrefix = "settings.input.move.";

        // 表に無い名前は、Input Actions に足したばかりのアクションでも行が出るよう、警告を出さずにアクション名をそのまま使う。
        public static string LabelOf(string actionName, string part = null)
        {
            if (part != null
                && string.Equals(actionName, "Move", StringComparison.Ordinal)
                && Localizer.TryGet(MoveLabelKeyPrefix + part.ToLowerInvariant(), out var direction))
            {
                return direction;
            }

            var label = actionName != null && Localizer.TryGet(ActionLabelKeyPrefix + actionName, out var text) ? text : actionName;
            return part == null ? label : label + " (" + part + ")";
        }

        public static string LabelOf(BindingConflict conflict)
        {
            return LabelOf(conflict.ActionName, conflict.Part);
        }
    }
}
