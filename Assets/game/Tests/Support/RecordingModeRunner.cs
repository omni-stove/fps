using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// <see cref="ModeRunnerBase"/> のフックが呼ばれた順を記録する。モードは <see cref="Factory"/> で組み立てる。
    /// </summary>
    public sealed class RecordingModeRunner : ModeRunnerBase
    {
        public Func<ModeSession> Factory { get; set; }

        public List<string> Events { get; } = new List<string>();

        /// <summary><see cref="ModeStatus.Detail"/> に入れるもの。</summary>
        public object Detail { get; set; }

        protected override object HudDetail => Detail;

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
    }
}
