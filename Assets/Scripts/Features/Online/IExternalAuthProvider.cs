using System.Threading;
using System.Threading.Tasks;
using UncballArena.Core.Auth.Models;

namespace UncballArena.Core.Auth.Providers
{
    public interface IExternalAuthProvider
    {
        AuthProviderType ProviderType { get; }

        bool IsAvailable();

        Task<ExternalAuthResult> SignInAsync(CancellationToken cancellationToken);
    }
}