using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Profile.Models;

namespace UncballArena.Core.Profile.Repositories
{
    public sealed class CachedProfileRepository : IProfileRepository
    {
        private readonly IProfileRepository remoteRepository;
        private readonly IProfileRepository localCacheRepository;
        private readonly bool logDebug;

        public CachedProfileRepository(
            IProfileRepository remoteRepository,
            IProfileRepository localCacheRepository,
            bool logDebug = true)
        {
            this.remoteRepository = remoteRepository;
            this.localCacheRepository = localCacheRepository;
            this.logDebug = logDebug;
        }

        public async Task<ProfileSnapshot> LoadByPlayerIdAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return null;

            if (remoteRepository != null)
            {
                ProfileSnapshot remote = await remoteRepository.LoadByPlayerIdAsync(playerId);
                if (remote != null && remote.IsValid())
                {
                    if (localCacheRepository != null)
                        await localCacheRepository.SaveAsync(remote);

                    if (logDebug)
                    {
                        Debug.Log(
                            $"[CachedProfileRepository] Remote hit. PlayerId={playerId} | ProfileId={remote.ProfileId}");
                    }

                    return remote;
                }
            }

            if (localCacheRepository != null)
            {
                ProfileSnapshot cached = await localCacheRepository.LoadByPlayerIdAsync(playerId);
                if (cached != null && cached.IsValid())
                {
                    if (logDebug)
                    {
                        Debug.Log(
                            $"[CachedProfileRepository] Local cache hit. PlayerId={playerId} | ProfileId={cached.ProfileId}");
                    }

                    return cached;
                }
            }

            if (logDebug)
                Debug.Log($"[CachedProfileRepository] No profile found. PlayerId={playerId}");

            return null;
        }

        public async Task SaveAsync(ProfileSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsValid())
                return;

            if (remoteRepository != null)
                await remoteRepository.SaveAsync(snapshot);

            if (localCacheRepository != null)
                await localCacheRepository.SaveAsync(snapshot);
        }

        public async Task DeleteByPlayerIdAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            if (remoteRepository != null)
                await remoteRepository.DeleteByPlayerIdAsync(playerId);

            if (localCacheRepository != null)
                await localCacheRepository.DeleteByPlayerIdAsync(playerId);
        }

        public bool Exists(string playerId)
        {
            if (localCacheRepository != null && localCacheRepository.Exists(playerId))
                return true;

            return false;
        }
    }
}