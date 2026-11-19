using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 起動時は覚えている Epic アカウントで入り、無ければ Device ID で入る。誰も入っていなければ入り直す。
    // 入ったユーザーは EOS のプラットフォームで共有するので、クラウドセーブ・ロビー・パーティが同じユーザーを使う。
    public sealed class AccountSignIn : IAccountControl
    {
        public const double DefaultRetrySeconds = 60d;

        private readonly IAccountBackend _backend;
        private AccountKind _kind;
        private AccountStage _stage;
        private AccountSignInFailure _failure;
        private AccountStatus _published;
        private bool _prepared;
        private bool _off;
        private bool _epicAvailable;

        // 覚えている Epic アカウントが無いと分かった後や、自分でサインアウトした後は、入り直しに Device ID を使う。
        private bool _preferDevice;
        private int _operation;
        private double _now;
        private double _retryAt;

        public AccountSignIn(IAccountBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _published = Status;
        }

        public event Action<AccountStatus> StatusChanged;

        // 使えると分かった時に 1 回だけ呼ぶ。画面に口を出すのはこの後にする。
        public event Action Prepared;

        public IAccountBackend Backend => _backend;

        public bool IsAvailable => _prepared && !_off;

        public bool IsOff => _off;

        public double RetrySeconds { get; set; } = DefaultRetrySeconds;

        public AccountStatus Status => new AccountStatus(_kind, _stage, _failure, _prepared && _epicAvailable);

        public void Tick(double now)
        {
            _now = now;
            if (_off)
            {
                return;
            }

            if (!_prepared)
            {
                if (!_backend.TryPrepare(out var epic, out var unavailable))
                {
                    _off = true;
                    Debug.Log(AccountLog.Off(string.IsNullOrEmpty(unavailable) ? "the account service is unavailable" : unavailable));
                    return;
                }

                _prepared = true;
                _epicAvailable = epic;
                Prepared?.Invoke();
                Publish();
            }

            _backend.Tick();
            SyncKind(true);
            if (_stage == AccountStage.Idle && _kind == AccountKind.None && _now >= _retryAt)
            {
                StartAutomatic();
            }
        }

        public bool SignInWithEpic()
        {
            if (_off || !Status.CanSignInWithEpic)
            {
                return false;
            }

            var operation = ++_operation;
            _stage = AccountStage.SigningIn;
            _failure = AccountSignInFailure.None;
            Publish();
            Debug.Log(AccountLog.EpicSignInStarted());
            _backend.SignInEpic(result =>
            {
                if (operation != _operation)
                {
                    return;
                }

                _stage = AccountStage.Idle;
                switch (result.Outcome)
                {
                    case AccountLoginOutcome.SignedIn:
                        _preferDevice = false;
                        _failure = AccountSignInFailure.None;
                        Debug.Log(AccountLog.SignedIn(AccountKind.Epic));
                        break;
                    case AccountLoginOutcome.Cancelled:
                        _failure = AccountSignInFailure.Cancelled;
                        Debug.Log(AccountLog.EpicSignInCancelled());
                        break;
                    default:
                        _failure = AccountSignInFailure.Failed;
                        Debug.LogWarning(AccountLog.EpicSignInFailed(result.Error ?? result.Outcome.ToString()));
                        break;
                }

                SyncKind(false);
                Publish();
            });
            return true;
        }

        public bool SignOut()
        {
            if (_off || !Status.CanSignOut)
            {
                return false;
            }

            var operation = ++_operation;
            _stage = AccountStage.SigningOut;
            _failure = AccountSignInFailure.None;
            Publish();
            _backend.SignOutEpic(error =>
            {
                if (operation != _operation)
                {
                    return;
                }

                _stage = AccountStage.Idle;
                _preferDevice = true;
                _retryAt = 0d;
                if (error == null)
                {
                    Debug.Log(AccountLog.SignedOut());
                }
                else
                {
                    Debug.LogWarning(AccountLog.SignOutFailed(error));
                }

                SyncKind(false);
                Publish();
            });
            return true;
        }

        // 前の Play の呼び戻しが後から返っても、今の状態に当てない。
        public void Dispose()
        {
            _operation++;
            _off = true;
            StatusChanged = null;
            Prepared = null;
        }

        private void StartAutomatic()
        {
            var operation = ++_operation;
            _stage = AccountStage.SigningIn;
            Publish();
            if (!_epicAvailable || _preferDevice)
            {
                StartDevice(operation);
                return;
            }

            Debug.Log(AccountLog.Restoring());
            _backend.RestoreEpic(result =>
            {
                if (operation != _operation)
                {
                    return;
                }

                switch (result.Outcome)
                {
                    case AccountLoginOutcome.SignedIn:
                        _stage = AccountStage.Idle;
                        Debug.Log(AccountLog.SignedIn(AccountKind.Epic));
                        SyncKind(false);
                        Publish();
                        return;
                    case AccountLoginOutcome.NoSavedLogin:
                        _preferDevice = true;
                        Debug.Log(AccountLog.NoSavedLogin(result.Error ?? result.Outcome.ToString()));
                        break;
                    default:
                        // 通信が切れていただけなら次の入り直しで戻れるよう、覚えている Epic アカウントはあきらめない。
                        Debug.LogWarning(AccountLog.RestoreFailed(result.Error ?? result.Outcome.ToString()));
                        break;
                }

                StartDevice(operation);
            });
        }

        private void StartDevice(int operation)
        {
            _backend.SignInDevice(result =>
            {
                if (operation != _operation)
                {
                    return;
                }

                _stage = AccountStage.Idle;
                if (result.Succeeded)
                {
                    Debug.Log(AccountLog.SignedIn(AccountKind.Device));
                }
                else
                {
                    _retryAt = _now + RetrySeconds;
                    Debug.LogWarning(AccountLog.DeviceFailed(result.Error ?? result.Outcome.ToString(), RetrySeconds));
                }

                SyncKind(false);
                Publish();
            });
        }

        private void SyncKind(bool announceLoss)
        {
            var kind = _backend.SignedInKind;
            if (kind == _kind)
            {
                return;
            }

            if (announceLoss && kind == AccountKind.None && _stage == AccountStage.Idle)
            {
                Debug.Log(AccountLog.Lost(_kind));
            }

            _kind = kind;
            Publish();
        }

        private void Publish()
        {
            var status = Status;
            if (status == _published)
            {
                return;
            }

            _published = status;
            if (_prepared)
            {
                StatusChanged?.Invoke(status);
            }
        }
    }
}
