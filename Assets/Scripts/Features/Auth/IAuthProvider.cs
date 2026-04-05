using System.Threading.Tasks;

namespace UncballArena.Core.Auth
{
    public interface IAuthProvider
    {
        AuthProviderType ProviderType { get; }
        bool IsAvailable { get; }

        Task<AuthProviderSignInResult> SignInAsync();
        Task SignOutAsync();
    }
}