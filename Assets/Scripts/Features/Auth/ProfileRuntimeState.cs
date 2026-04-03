using System;
using UncballArena.Core.Profile.Models;

namespace UncballArena.Core.Runtime
{
    public sealed class ProfileRuntimeState
    {
        public ProfileSnapshot CurrentProfile { get; private set; }

        public event Action<ProfileSnapshot> Changed;

        public void Set(ProfileSnapshot snapshot)
        {
            CurrentProfile = snapshot;
            Changed?.Invoke(CurrentProfile);
        }

        public void Clear()
        {
            CurrentProfile = null;
            Changed?.Invoke(CurrentProfile);
        }
    }
}