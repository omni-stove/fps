using System.Collections.Generic;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class FakeMeleeWitness : MonoBehaviour, IMeleeWitness
    {
        public string WitnessId { get; set; } = "witness";

        public bool IsUnaware { get; set; } = true;

        public string StageName { get; set; } = "Unaware";

        public Damageable Watching { get; set; }

        public string StageAfterWitness { get; set; } = "Engaged";

        public List<float> Calls { get; } = new List<float>();

        string IMeleeWitness.Id => WitnessId;

        public bool Witness(Damageable attacker, float awareness)
        {
            Calls.Add(awareness);
            if (Watching == null || Watching != attacker)
            {
                return false;
            }

            IsUnaware = false;
            StageName = StageAfterWitness;
            return true;
        }
    }
}
