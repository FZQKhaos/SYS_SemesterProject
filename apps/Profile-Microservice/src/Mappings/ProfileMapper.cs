using Bizcord.Shared.Models;
using ProfileMicroservice.Entities;

namespace ProfileMicroservice.Mappings;

public static class ProfileMapper
{
    public static ProfileDto ToSharedModel(Profile profile)
    {
        return new ProfileDto
        {
            Id = profile.Id,
            CreatedAt = profile.CreatedAt,
            DisplayName = profile.DisplayName
        };
    }
}