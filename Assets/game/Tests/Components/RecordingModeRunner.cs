using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class RecordingModeRunner : ModeRunnerBase
    {
        public Func<ModeSession> Factory { get; set; }

        public List<string> Events { get; } = new List<string>();

        public object Detail { get; set; }

        protected override object HudDetail => Detail;

        protected override ModeSession CreateSession() => Factory?.Invoke();

        public void EndMatchFor(Side? winner) => EndMatch(winner);

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
