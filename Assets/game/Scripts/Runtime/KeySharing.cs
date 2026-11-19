using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // Input System は同じキーに付いた二つのアクションを両方動かすので、key-sharing.json の prefer の側が「今この押下を使う」と名乗っている間、もう一方の押下を読む側に譲らせる。
    public static class KeySharing
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static readonly Dictionary<string, HashSet<object>> _claims = new Dictionary<string, HashSet<object>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> _releasedFrame = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static KeySharingSpec _spec;

        // 譲った押下は離すまで譲り続ける。押している間に箱が開くなどして prefer の側が名乗りをやめても、そこから振り始めないようにするため。
        private static readonly HashSet<InputAction> _yieldedUntilRelease = new HashSet<InputAction>();

        public static KeySharingSpec Spec
        {
            get => _spec ?? KitTuning.KeySharing;
            set => _spec = value;
        }

        public static string ActionNameOf(string actionOrPath)
        {
            if (string.IsNullOrWhiteSpace(actionOrPath))
            {
                throw new ArgumentException("an action name such as Interact or a path such as Player/Interact is required", nameof(actionOrPath));
            }

            var trimmed = actionOrPath.Trim();
            var slash = trimmed.LastIndexOf('/');
            return slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;
        }

        public static void Claim(string action, object source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var name = ActionNameOf(action);
            if (!_claims.TryGetValue(name, out var sources))
            {
                sources = new HashSet<object>();
                _claims[name] = sources;
            }

            sources.Add(source);
        }

        public static void Release(string action, object source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var name = ActionNameOf(action);
            if (_claims.TryGetValue(name, out var sources) && sources.Remove(source))
            {
                // Interact の押下は Input System の更新の中で使われ、Pickup などがその場で Claim を外す。その後に読む近接やリロードがまだ譲れるよう、外したフレームの間は名乗りを残す。
                _releasedFrame[name] = Time.frameCount;
            }
        }

        public static void SetClaim(string action, object source, bool claimed)
        {
            if (claimed)
            {
                Claim(action, source);
            }
            else
            {
                Release(action, source);
            }
        }

        public static bool IsClaimed(string action)
        {
            var name = ActionNameOf(action);
            if (_claims.TryGetValue(name, out var sources) && sources.Count > 0)
            {
                return true;
            }

            // EditMode のテストは frameCount が進まないので、再生中だけ猶予を使う。
            return Application.isPlaying && _releasedFrame.TryGetValue(name, out var frame) && frame == Time.frameCount;
        }

        public static bool Yields(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (_yieldedUntilRelease.Contains(action))
            {
                if (action.IsPressed())
                {
                    return true;
                }

                _yieldedUntilRelease.Remove(action);
                return false;
            }

            if (!Yields(action, action.activeControl))
            {
                return false;
            }

            if (action.IsPressed())
            {
                _yieldedUntilRelease.Add(action);
            }

            return true;
        }

        public static bool Yields(InputAction action, InputControl control)
        {
            if (action == null || control == null)
            {
                return false;
            }

            var preferred = Spec.YieldsTo(action.name);
            if (preferred.Count == 0)
            {
                return false;
            }

            var map = action.actionMap;
            foreach (var name in preferred)
            {
                if (!IsClaimed(name))
                {
                    continue;
                }

                var other = map != null ? map.FindAction(name) : null;
                if (other != null && BindsTo(other, control))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool WasPressedThisFrame(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            // この関数は押下のないフレームにも呼ばれる。ここで外さないと、離したあとも譲った印が残り、次の押下を Claim なしでも譲ってしまう。
            if (!action.IsPressed())
            {
                _yieldedUntilRelease.Remove(action);
            }

            if (!action.WasPressedThisFrame())
            {
                return false;
            }

            // 新しい押下は、前の押下で譲った印を引き継がず判定し直す。
            _yieldedUntilRelease.Remove(action);

            if (!Yields(action))
            {
                return true;
            }

            Debug.Log(KeySharingLog.Yielded(action.name, action.activeControl?.path, Spec.YieldsTo(action.name)));
            return false;
        }

        public static bool IsPressed(InputAction action)
        {
            return action != null && action.IsPressed() && !Yields(action);
        }

        private static bool BindsTo(InputAction action, InputControl control)
        {
            foreach (var bound in action.controls)
            {
                if (bound == control)
                {
                    return true;
                }
            }

            return false;
        }

        public static void Reset()
        {
            _claims.Clear();
            _releasedFrame.Clear();
            _yieldedUntilRelease.Clear();
            _spec = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Reset();
        }
    }
}
