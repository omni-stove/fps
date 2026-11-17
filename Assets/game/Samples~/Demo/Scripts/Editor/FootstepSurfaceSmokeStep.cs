using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    public sealed class FootstepSurfaceSmokeStep : ITpsActionSmokeStep
    {
        private static readonly string _surfaceLogPrefix = $"{CharacterAudio.FootstepLogTag} '{GameBootstrap.PlayerName}' surface ";
        private const string Arrow = " -> ";
        private const string FromSurface = " from " + nameof(FootstepClipSource.Surface) + ")";

        private readonly List<(string To, bool FromSurface)> _switches = new List<(string, bool)>();
        private Phase _phase;
        private PlayerController _mover;
        private WalkFilter _filter;

        private enum Phase
        {
            PlaceAtStart,
            Walk,
            Done,
        }

        public int Order => 100;

        public string Name => "footstep surfaces";

        public float TimeoutSeconds => 15f;

        public void Reset()
        {
            _switches.Clear();
            _phase = Phase.PlaceAtStart;
            _mover = null;
            _filter = null;
        }

        public void OnLog(string condition)
        {
            if (TryParseSwitch(condition, out var to, out var fromSurface))
            {
                _switches.Add((to, fromSurface));
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            var mover = world.Mover;
            if (mover == null || !mover.IsInitialized)
            {
                problem = "the player's PlayerController is not initialized";
                return false;
            }

            switch (_phase)
            {
                case Phase.PlaceAtStart:
                    problem = PlaceAtStart(world, mover);
                    return false;

                case Phase.Walk:
                    return Walk(world, out problem);

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            if (_mover != null && _filter != null && ReferenceEquals(_mover.MoveFilter, _filter))
            {
                _mover.MoveFilter = _filter.Inner;
            }

            _mover = null;
            _filter = null;
        }

        public static bool TryParseSwitch(string condition, out string to, out bool fromSurface)
        {
            to = null;
            fromSurface = false;
            if (condition == null || !condition.StartsWith(_surfaceLogPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var arrow = condition.IndexOf(Arrow, _surfaceLogPrefix.Length, StringComparison.Ordinal);
            if (arrow < 0)
            {
                return false;
            }

            var start = arrow + Arrow.Length;
            var end = condition.IndexOf(" (", start, StringComparison.Ordinal);
            if (end <= start)
            {
                return false;
            }

            to = condition.Substring(start, end - start);
            fromSurface = condition.EndsWith(FromSurface, StringComparison.Ordinal);
            return true;
        }

        // 板に乗る前の射撃場の床 (default) への切り替えや、前の手順で残った切り替えがあっても通すため、並びの完全一致ではなく順に含むかを見る。
        public static string CheckSwitches(IReadOnlyList<(string To, bool FromSurface)> switches, IReadOnlyList<string> expected)
        {
            var next = 0;
            foreach (var (to, fromSurface) in switches)
            {
                if (next < expected.Count && string.Equals(to, expected[next], StringComparison.Ordinal))
                {
                    if (!fromSurface)
                    {
                        return $"the footstep on '{to}' did not use that surface's sounds";
                    }

                    next++;
                }
            }

            return next == expected.Count
                ? null
                : $"the footsteps switched to [{Join(switches)}], expected [{string.Join(", ", expected)}] in order";
        }

        private string PlaceAtStart(BootstrapWorld world, PlayerController mover)
        {
            if (GameObject.Find(FootstepSurfaceFixture.FixtureName + "/" + FootstepSurfaceFixture.PlateName(0)) == null)
            {
                return $"the scene has no '{FootstepSurfaceFixture.FixtureName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (FootstepSurfaceCatalog.Load() == null)
            {
                return $"there is no footstep surface catalog at Resources/{FootstepSurfaceCatalog.ResourcePath}; rerun the presentation asset builder";
            }

            Teleport(world.Controller, FootstepSurfaceFixture.Start + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
            FaceForward(world);

            _mover = mover;
            _filter = new WalkFilter(mover.MoveFilter);
            mover.MoveFilter = _filter;
            _switches.Clear();
            _phase = Phase.Walk;
            return null;
        }

        private bool Walk(BootstrapWorld world, out string problem)
        {
            problem = null;
            if (world.Player.transform.position.z < FootstepSurfaceFixture.Finish.z)
            {
                return false;
            }

            Cleanup();
            problem = CheckSwitches(_switches, FootstepSurfaceFixture.SurfaceIds);
            if (problem != null)
            {
                return false;
            }

            Debug.Log($"{TpsRangeSmokeRun.LogTag} footsteps switched surfaces {string.Join(" -> ", FootstepSurfaceFixture.SurfaceIds)}");
            _phase = Phase.Done;
            return true;
        }

        private static string Join(IReadOnlyList<(string To, bool FromSurface)> switches)
        {
            var names = new string[switches.Count];
            for (var i = 0; i < switches.Count; i++)
            {
                names[i] = switches[i].To;
            }

            return string.Join(", ", names);
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(CharacterController controller, Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        // FirstPersonLook が自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。
        private static void FaceForward(BootstrapWorld world)
        {
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, 0f), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.identity;
            }
        }

        // 先に差し込まれていたもの (カバー) を捨てると、カバーの制限が外れたままになるので後から呼ぶ。
        private sealed class WalkFilter : IPlayerMoveFilter
        {
            public WalkFilter(IPlayerMoveFilter inner)
            {
                Inner = inner;
            }

            public IPlayerMoveFilter Inner { get; }

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                move = Vector2.up;
                sprint = false;
                crouch = false;
                jumpPressed = false;
                Inner?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
            }
        }
    }
}
