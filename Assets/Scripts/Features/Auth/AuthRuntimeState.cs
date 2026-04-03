using System;
using UncballArena.Core.Auth.Models;

namespace UncballArena.Core.Runtime
{
    public sealed class AuthRuntimeState
    {
        public AuthSession CurrentSession { get; private set; }

        public event Action<AuthSession> Changed;

        public void Set(AuthSession session)
        {
            CurrentSession = session;
            Changed?.Invoke(CurrentSession);
        }

        public void Clear()
        {
            CurrentSession = null;
            Changed?.Invoke(CurrentSession);
        }
    }
}