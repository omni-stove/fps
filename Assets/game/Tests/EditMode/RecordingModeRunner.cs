using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="ModeRunnerBase"/> のフックが呼ばれた順を記録する。モードは <see cref="Factory"/> で組み立てる。
    /// </summary>
    public sealed class RecordingModeRunner : ModeRunnerBase
    {
        public Func<ModeSession> Factory { get; set; }

        public List<string> Events { get; } = new List<string>();

        public string ExtraHudLine { get; set; }

        public LocalizedText ExtraHudText { get; set; }

        protected override ModeSession CreateSession() => Factory?.Invoke();

        protected override void OnKill(SideMember killer, SideMember victim)
        {
            Events.Add("kill " + (killer != null ? killer.name : "-") + " " + victim.name);
        }

        protected override void OnSpawned(SideMember member)
        {
            Events.Add("spawn " + member.name);
        }

        protected override void OnEnded(Side? winner)
        {
            Events.Add("ended " + (winner.HasValue ? winner.Value.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) : "draw"));
        }

        protected override void AppendHudLines(List<string> lines)
        {
            if (!string.IsNullOrEmpty(ExtraHudLine))
            {
                lines.Add(ExtraHudLine);
            }
        }

        protected override void AppendHudTexts(List<LocalizedText> lines)
        {
            if (!ExtraHudText.IsEmpty)
            {
                lines.Add(ExtraHudText);
            }
        }
    }
}
