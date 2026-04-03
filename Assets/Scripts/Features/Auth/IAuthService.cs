using System;
using System.Threading.Tasks;
using UncballArena.Core.Auth.Models;

namespace UncballArena.Core.Auth.Services
{
    public interface IAuthService
    {
        AuthSession CurrentSession { get; }
        bool IsInitialized { get; }
        bool IsAuthenticated { get; }

        event Action<AuthSession> SessionChanged;

        Task InitializeAsync();
        Task<AuthSession> RestoreSessionAsync();
        Task<AuthSession> SignInAsGuestAsync(string displayName = "");
        Task<AuthSession> SignInWithGooglePlayAsync();
        Task<AuthSession> SignInWithAppleAsync();
        Task<AuthSession> SignInWithFacebookAsync();
        Task<AuthSession> LinkCurrentGuestToGooglePlayAsync();
        Task<AuthSession> LinkCurrentGuestToAppleAsync();
        Task<AuthSession> LinkCurrentGuestToFacebookAsync();
        Task<AuthSession> UpdateDisplayNameAsync(string displayName);
        Task SignOutAsync();
    }
}