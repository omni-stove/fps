using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="CharacterRigBuilder"/> を一時フォルダに走らせて、プレハブ・コントローラー・クリップの中身と、
    /// 2 回目の実行でファイルが変わらないことを確かめる。リポジトリの Assets/game/Characters は触らない。
    /// </summary>
    public sealed class CharacterRigBuilderTests
    {
        private const float _footTolerance = 0.1f;

        private string _folder;
        private string _prefabPath;
        private string _controllerPath;
        private string _clipFolder;
        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__CharacterRigBuilderTests_" + Guid.NewGuid().ToString("N");
            _prefabPath = _folder + "/CharacterRig.prefab";
            _controllerPath = _folder + "/CharacterRig.controller";
            _clipFolder = _folder + "/Clips";
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void DefaultPathsAreUnderGameCharacters()
        {
            Assert.That(CharacterRigBuilder.PrefabPath, Is.EqualTo("Assets/game/Characters/CharacterRig.prefab"));
            Assert.That(CharacterRigBuilder.ControllerPath, Is.EqualTo("Assets/game/Characters/CharacterRig.controller"));
            Assert.That(CharacterRigBuilder.ClipFolder, Is.EqualTo("Assets/game/Characters/Clips"));
        }

        [Test]
        public void BuildsAPrefabWithBonesMeshesAndAnimatorButNoColliders()
        {
            var prefab = Build();

            Assert.That(prefab, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath)));
            var animator = prefab.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath)));
            Assert.That(animator.applyRootMotion, Is.False);

            foreach (var bone in CharacterRigBuilder.BoneNames)
            {
                Assert.That(prefab.transform.Find(CharacterRigBuilder.BonePath(bone)), Is.Not.Null, bone);
            }

            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(CharacterRigBuilder.PartNames.Count));
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.name);
                Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null, renderer.name);
            }

            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty, "the rig is visual only; hit boxes stay on the character");
        }

        [Test]
        public void ControllerHasThePoseParameters()
        {
            Build();
            var controller = LoadController();

            var parameters = new Dictionary<string, AnimatorControllerParameterType>();
            foreach (var parameter in controller.parameters)
            {
                parameters.Add(parameter.name, parameter.type);
            }

            Assert.That(parameters, Is.EquivalentTo(new Dictionary<string, AnimatorControllerParameterType>
            {
                { CharacterPose.SpeedParameter, AnimatorControllerParameterType.Float },
                { CharacterPose.CrouchParameter, AnimatorControllerParameterType.Bool },
                { CharacterPose.AimParameter, AnimatorControllerParameterType.Bool },
                { CharacterPose.CoverParameter, AnimatorControllerParameterType.Int },
                { CharacterPose.DownedParameter, AnimatorControllerParameterType.Bool },
                { CharacterPose.DeadParameter, AnimatorControllerParameterType.Bool },
                { CharacterPose.WeaponCategoryParameter, AnimatorControllerParameterType.Int },
            }));
        }

        [Test]
        public void ControllerHasEveryStateWithAClip()
        {
            Build();
            var machine = LoadController().layers[0].stateMachine;

            var names = new List<string>();
            foreach (var child in machine.states)
            {
                names.Add(child.state.name);
                Assert.That(child.state.motion, Is.Not.Null, child.state.name);
            }

            Assert.That(names, Is.EqualTo(CharacterRigBuilder.StateNames));
            Assert.That(machine.defaultState.name, Is.EqualTo(CharacterRigBuilder.LocomotionState));

            var locomotion = (BlendTree)State(machine, CharacterRigBuilder.LocomotionState).motion;
            Assert.That(locomotion.blendParameter, Is.EqualTo(CharacterPose.SpeedParameter));
            Assert.That(locomotion.children, Has.Length.EqualTo(3));
            Assert.That(locomotion.children[0].motion.name, Is.EqualTo(CharacterRigClips.Idle));
            Assert.That(locomotion.children[1].motion.name, Is.EqualTo(CharacterRigClips.Walk));
            Assert.That(locomotion.children[2].motion.name, Is.EqualTo(CharacterRigClips.Run));
            Assert.That(locomotion.children[2].threshold, Is.EqualTo(CharacterPose.RunSpeedValue));
        }

        [Test]
        public void EveryPoseReachesExactlyTheExpectedState()
        {
            Build();
            var machine = LoadController().layers[0].stateMachine;
            var pose = new CharacterPose(5f, 8f, 2.5f);
            var covers = new CoverHeight?[] { null, CoverHeight.Low, CoverHeight.High };
            var speeds = new[] { 0f, 3f, 8f };

            foreach (var speed in speeds)
            {
                foreach (var cover in covers)
                {
                    for (var bits = 0; bits < 64; bits++)
                    {
                        var input = new CharacterPoseInput(
                            speed,
                            (bits & 1) != 0,
                            (bits & 2) != 0,
                            (bits & 4) != 0,
                            (bits & 8) != 0,
                            cover,
                            (bits & 16) != 0,
                            (bits & 32) != 0,
                            WeaponCategory.Handgun);
                        var parameters = pose.Evaluate(input);

                        var matched = new List<string>();
                        foreach (var transition in machine.anyStateTransitions)
                        {
                            if (Matches(transition.conditions, parameters))
                            {
                                matched.Add(transition.destinationState.name);
                            }
                        }

                        Assert.That(matched, Is.EqualTo(new[] { ExpectedState(parameters) }), input.ToString());
                    }
                }
            }
        }

        [Test]
        public void AnyStateTransitionsDoNotRestartTheCurrentState()
        {
            Build();
            var machine = LoadController().layers[0].stateMachine;

            Assert.That(machine.anyStateTransitions, Has.Length.EqualTo(CharacterRigBuilder.StateNames.Count));
            foreach (var transition in machine.anyStateTransitions)
            {
                Assert.That(transition.canTransitionToSelf, Is.False, transition.destinationState.name);
                Assert.That(transition.hasExitTime, Is.False, transition.destinationState.name);
            }
        }

        [Test]
        public void ClipsAnimateOnlyBonesOfTheRig()
        {
            var prefab = Build();

            foreach (var spec in CharacterRigClips.All())
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(_clipFolder + "/" + spec.Name + ".anim");
                Assert.That(clip, Is.Not.Null, spec.Name);
                Assert.That(clip.isLooping, Is.EqualTo(spec.Loop), spec.Name);
                Assert.That(clip.length, Is.EqualTo(spec.Length).Within(1e-4f), spec.Name);

                var bindings = AnimationUtility.GetCurveBindings(clip);
                Assert.That(bindings, Is.Not.Empty, spec.Name);
                foreach (var binding in bindings)
                {
                    Assert.That(AnimationUtility.GetAnimatedObject(prefab, binding), Is.Not.Null, $"{spec.Name}: {binding.path}");
                }
            }
        }

        [TestCase(CharacterRigClips.Idle)]
        [TestCase(CharacterRigClips.Walk)]
        [TestCase(CharacterRigClips.Run)]
        [TestCase(CharacterRigClips.Crouch)]
        [TestCase(CharacterRigClips.Aim)]
        [TestCase(CharacterRigClips.CrouchAim)]
        [TestCase(CharacterRigClips.CoverLow)]
        [TestCase(CharacterRigClips.CoverHigh)]
        public void UprightPosesKeepAFootOnTheGround(string clipName)
        {
            var rig = SampleRig(clipName);

            var lowest = Mathf.Min(
                rig.transform.Find(CharacterRigBuilder.BonePath(CharacterRigBuilder.LeftShinBone) + "/LeftFootMesh").position.y,
                rig.transform.Find(CharacterRigBuilder.BonePath(CharacterRigBuilder.RightShinBone) + "/RightFootMesh").position.y);
            Assert.That(lowest, Is.EqualTo(0f).Within(_footTolerance));
        }

        [Test]
        public void CrouchAndCoverLowAreLowerThanStanding()
        {
            var standing = HeadHeight(CharacterRigClips.Idle);

            Assert.That(standing, Is.GreaterThan(1.5f));
            Assert.That(HeadHeight(CharacterRigClips.Crouch), Is.LessThan(standing - 0.3f));
            Assert.That(HeadHeight(CharacterRigClips.CoverLow), Is.LessThan(HeadHeight(CharacterRigClips.Crouch)));
        }

        [Test]
        public void DownedSitsAndDeadLiesFlat()
        {
            var downed = HeadHeight(CharacterRigClips.Downed);

            Assert.That(downed, Is.InRange(0.3f, 1f));
            Assert.That(HeadHeight(CharacterRigClips.Dead), Is.LessThan(0.4f));
        }

        [Test]
        public void RebuildingKeepsGuidsAndFileContents()
        {
            Build();
            var paths = new List<string> { _prefabPath, _controllerPath };
            foreach (var spec in CharacterRigClips.All())
            {
                paths.Add(_clipFolder + "/" + spec.Name + ".anim");
            }

            var guids = new List<string>();
            var contents = new List<string>();
            foreach (var path in paths)
            {
                guids.Add(AssetDatabase.AssetPathToGUID(path));
                contents.Add(File.ReadAllText(path));
            }

            Build();

            for (var i = 0; i < paths.Count; i++)
            {
                Assert.That(AssetDatabase.AssetPathToGUID(paths[i]), Is.EqualTo(guids[i]), paths[i]);
                Assert.That(File.ReadAllText(paths[i]), Is.EqualTo(contents[i]), paths[i]);
            }
        }

        [Test]
        public void RejectsInvalidPaths()
        {
            Assert.Throws<ArgumentException>(() => CharacterRigBuilder.BuildAt("CharacterRig.prefab", _controllerPath, _clipFolder));
            Assert.Throws<ArgumentException>(() => CharacterRigBuilder.BuildAt(_folder + "/CharacterRig.asset", _controllerPath, _clipFolder));
            Assert.Throws<ArgumentException>(() => CharacterRigBuilder.BuildAt(_prefabPath, _folder + "/CharacterRig.asset", _clipFolder));
            Assert.Throws<ArgumentException>(() => CharacterRigBuilder.BuildAt(_prefabPath, _controllerPath, "Clips"));
            Assert.Throws<ArgumentException>(() => CharacterRigBuilder.BuildAt(_prefabPath, _controllerPath, _folder + "/../Clips"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False, "nothing is written for invalid paths");
        }

        [Test]
        public void RefusesToOverwriteAnotherKindOfAsset()
        {
            // クリップのパスにフォルダを置くと、AnimationClip ではないアセットが既にある状態になる。
            SceneBuildHelpers.EnsureFolder(_clipFolder + "/" + CharacterRigClips.Idle + ".anim");

            Assert.Throws<InvalidOperationException>(() => Build());
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_prefabPath), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_controllerPath), Is.Null);
        }

        private GameObject Build()
        {
            return CharacterRigBuilder.BuildAt(_prefabPath, _controllerPath, _clipFolder);
        }

        private AnimatorController LoadController()
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
        }

        private GameObject SampleRig(string clipName)
        {
            if (AssetDatabase.LoadMainAssetAtPath(_prefabPath) == null)
            {
                Build();
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath));
            _spawned.Add(rig);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(_clipFolder + "/" + clipName + ".anim");
            clip.SampleAnimation(rig, 0f);
            return rig;
        }

        private float HeadHeight(string clipName)
        {
            var rig = SampleRig(clipName);
            return rig.transform.Find(CharacterRigBuilder.BonePath(CharacterRigBuilder.HeadBone) + "/HeadMesh").position.y;
        }

        private static AnimatorState State(AnimatorStateMachine machine, string name)
        {
            foreach (var child in machine.states)
            {
                if (child.state.name == name)
                {
                    return child.state;
                }
            }

            throw new KeyNotFoundException(name);
        }

        // CharacterPose の優先順を Animator の状態の名前で書き直したもの。コントローラーの条件と食い違えば上のテストが落ちる。
        private static string ExpectedState(CharacterPoseParameters parameters)
        {
            if (parameters.Dead)
            {
                return CharacterRigBuilder.DeadState;
            }

            if (parameters.Downed)
            {
                return CharacterRigBuilder.DownedState;
            }

            if (parameters.Aim)
            {
                return parameters.Crouch ? CharacterRigBuilder.CrouchAimState : CharacterRigBuilder.AimState;
            }

            if (parameters.Cover == CharacterPose.CoverLow)
            {
                return CharacterRigBuilder.CoverLowState;
            }

            if (parameters.Cover == CharacterPose.CoverHigh)
            {
                return CharacterRigBuilder.CoverHighState;
            }

            return parameters.Crouch ? CharacterRigBuilder.CrouchState : CharacterRigBuilder.LocomotionState;
        }

        private static bool Matches(AnimatorCondition[] conditions, CharacterPoseParameters parameters)
        {
            foreach (var condition in conditions)
            {
                if (!Matches(condition, parameters))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Matches(AnimatorCondition condition, CharacterPoseParameters parameters)
        {
            float value;
            switch (condition.parameter)
            {
                case CharacterPose.SpeedParameter:
                    value = parameters.Speed;
                    break;
                case CharacterPose.CrouchParameter:
                    value = parameters.Crouch ? 1f : 0f;
                    break;
                case CharacterPose.AimParameter:
                    value = parameters.Aim ? 1f : 0f;
                    break;
                case CharacterPose.CoverParameter:
                    value = parameters.Cover;
                    break;
                case CharacterPose.DownedParameter:
                    value = parameters.Downed ? 1f : 0f;
                    break;
                case CharacterPose.DeadParameter:
                    value = parameters.Dead ? 1f : 0f;
                    break;
                case CharacterPose.WeaponCategoryParameter:
                    value = parameters.WeaponCategory;
                    break;
                default:
                    throw new ArgumentException($"unknown parameter '{condition.parameter}'");
            }

            switch (condition.mode)
            {
                case AnimatorConditionMode.If:
                    return value != 0f;
                case AnimatorConditionMode.IfNot:
                    return value == 0f;
                case AnimatorConditionMode.Equals:
                    return value == condition.threshold;
                case AnimatorConditionMode.NotEqual:
                    return value != condition.threshold;
                case AnimatorConditionMode.Greater:
                    return value > condition.threshold;
                case AnimatorConditionMode.Less:
                    return value < condition.threshold;
                default:
                    throw new ArgumentException($"unknown condition mode {condition.mode}");
            }
        }
    }
}
