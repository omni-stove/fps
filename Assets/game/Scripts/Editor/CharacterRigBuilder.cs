using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// プリミティブで胴・頭・腕・脚を組んだ人型のリグを作るか上書きする。
    /// <list type="bullet">
    /// <item><see cref="PrefabPath"/>: 関節 (空の GameObject) とその子の見た目 (MeshFilter と MeshRenderer) を組み、根に Animator を付けたプレハブ。
    /// <see cref="Fps.Runtime.CharacterAnimator"/> が Resources から読み、プレイヤーと敵の子に置く。
    /// 見た目だけのリグなので、コライダーは付けない (当たり判定は今までどおり Hitbox が持つ)。</item>
    /// <item><see cref="ClipFolder"/>: <see cref="CharacterRigClips"/> の姿勢ごとの AnimationClip。関節の回転と腰の位置をコードでキーにする。</item>
    /// <item><see cref="ControllerPath"/>: クリップを <see cref="CharacterPose"/> のパラメーターでつなぐ AnimatorController。</item>
    /// </list>
    /// 既存のアセットは GUID と中のオブジェクトを保って中身だけ書き換えるので、何度実行しても同じ結果になり、git diff が出ない。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.CharacterRigBuilder.Build</code>
    /// </summary>
    public static class CharacterRigBuilder
    {
        public const string Folder = "Assets/game/Characters";
        // CharacterAnimator が実行時に Resources.Load で読むので、プレハブだけ Resources の下に置く。
        public const string PrefabPath = Folder + "/Resources/" + Fps.Runtime.CharacterAnimator.RigResourcePath + ".prefab";
        public const string ControllerPath = Folder + "/CharacterRig.controller";
        public const string ClipFolder = Folder + "/Clips";

        public const string MenuPath = "Fps/Build Character Rig";
        public const string LogTag = "[CharacterRigBuilder]";

        public const string BaseLayerName = "Base Layer";

        public const string HipsBone = "Hips";
        public const string TorsoBone = "Torso";
        public const string HeadBone = "Head";
        public const string LeftUpperArmBone = "LeftUpperArm";
        public const string LeftForearmBone = "LeftForearm";
        public const string RightUpperArmBone = "RightUpperArm";
        public const string RightForearmBone = "RightForearm";
        public const string LeftThighBone = "LeftThigh";
        public const string LeftShinBone = "LeftShin";
        public const string RightThighBone = "RightThigh";
        public const string RightShinBone = "RightShin";

        public const string LocomotionState = "Locomotion";
        public const string CrouchState = "Crouch";
        public const string AimState = "Aim";
        public const string CrouchAimState = "CrouchAim";
        public const string CoverLowState = "CoverLow";
        public const string CoverHighState = "CoverHigh";
        public const string DownedState = "Downed";
        public const string DeadState = "Dead";

        /// <summary>ふつうの姿勢の間を移る秒数。</summary>
        public const float PoseTransitionSeconds = 0.15f;

        /// <summary>ダウンへ崩れ落ちる秒数。</summary>
        public const float DownedTransitionSeconds = 0.3f;

        /// <summary>死亡で倒れ込む秒数。</summary>
        public const float DeadTransitionSeconds = 0.25f;

        // Euler 角のままキーにすると、Animation ウィンドウと同じく Euler で補間され、180° を越える曲げでも反対回りにならない。
        private const string RotationProperty = "localEulerAnglesRaw";
        private const string PositionProperty = "m_LocalPosition";

        // 周期の動きは 1 周を 16 等分してキーにする。接線は sin の微分をそのまま入れるので、これ以上細かくしても見た目は変わらない。
        private const int SamplesPerClip = 16;

        private const float ClipFrameRate = 60f;

        private static readonly BoneSpec[] Bones =
        {
            new BoneSpec(HipsBone, null, new Vector3(0f, CharacterRigClips.StandingHipsHeight, 0f)),
            new BoneSpec(TorsoBone, HipsBone, new Vector3(0f, 0.05f, 0f)),
            new BoneSpec(HeadBone, TorsoBone, new Vector3(0f, 0.57f, 0f)),
            new BoneSpec(LeftUpperArmBone, TorsoBone, new Vector3(-0.27f, 0.52f, 0f)),
            new BoneSpec(LeftForearmBone, LeftUpperArmBone, new Vector3(0f, -0.3f, 0f)),
            new BoneSpec(RightUpperArmBone, TorsoBone, new Vector3(0.27f, 0.52f, 0f)),
            new BoneSpec(RightForearmBone, RightUpperArmBone, new Vector3(0f, -0.3f, 0f)),
            new BoneSpec(LeftThighBone, HipsBone, new Vector3(-0.11f, 0f, 0f)),
            new BoneSpec(LeftShinBone, LeftThighBone, new Vector3(0f, -0.44f, 0f)),
            new BoneSpec(RightThighBone, HipsBone, new Vector3(0.11f, 0f, 0f)),
            new BoneSpec(RightShinBone, RightThighBone, new Vector3(0f, -0.44f, 0f)),
        };

        // 関節は下 (手足) か上 (胴・頭) へ伸びる部位の付け根に置き、見た目はその途中に置く。足の裏は立ったとき足元 (0) に付く。
        private static readonly PartSpec[] Parts =
        {
            new PartSpec("PelvisMesh", HipsBone, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.16f, 0.2f)),
            new PartSpec("TorsoMesh", TorsoBone, PrimitiveType.Cube, new Vector3(0f, 0.275f, 0f), new Vector3(0.42f, 0.55f, 0.22f)),
            new PartSpec("HeadMesh", HeadBone, PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0f), Vector3.one * 0.26f),
            new PartSpec("LeftUpperArmMesh", LeftUpperArmBone, PrimitiveType.Cube, new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.3f, 0.1f)),
            new PartSpec("LeftForearmMesh", LeftForearmBone, PrimitiveType.Cube, new Vector3(0f, -0.14f, 0f), new Vector3(0.09f, 0.28f, 0.09f)),
            new PartSpec("RightUpperArmMesh", RightUpperArmBone, PrimitiveType.Cube, new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.3f, 0.1f)),
            new PartSpec("RightForearmMesh", RightForearmBone, PrimitiveType.Cube, new Vector3(0f, -0.14f, 0f), new Vector3(0.09f, 0.28f, 0.09f)),
            new PartSpec("LeftThighMesh", LeftThighBone, PrimitiveType.Cube, new Vector3(0f, -0.22f, 0f), new Vector3(0.15f, 0.44f, 0.15f)),
            new PartSpec("LeftShinMesh", LeftShinBone, PrimitiveType.Cube, new Vector3(0f, -0.23f, 0f), new Vector3(0.12f, 0.46f, 0.12f)),
            new PartSpec("LeftFootMesh", LeftShinBone, PrimitiveType.Cube, new Vector3(0f, -0.485f, 0.05f), new Vector3(0.12f, 0.05f, 0.22f)),
            new PartSpec("RightThighMesh", RightThighBone, PrimitiveType.Cube, new Vector3(0f, -0.22f, 0f), new Vector3(0.15f, 0.44f, 0.15f)),
            new PartSpec("RightShinMesh", RightShinBone, PrimitiveType.Cube, new Vector3(0f, -0.23f, 0f), new Vector3(0.12f, 0.46f, 0.12f)),
            new PartSpec("RightFootMesh", RightShinBone, PrimitiveType.Cube, new Vector3(0f, -0.485f, 0.05f), new Vector3(0.12f, 0.05f, 0.22f)),
        };

        /// <summary>関節の名前 (根に近い順)。</summary>
        public static IReadOnlyList<string> BoneNames
        {
            get
            {
                var names = new string[Bones.Length];
                for (var i = 0; i < Bones.Length; i++)
                {
                    names[i] = Bones[i].Name;
                }

                return names;
            }
        }

        /// <summary>見た目の部品の名前。</summary>
        public static IReadOnlyList<string> PartNames
        {
            get
            {
                var names = new string[Parts.Length];
                for (var i = 0; i < Parts.Length; i++)
                {
                    names[i] = Parts[i].Name;
                }

                return names;
            }
        }

        /// <summary>Animator の状態の名前 (コントローラーに並ぶ順)。</summary>
        public static IReadOnlyList<string> StateNames => new[]
        {
            LocomotionState,
            CrouchState,
            AimState,
            CrouchAimState,
            CoverLowState,
            CoverHighState,
            DownedState,
            DeadState,
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(PrefabPath, ControllerPath, ClipFolder);
        }

        /// <summary>
        /// clipFolder にクリップを、controllerPath にコントローラーを、prefabPath にプレハブを作るか上書きして保存し、プレハブを返す。
        /// prefabPath は Assets/ 以下の .prefab、controllerPath は Assets/ 以下の .controller、clipFolder は Assets/ 以下のフォルダであること。
        /// パスに別の種類のアセットがあれば、何も書き換えずに例外を投げる。
        /// </summary>
        public static GameObject BuildAt(string prefabPath, string controllerPath, string clipFolder)
        {
            prefabPath = ValidatePath(prefabPath, ".prefab", nameof(prefabPath));
            controllerPath = ValidatePath(controllerPath, ".controller", nameof(controllerPath));
            clipFolder = ValidateFolder(clipFolder, nameof(clipFolder));

            var specs = CharacterRigClips.All();
            var clipPaths = new string[specs.Count];
            for (var i = 0; i < specs.Count; i++)
            {
                clipPaths[i] = clipFolder + "/" + specs[i].Name + ".anim";
            }

            // 書き換え始める前に、上書きしてはいけないものが無いことを確かめる。途中で止まって半端な組を残さないため。
            RequireTypeOrMissing<GameObject>(prefabPath);
            RequireTypeOrMissing<AnimatorController>(controllerPath);
            foreach (var clipPath in clipPaths)
            {
                RequireTypeOrMissing<AnimationClip>(clipPath);
            }

            ValidateClipSpecs(specs);

            var clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            for (var i = 0; i < specs.Count; i++)
            {
                clips.Add(specs[i].Name, BuildClip(clipPaths[i], specs[i]));
            }

            var controller = BuildController(controllerPath, clips);
            var prefab = BuildPrefab(prefabPath, controller, clips.Values);

            AssetDatabase.SaveAssets();
            Debug.Log($"{LogTag} saved '{prefabPath}', '{controllerPath}' and {clips.Count} clip(s) under '{clipFolder}'");
            return prefab;
        }

        /// <summary>根から bone までの、Animator から見たパス (例: Hips/Torso/Head)。bone が無ければ例外を投げる。</summary>
        public static string BonePath(string bone)
        {
            var segments = new List<string>();
            var current = bone;
            while (current != null)
            {
                var spec = FindBone(current);
                segments.Insert(0, spec.Name);
                current = spec.Parent;
            }

            return string.Join("/", segments);
        }

        private static BoneSpec FindBone(string name)
        {
            foreach (var bone in Bones)
            {
                if (string.Equals(bone.Name, name, StringComparison.Ordinal))
                {
                    return bone;
                }
            }

            throw new ArgumentException($"'{name}' is not a bone of the character rig", nameof(name));
        }

        private static void ValidateClipSpecs(IReadOnlyList<CharacterRigClipSpec> specs)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var spec in specs)
            {
                if (!names.Add(spec.Name))
                {
                    throw new InvalidOperationException($"clip name '{spec.Name}' is used more than once");
                }

                foreach (var bone in spec.RotatedBones)
                {
                    FindBone(bone);
                }
            }
        }

        // ---- clips ----

        private static AnimationClip BuildClip(string path, CharacterRigClipSpec spec)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var created = clip == null;
            if (created)
            {
                clip = new AnimationClip { name = spec.Name };
            }

            // 前の実行のカーブを残すと、関節の名前を変えたときに使われないカーブが溜まる。
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            }

            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            clip.frameRate = ClipFrameRate;

            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            AddVectorCurves(bindings, curves, BonePath(HipsBone), PositionProperty, spec.HipsPosition, spec.Length);
            foreach (var bone in Bones)
            {
                AddVectorCurves(bindings, curves, BonePath(bone.Name), RotationProperty, spec.Rotation(bone.Name), spec.Length);
            }

            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = spec.Loop;
            settings.loopBlend = false;
            settings.cycleOffset = 0f;
            settings.mirror = false;
            settings.startTime = 0f;
            settings.stopTime = spec.Length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (created)
            {
                SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
                AssetDatabase.CreateAsset(clip, path);
            }
            else
            {
                clip.name = spec.Name;
                EditorUtility.SetDirty(clip);
            }

            return clip;
        }

        private static void AddVectorCurves(
            List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, RigWave3 value, float length)
        {
            bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property + ".x"));
            curves.Add(Curve(value.X, length));
            bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property + ".y"));
            curves.Add(Curve(value.Y, length));
            bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property + ".z"));
            curves.Add(Curve(value.Z, length));
        }

        private static AnimationCurve Curve(RigWave wave, float length)
        {
            if (wave.IsConstant)
            {
                return new AnimationCurve(new Keyframe(0f, wave.Base, 0f, 0f), new Keyframe(length, wave.Base, 0f, 0f));
            }

            var keys = new Keyframe[SamplesPerClip + 1];
            var angularPerSecond = 2.0 * Math.PI * wave.Frequency / length;
            for (var i = 0; i <= SamplesPerClip; i++)
            {
                var normalized = (double)i / SamplesPerClip;
                var angle = 2.0 * Math.PI * ((wave.Frequency * normalized) + wave.Phase);
                var value = (float)(wave.Base + (wave.Amplitude * Math.Sin(angle)));
                var slope = (float)(wave.Amplitude * angularPerSecond * Math.Cos(angle));
                keys[i] = new Keyframe((float)(normalized * length), value, slope, slope);
            }

            return new AnimationCurve(keys);
        }

        // ---- controller ----

        private static AnimatorController BuildController(string path, IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                if (controller == null)
                {
                    throw new IOException($"Failed to create the animator controller '{path}'");
                }
            }

            controller.parameters = Parameters();
            var machine = SyncBaseLayer(controller);

            var stateSpecs = StateSpecs(clips);
            var states = SyncStates(controller, machine, stateSpecs);
            SyncAnyStateTransitions(machine, stateSpecs, states);

            machine.defaultState = states[LocomotionState];
            machine.anyStatePosition = new Vector3(50f, 160f, 0f);
            machine.entryPosition = new Vector3(50f, 40f, 0f);
            machine.exitPosition = new Vector3(50f, 400f, 0f);
            machine.parentStateMachinePosition = new Vector3(800f, 20f, 0f);

            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorControllerParameter[] Parameters()
        {
            return new[]
            {
                new AnimatorControllerParameter { name = CharacterPose.SpeedParameter, type = AnimatorControllerParameterType.Float, defaultFloat = CharacterPose.IdleSpeedValue },
                new AnimatorControllerParameter { name = CharacterPose.CrouchParameter, type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = CharacterPose.AimParameter, type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = CharacterPose.CoverParameter, type = AnimatorControllerParameterType.Int, defaultInt = CharacterPose.CoverNone },
                new AnimatorControllerParameter { name = CharacterPose.DownedParameter, type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = CharacterPose.DeadParameter, type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = CharacterPose.WeaponCategoryParameter, type = AnimatorControllerParameterType.Int, defaultInt = CharacterPose.NoWeapon },
            };
        }

        private static AnimatorStateMachine SyncBaseLayer(AnimatorController controller)
        {
            if (controller.layers.Length == 0)
            {
                controller.AddLayer(BaseLayerName);
            }

            for (var i = controller.layers.Length - 1; i >= 1; i--)
            {
                controller.RemoveLayer(i);
            }

            var layer = controller.layers[0];
            var machine = layer.stateMachine;
            if (machine == null)
            {
                machine = new AnimatorStateMachine { name = BaseLayerName, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(machine, controller);
            }

            machine.name = BaseLayerName;
            foreach (var child in machine.stateMachines)
            {
                machine.RemoveStateMachine(child.stateMachine);
            }

            foreach (var entry in machine.entryTransitions)
            {
                machine.RemoveEntryTransition(entry);
            }

            machine.behaviours = Array.Empty<StateMachineBehaviour>();

            layer.name = BaseLayerName;
            layer.stateMachine = machine;
            layer.avatarMask = null;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 1f;
            layer.iKPass = false;
            layer.syncedLayerIndex = -1;
            layer.syncedLayerAffectsTiming = false;
            controller.layers = new[] { layer };
            return machine;
        }

        private static StateSpec[] StateSpecs(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var alive = new[]
            {
                Condition(AnimatorConditionMode.IfNot, CharacterPose.DeadParameter),
                Condition(AnimatorConditionMode.IfNot, CharacterPose.DownedParameter),
            };

            // 条件はどの組み合わせでも 1 つの状態にしか当てはまらないようにする。手でパラメーターを変えても、Any State から
            // 2 つの遷移が取り合わない。
            return new[]
            {
                new StateSpec(
                    LocomotionState,
                    new Vector3(300f, 0f, 0f),
                    new[]
                    {
                        new BlendChild(clips[CharacterRigClips.Idle], CharacterPose.IdleSpeedValue),
                        new BlendChild(clips[CharacterRigClips.Walk], CharacterPose.WalkSpeedValue),
                        new BlendChild(clips[CharacterRigClips.Run], CharacterPose.RunSpeedValue),
                    },
                    PoseTransitionSeconds,
                    With(
                        alive,
                        Condition(AnimatorConditionMode.Equals, CharacterPose.CoverParameter, CharacterPose.CoverNone),
                        Condition(AnimatorConditionMode.IfNot, CharacterPose.AimParameter),
                        Condition(AnimatorConditionMode.IfNot, CharacterPose.CrouchParameter))),
                new StateSpec(
                    CrouchState,
                    new Vector3(300f, 80f, 0f),
                    new[]
                    {
                        new BlendChild(clips[CharacterRigClips.Crouch], CharacterPose.IdleSpeedValue),
                        new BlendChild(clips[CharacterRigClips.CrouchWalk], CharacterPose.WalkSpeedValue),
                    },
                    PoseTransitionSeconds,
                    With(
                        alive,
                        Condition(AnimatorConditionMode.Equals, CharacterPose.CoverParameter, CharacterPose.CoverNone),
                        Condition(AnimatorConditionMode.IfNot, CharacterPose.AimParameter),
                        Condition(AnimatorConditionMode.If, CharacterPose.CrouchParameter))),
                new StateSpec(
                    AimState,
                    new Vector3(300f, 160f, 0f),
                    clips[CharacterRigClips.Aim],
                    PoseTransitionSeconds,
                    With(
                        alive,
                        Condition(AnimatorConditionMode.If, CharacterPose.AimParameter),
                        Condition(AnimatorConditionMode.IfNot, CharacterPose.CrouchParameter))),
                new StateSpec(
                    CrouchAimState,
                    new Vector3(300f, 240f, 0f),
                    clips[CharacterRigClips.CrouchAim],
                    PoseTransitionSeconds,
                    With(
                        alive,
                        Condition(AnimatorConditionMode.If, CharacterPose.AimParameter),
                        Condition(AnimatorConditionMode.If, CharacterPose.CrouchParameter))),
                new StateSpec(
                    CoverLowState,
                    new Vector3(550f, 0f, 0f),
                    clips[CharacterRigClips.CoverLow],
                    PoseTransitionSeconds,
                    With(
                        alive,
                        Condition(AnimatorConditionMode.Equals, CharacterPose.CoverParameter, CharacterPose.CoverLow),
                        Condition(AnimatorConditionMode.IfNot, CharacterPose.AimParameter))),
                new StateSpec(
                    CoverHighState,
                    new Vector3(550f, 80f, 0f),
                    clips[CharacterRigClips.CoverHigh],
                    PoseTransitionSeconds,
                    With(
                        alive,
                        Condition(AnimatorConditionMode.Equals, CharacterPose.CoverParameter, CharacterPose.CoverHigh),
                        Condition(AnimatorConditionMode.IfNot, CharacterPose.AimParameter))),
                new StateSpec(
                    DownedState,
                    new Vector3(550f, 160f, 0f),
                    clips[CharacterRigClips.Downed],
                    DownedTransitionSeconds,
                    new[]
                    {
                        Condition(AnimatorConditionMode.IfNot, CharacterPose.DeadParameter),
                        Condition(AnimatorConditionMode.If, CharacterPose.DownedParameter),
                    }),
                new StateSpec(
                    DeadState,
                    new Vector3(550f, 240f, 0f),
                    clips[CharacterRigClips.Dead],
                    DeadTransitionSeconds,
                    new[] { Condition(AnimatorConditionMode.If, CharacterPose.DeadParameter) }),
            };
        }

        private static Dictionary<string, AnimatorState> SyncStates(AnimatorController controller, AnimatorStateMachine machine, StateSpec[] specs)
        {
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                wanted.Add(spec.Name);
            }

            var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            foreach (var child in machine.states)
            {
                if (child.state == null)
                {
                    continue;
                }

                if (wanted.Contains(child.state.name) && !states.ContainsKey(child.state.name))
                {
                    states.Add(child.state.name, child.state);
                    continue;
                }

                RemoveOwnedBlendTree(controller, child.state.motion);
                machine.RemoveState(child.state);
            }

            var ordered = new ChildAnimatorState[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                if (!states.TryGetValue(spec.Name, out var state))
                {
                    state = machine.AddState(spec.Name, spec.Position);
                    states.Add(spec.Name, state);
                }

                foreach (var transition in state.transitions)
                {
                    state.RemoveTransition(transition);
                }

                state.motion = SyncMotion(controller, state, spec);
                state.speed = 1f;
                state.speedParameterActive = false;
                state.cycleOffset = 0f;
                state.cycleOffsetParameterActive = false;
                state.mirror = false;
                state.mirrorParameterActive = false;
                state.timeParameterActive = false;
                state.iKOnFeet = false;
                state.writeDefaultValues = true;
                state.tag = string.Empty;
                state.behaviours = Array.Empty<StateMachineBehaviour>();
                EditorUtility.SetDirty(state);

                ordered[i] = new ChildAnimatorState { state = state, position = spec.Position };
            }

            machine.states = ordered;
            return states;
        }

        private static Motion SyncMotion(AnimatorController controller, AnimatorState state, StateSpec spec)
        {
            if (spec.Clip != null)
            {
                RemoveOwnedBlendTree(controller, state.motion);
                return spec.Clip;
            }

            var tree = state.motion as BlendTree;
            if (tree == null || !IsOwnedBy(tree, controller))
            {
                tree = new BlendTree { hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(tree, controller);
            }

            tree.name = spec.Name;
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = CharacterPose.SpeedParameter;
            tree.blendParameterY = CharacterPose.SpeedParameter;
            tree.useAutomaticThresholds = false;

            var children = new ChildMotion[spec.BlendChildren.Length];
            for (var i = 0; i < children.Length; i++)
            {
                children[i] = new ChildMotion
                {
                    motion = spec.BlendChildren[i].Clip,
                    threshold = spec.BlendChildren[i].Threshold,
                    position = Vector2.zero,
                    timeScale = 1f,
                    cycleOffset = 0f,
                    directBlendParameter = CharacterPose.SpeedParameter,
                    mirror = false,
                };
            }

            tree.children = children;
            tree.minThreshold = children[0].threshold;
            tree.maxThreshold = children[children.Length - 1].threshold;
            EditorUtility.SetDirty(tree);
            return tree;
        }

        private static void RemoveOwnedBlendTree(AnimatorController controller, Motion motion)
        {
            if (motion is BlendTree tree && IsOwnedBy(tree, controller))
            {
                AssetDatabase.RemoveObjectFromAsset(tree);
                Object.DestroyImmediate(tree, true);
            }
        }

        private static bool IsOwnedBy(Object subAsset, Object asset)
        {
            return string.Equals(AssetDatabase.GetAssetPath(subAsset), AssetDatabase.GetAssetPath(asset), StringComparison.Ordinal);
        }

        private static void SyncAnyStateTransitions(AnimatorStateMachine machine, StateSpec[] specs, IReadOnlyDictionary<string, AnimatorState> states)
        {
            var kept = new Dictionary<AnimatorState, AnimatorStateTransition>();
            foreach (var transition in machine.anyStateTransitions)
            {
                var destination = transition.destinationState;
                if (destination != null && states.TryGetValue(destination.name, out var state) && state == destination && !kept.ContainsKey(state))
                {
                    kept.Add(state, transition);
                    continue;
                }

                machine.RemoveAnyStateTransition(transition);
            }

            var ordered = new AnimatorStateTransition[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                var state = states[spec.Name];
                if (!kept.TryGetValue(state, out var transition))
                {
                    transition = machine.AddAnyStateTransition(state);
                }

                transition.name = string.Empty;
                transition.destinationState = state;
                transition.isExit = false;
                transition.mute = false;
                transition.solo = false;
                transition.hasExitTime = false;
                transition.exitTime = 0f;
                transition.hasFixedDuration = true;
                transition.duration = spec.TransitionSeconds;
                transition.offset = 0f;
                transition.interruptionSource = TransitionInterruptionSource.None;
                transition.orderedInterruption = true;

                // 今の状態へ戻る遷移を毎フレーム始めないよう、自分自身への遷移を止める。
                transition.canTransitionToSelf = false;
                transition.conditions = spec.Conditions;
                EditorUtility.SetDirty(transition);

                ordered[i] = transition;
            }

            machine.anyStateTransitions = ordered;
        }

        private static AnimatorCondition Condition(AnimatorConditionMode mode, string parameter, float threshold = 0f)
        {
            return new AnimatorCondition { mode = mode, parameter = parameter, threshold = threshold };
        }

        private static AnimatorCondition[] With(AnimatorCondition[] head, params AnimatorCondition[] tail)
        {
            var all = new AnimatorCondition[head.Length + tail.Length];
            head.CopyTo(all, 0);
            tail.CopyTo(all, head.Length);
            return all;
        }

        // ---- prefab ----

        private static GameObject BuildPrefab(string path, AnimatorController controller, IEnumerable<AnimationClip> clips)
        {
            var exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var rootName = Path.GetFileNameWithoutExtension(path);

            // 既存のプレハブは中身を開いて書き換える。作り直すと中のオブジェクトの fileID が変わり、2 回目の実行でも差分が出る。
            var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(rootName);
            try
            {
                var meshes = new PrimitiveMeshes();
                SyncRoot(root, rootName, controller);
                SyncChildren(root.transform, null, meshes);
                RequireBindingsResolve(root, clips);

                if (!exists)
                {
                    SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
                if (!success || saved == null)
                {
                    throw new IOException($"Failed to save the character rig prefab '{path}'");
                }

                return saved;
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void SyncRoot(GameObject root, string rootName, AnimatorController controller)
        {
            root.name = rootName;
            root.layer = 0;
            root.tag = "Untagged";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            RemoveComponentsExcept(root, typeof(Animator));

            var animator = GetOrAdd<Animator>(root);
            animator.runtimeAnimatorController = controller;
            animator.avatar = null;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        // parentBone が null なら根の直下 (根に近い関節)。関節の子は、子の関節を先に、見た目の部品を後に並べる。
        private static void SyncChildren(Transform parent, string parentBone, PrimitiveMeshes meshes)
        {
            var bones = new List<BoneSpec>();
            foreach (var bone in Bones)
            {
                if (string.Equals(bone.Parent, parentBone, StringComparison.Ordinal))
                {
                    bones.Add(bone);
                }
            }

            var parts = new List<PartSpec>();
            if (parentBone != null)
            {
                foreach (var part in Parts)
                {
                    if (string.Equals(part.Bone, parentBone, StringComparison.Ordinal))
                    {
                        parts.Add(part);
                    }
                }
            }

            var names = new List<string>();
            foreach (var bone in bones)
            {
                names.Add(bone.Name);
            }

            foreach (var part in parts)
            {
                names.Add(part.Name);
            }

            var children = KeepOnly(parent, names);

            for (var i = 0; i < bones.Count; i++)
            {
                var node = children[bones[i].Name];
                node.SetSiblingIndex(i);
                ResetNode(node, bones[i].LocalPosition, Vector3.one);
                RemoveComponentsExcept(node.gameObject);
                SyncChildren(node, bones[i].Name, meshes);
            }

            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var node = children[part.Name];
                node.SetSiblingIndex(bones.Count + i);
                ResetNode(node, part.LocalPosition, part.LocalScale);
                RemoveComponentsExcept(node.gameObject, typeof(MeshFilter), typeof(MeshRenderer));
                KeepOnly(node, Array.Empty<string>());

                GetOrAdd<MeshFilter>(node.gameObject).sharedMesh = meshes.MeshOf(part.Primitive);
                var renderer = GetOrAdd<MeshRenderer>(node.gameObject);
                renderer.sharedMaterials = new[] { meshes.MaterialOf(part.Primitive) };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.enabled = true;
            }
        }

        // names に無い子と、同じ名前の 2 つ目以降の子を消し、足りない子を作って、名前から子を引ける表を返す。
        private static Dictionary<string, Transform> KeepOnly(Transform parent, IReadOnlyList<string> names)
        {
            var wanted = new HashSet<string>(names, StringComparer.Ordinal);
            var found = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var doomed = new List<GameObject>();
            foreach (Transform child in parent)
            {
                if (wanted.Contains(child.name) && !found.ContainsKey(child.name))
                {
                    found.Add(child.name, child);
                }
                else
                {
                    doomed.Add(child.gameObject);
                }
            }

            foreach (var child in doomed)
            {
                Object.DestroyImmediate(child);
            }

            foreach (var name in names)
            {
                if (!found.ContainsKey(name))
                {
                    var node = new GameObject(name).transform;
                    node.SetParent(parent, false);
                    found.Add(name, node);
                }
            }

            return found;
        }

        private static void ResetNode(Transform node, Vector3 localPosition, Vector3 localScale)
        {
            node.gameObject.layer = 0;
            node.gameObject.tag = "Untagged";
            node.gameObject.SetActive(true);
            node.localPosition = localPosition;
            node.localRotation = Quaternion.identity;
            node.localScale = localScale;
        }

        private static void RemoveComponentsExcept(GameObject target, params Type[] allowed)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
            foreach (var component in target.GetComponents<Component>())
            {
                if (component == null || component is Transform)
                {
                    continue;
                }

                var keep = false;
                foreach (var type in allowed)
                {
                    if (type.IsInstanceOfType(component))
                    {
                        keep = true;
                        break;
                    }
                }

                if (!keep)
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        private static T GetOrAdd<T>(GameObject target)
            where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        // クリップのカーブがどれもプレハブの関節に当たることを確かめる。関節の名前を変えてクリップだけが古い名前を指すのを、保存前に止める。
        private static void RequireBindingsResolve(GameObject root, IEnumerable<AnimationClip> clips)
        {
            foreach (var clip in clips)
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (AnimationUtility.GetAnimatedObject(root, binding) == null)
                    {
                        throw new InvalidOperationException(
                            $"clip '{clip.name}' animates '{binding.path}' ({binding.propertyName}), which is not in the character rig");
                    }
                }
            }
        }

        // ---- paths ----

        private static string ValidatePath(string path, string extension, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("asset path must not be empty", parameterName);
            }

            var normalized = path.Trim().Replace('\\', '/');
            if (!normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || normalized.Length == extension.Length)
            {
                throw new ArgumentException($"asset path '{path}' must end with {extension}", parameterName);
            }

            RequireUnderAssets(normalized, path, parameterName);
            return normalized;
        }

        private static string ValidateFolder(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("folder path must not be empty", parameterName);
            }

            var normalized = path.Trim().Replace('\\', '/').TrimEnd('/');
            RequireUnderAssets(normalized, path, parameterName);
            return normalized;
        }

        private static void RequireUnderAssets(string normalized, string original, string parameterName)
        {
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"path '{original}' must be under Assets/", parameterName);
            }

            foreach (var segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException($"path '{original}' must not contain empty, '.' or '..' segments", parameterName);
                }
            }
        }

        private static void RequireTypeOrMissing<T>(string path)
            where T : Object
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is T))
            {
                throw new InvalidOperationException($"'{path}' is a {existing.GetType().Name}, not a {typeof(T).Name}; move it away first");
            }
        }

        private readonly struct BoneSpec
        {
            public string Name { get; }

            /// <summary>親の関節。根の直下なら null。</summary>
            public string Parent { get; }

            public Vector3 LocalPosition { get; }

            public BoneSpec(string name, string parent, Vector3 localPosition)
            {
                Name = name;
                Parent = parent;
                LocalPosition = localPosition;
            }
        }

        private readonly struct PartSpec
        {
            public string Name { get; }
            public string Bone { get; }
            public PrimitiveType Primitive { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 LocalScale { get; }

            public PartSpec(string name, string bone, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale)
            {
                Name = name;
                Bone = bone;
                Primitive = primitive;
                LocalPosition = localPosition;
                LocalScale = localScale;
            }
        }

        private readonly struct BlendChild
        {
            public AnimationClip Clip { get; }
            public float Threshold { get; }

            public BlendChild(AnimationClip clip, float threshold)
            {
                Clip = clip;
                Threshold = threshold;
            }
        }

        private sealed class StateSpec
        {
            public string Name { get; }
            public Vector3 Position { get; }

            /// <summary>1 本のクリップを流す状態のクリップ。ブレンドツリーなら null。</summary>
            public AnimationClip Clip { get; }

            /// <summary>Speed で混ぜるクリップ (しきい値の小さい順)。1 本のクリップなら null。</summary>
            public BlendChild[] BlendChildren { get; }

            public float TransitionSeconds { get; }
            public AnimatorCondition[] Conditions { get; }

            public StateSpec(string name, Vector3 position, AnimationClip clip, float transitionSeconds, AnimatorCondition[] conditions)
                : this(name, position, clip, null, transitionSeconds, conditions)
            {
            }

            public StateSpec(string name, Vector3 position, BlendChild[] blendChildren, float transitionSeconds, AnimatorCondition[] conditions)
                : this(name, position, null, blendChildren, transitionSeconds, conditions)
            {
            }

            private StateSpec(
                string name, Vector3 position, AnimationClip clip, BlendChild[] blendChildren, float transitionSeconds, AnimatorCondition[] conditions)
            {
                if (clip == null && (blendChildren == null || blendChildren.Length == 0))
                {
                    throw new ArgumentException($"state '{name}' needs a clip or blend children");
                }

                Name = name;
                Position = position;
                Clip = clip;
                BlendChildren = blendChildren;
                TransitionSeconds = transitionSeconds;
                Conditions = conditions;
            }
        }

        // プリミティブのメッシュと材質は、一時的に作ったプリミティブから借りる。組み込みのリソースを名前で引くより版の違いに強く、
        // 今の描画パイプラインの既定の材質がそのまま付く。
        private sealed class PrimitiveMeshes
        {
            private readonly Dictionary<PrimitiveType, Mesh> _meshes = new Dictionary<PrimitiveType, Mesh>();
            private readonly Dictionary<PrimitiveType, Material> _materials = new Dictionary<PrimitiveType, Material>();

            public Mesh MeshOf(PrimitiveType type)
            {
                Load(type);
                return _meshes[type];
            }

            public Material MaterialOf(PrimitiveType type)
            {
                Load(type);
                return _materials[type];
            }

            private void Load(PrimitiveType type)
            {
                if (_meshes.ContainsKey(type))
                {
                    return;
                }

                var temp = GameObject.CreatePrimitive(type);
                try
                {
                    var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
                    var material = temp.GetComponent<MeshRenderer>().sharedMaterial;
                    if (mesh == null || material == null)
                    {
                        throw new InvalidOperationException($"the {type} primitive has no mesh or material to copy");
                    }

                    _meshes.Add(type, mesh);
                    _materials.Add(type, material);
                }
                finally
                {
                    Object.DestroyImmediate(temp);
                }
            }
        }
    }
}
