using System.Collections.Concurrent;
using ProfileMicroservice.Entities;

namespace ProfileMicroservice.Services;

public sealed class ProfileService : IProfileService
{
    private readonly ConcurrentDictionary<string, Profile> _profiles = new(); //Memory storage for profiles, change later for database storage

    public IReadOnlyCollection<Profile> GetAll()
    {
        return _profiles.Values.ToList();
    }

    public Profile? GetById(string id)
    {
        _profiles.TryGetValue(id, out var profile);

        return profile;
    }

    public Profile Create(string displayName)
    {
        ValidateDisplayName(displayName);

        var profile = new Profile
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            DisplayName = displayName
        };

        _profiles[profile.Id] = profile;

        return profile;
    }

    public Profile? Update(string id, string displayName)
    {
        ValidateDisplayName(displayName);

        if (!_profiles.TryGetValue(id, out var profile))
        {
            return null;
        }

        profile.DisplayName = displayName;

        return profile;
    }

    public bool Delete(string id)
    {
        return _profiles.TryRemove(id, out _);
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Display name is required.",
                nameof(displayName));
        }
    }
}