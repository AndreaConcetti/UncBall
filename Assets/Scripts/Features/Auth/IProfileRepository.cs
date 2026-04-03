using System.Threading.Tasks;
using UncballArena.Core.Profile.Models;

namespace UncballArena.Core.Profile.Repositories
{
    public interface IProfileRepository
    {
        Task<ProfileSnapshot> LoadByPlayerIdAsync(string playerId);
        Task SaveAsync(ProfileSnapshot snapshot);
        Task DeleteByPlayerIdAsync(string playerId);
        bool Exists(string playerId);
    }
}