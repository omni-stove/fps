using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// NetPlayer がリグの姿勢の読み元 (<see cref="ICharacterPoseSource"/>) として同期値から状態を作ること、
    /// チームの色をリグの材質の複製に塗ること。プレハブは Fps.Editor.NetSceneBuilder.Build の出力をそのまま使う。
    /// </summary>
    public sealed class NetPlayerPoseTests
    {
        private GameObject _instance;
        private Material[] _copies = new Material[0];

        [TearDown]
        public void TearDown()
        {
            foreach (var copy in _copies)
            {
                if (copy != null)
                {
                    Object.DestroyImmediate(copy);
                }
            }

            _copies = new Material[0];
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
            }
        }

        [Test]
        public void MovingFasterThanWalkingWhileStandingCountsAsSprinting()
        {
            var walking = NetPlayer.PoseInput(5f, 5f, true, false, false, null, false, true, WeaponCategory.AssaultRifle);
            var running = NetPlayer.PoseInput(7f, 5f, true, false, false, null, false, true, WeaponCategory.AssaultRifle);
            var crouching = NetPlayer.PoseInput(7f, 5f, true, true, false, null, false, true, null);

            Assert.That(walking.IsSprinting, Is.False);
            Assert.That(running.IsSprinting, Is.True);
            Assert.That(crouching.IsSprinting, Is.False, "crouching never runs");

            var pose = new CharacterPose(5f, 8f, 2.5f);
            Assert.That(pose.Evaluate(walking).Speed, Is.EqualTo(CharacterPose.WalkSpeedValue));
            Assert.That(pose.Evaluate(running).Speed, Is.GreaterThan(CharacterPose.WalkSpeedValue));
        }

        [Test]
        public void PoseInputCarriesTheSyncedState()
        {
            var input = NetPlayer.PoseInput(0f, 5f, false, true, true, null, true, true, WeaponCategory.Handgun);

            Assert.That(input.IsGrounded, Is.False);
            Assert.That(input.IsCrouching, Is.True);
            Assert.That(input.IsAiming, Is.True);
            Assert.That(input.IsDowned, Is.True);
            Assert.That(input.IsDead, Is.False);
            Assert.That(input.IsInCover, Is.False);
            Assert.That(input.WeaponCategory, Is.EqualTo(WeaponCategory.Handgun));

            Assert.That(NetPlayer.PoseInput(0f, 5f, true, false, false, null, false, false, null).IsDead, Is.True, "no health means dead");
        }

        [Test]
        public void PoseInputCarriesTheSyncedCoverHeight()
        {
            var low = NetPlayer.PoseInput(0f, 5f, true, true, false, CoverHeight.Low, false, true, WeaponCategory.AssaultRifle);
            var high = NetPlayer.PoseInput(0f, 5f, true, false, false, CoverHeight.High, false, true, WeaponCategory.AssaultRifle);

            Assert.That(low.IsInCover, Is.True);
            Assert.That(low.CoverHeight, Is.EqualTo(CoverHeight.Low));
            Assert.That(high.CoverHeight, Is.EqualTo(CoverHeight.High));
        }

        [Test]
        public void CharacterAnimatorReadsThePlayerPrefabThroughThePoseSource()
        {
            var player = InstantiatePlayer();
            var motor = player.GetComponent<NetworkPlayerMotor>();
            var animator = player.GetComponent<CharacterAnimator>();
            Assert.That(animator, Is.Not.Null, "NetPlayer has no CharacterAnimator; run Fps.Editor.NetSceneBuilder.Build");

            Assert.That(animator.TryBind(), Is.True);

            Assert.That(animator.Source, Is.EqualTo(CharacterAnimatorSource.Network));
            Assert.That(animator.Pose.WalkSpeed, Is.EqualTo(motor.WalkSpeed));
            Assert.That(animator.Pose.SprintSpeed, Is.EqualTo(motor.SprintSpeed));
            Assert.That(animator.Pose.CrouchSpeed, Is.EqualTo(motor.CrouchSpeed));

            // 参加前 (体力 0) は倒れている扱い。見た目はそもそも NetPlayer が消している
            var input = player.ReadPose(1f);
            Assert.That(input.HorizontalSpeed, Is.EqualTo(1f));
            Assert.That(input.IsDead, Is.True);
            Assert.That(input.IsAiming, Is.False);
            Assert.That(input.WeaponCategory, Is.Null, "no weapon until the server equips one");
        }

        [Test]
        public void OfflineAndNetworkPlayersReadTheSameMotorSettings()
        {
            var settings = ScriptableObject.CreateInstance<MotorSettings>();
            var offline = new GameObject("OfflinePlayer");
            try
            {
                settings.WalkSpeed = 4f;
                settings.SprintSpeed = 9f;
                settings.CrouchSpeed = 1.5f;
                offline.AddComponent<CharacterController>();
                var mover = offline.AddComponent<PlayerController>();
                mover.MotorSettings = settings;

                var player = InstantiatePlayer();
                var motor = player.GetComponent<NetworkPlayerMotor>();
                motor.MotorSettings = settings;
                var animator = player.GetComponent<CharacterAnimator>();
                Assert.That(animator.TryBind(), Is.True);

                Assert.That(motor.WalkSpeed, Is.EqualTo(mover.WalkSpeed).And.EqualTo(4f));
                Assert.That(motor.SprintSpeed, Is.EqualTo(mover.SprintSpeed).And.EqualTo(9f));
                Assert.That(motor.CrouchSpeed, Is.EqualTo(mover.CrouchSpeed).And.EqualTo(1.5f));
                Assert.That(player.WalkSpeed, Is.EqualTo(4f));
                Assert.That(player.CrouchSpeed, Is.EqualTo(1.5f));
                Assert.That(animator.Pose.WalkSpeed, Is.EqualTo(4f));
                Assert.That(animator.Pose.SprintSpeed, Is.EqualTo(9f));
                Assert.That(animator.Pose.CrouchSpeed, Is.EqualTo(1.5f));
            }
            finally
            {
                Object.DestroyImmediate(offline);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CharacterAnimatorUsesTheRigInThePrefab()
        {
            var player = InstantiatePlayer();
            var animator = player.GetComponent<CharacterAnimator>();
            var placed = player.transform.Find(CharacterAnimator.RigObjectName);
            animator.TryBind();

            Assert.That(animator.BuildRig(), Is.True);

            Assert.That(animator.Rig, Is.SameAs(placed));
            Assert.That(animator.OwnsRig, Is.False);
            Assert.That(player.GetComponentsInChildren<Animator>(true), Has.Length.EqualTo(1), "no second rig from Resources");

            animator.Dismantle();
        }

        [Test]
        public void TeamColorIsPaintedOnCopiesOfTheRigMaterials()
        {
            var player = InstantiatePlayer();
            var renderers = player.GetComponentsInChildren<Renderer>(true);
            var originals = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            Assert.That(originals, Is.Not.Empty);
            var originalColors = originals.Select(m => m.color).ToArray();

            _copies = NetPlayer.ReplaceWithCopies(renderers);

            // 元の材質ごとに 1 つだけ作り、部品の数だけは作らない
            Assert.That(_copies, Has.Length.EqualTo(originals.Length));
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.sharedMaterials.All(m => _copies.Contains(m)), Is.True, renderer.name);
            }

            foreach (var copy in _copies)
            {
                copy.color = NetPlayer.TeamColor(Team.Bravo);
            }

            // リグのプレハブの共有の材質 (他のプレイヤーと敵も使う) は塗らない
            Assert.That(originals.Select(m => m.color), Is.EqualTo(originalColors));

            // 自分のプレイヤーがいない (観戦だけ) ときは、Alpha を味方・Bravo を敵の色にする
            Assert.That(NetPlayer.LocalTeam, Is.Null);
            Assert.That(NetPlayer.TeamColor(Team.Bravo), Is.EqualTo(TeamColors.ToColor(TeamColors.Current.Enemy)));
            Assert.That(NetPlayer.TeamColor(Team.Alpha), Is.EqualTo(TeamColors.ToColor(TeamColors.Current.Friendly)));
        }

        private NetPlayer InstantiatePlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetSceneBuilder.PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null, NetSceneBuilder.PlayerPrefabPath + " is missing; run Fps.Editor.NetSceneBuilder.Build");

            _instance = Object.Instantiate(prefab);
            var player = _instance.GetComponent<NetPlayer>();
            Assert.That(player, Is.Not.Null);
            return player;
        }
    }
}
