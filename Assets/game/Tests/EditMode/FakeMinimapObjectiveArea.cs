using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // AddComponent が型を見つけられるよう、ファイル名と同じ名前の最上位の型にする。
    public sealed class FakeMinimapObjectiveArea : MonoBehaviour, IMinimapObjective
    {
        public bool Shows = true;

        public Vector3 Position => transform.position;

        public bool ShowsOnMinimap => Shows;
    }
}
