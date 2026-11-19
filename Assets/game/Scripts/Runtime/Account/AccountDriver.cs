using UnityEngine;

namespace Shooter.Runtime
{
    // サインインは試合やメニューをまたいで続くので、シーンを替えても残るこの部品で回す。
    [DisallowMultipleComponent]
    public sealed class AccountDriver : MonoBehaviour
    {
        private AccountSignIn _signIn;

        public AccountSignIn SignIn => _signIn;

        public void Bind(AccountSignIn signIn)
        {
            _signIn = signIn;
        }

        private void Update()
        {
            // 前の Play のサインインが残っていれば、今のものと二重に回さない。
            if (_signIn == null || !ReferenceEquals(_signIn, AccountBootstrap.Current))
            {
                Destroy(gameObject);
                return;
            }

            _signIn.Tick(Time.unscaledTimeAsDouble);
        }
    }
}
