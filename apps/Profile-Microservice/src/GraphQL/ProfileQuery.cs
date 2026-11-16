using Bizcord.Shared.Models;
using ProfileMicroservice.Mappings;
using ProfileMicroservice.Services;

namespace ProfileMicroservice.GraphQL;

// Presentation layer: forespørgsler (læsning). Indeholder ingen forretningslogik,
// den ligger stadig i IProfileService. Her oversætter vi kun mellem GraphQL og servicen.
public class ProfileQuery
{
    // GraphQL: { profiles { id displayName } }
    // IProfileService injiceres af HotChocolate fra DI (samme registrering som REST bruger).
    public IEnumerable<ProfileDto> GetProfiles(IProfileService profileService)
    {
        return profileService
            .GetAll()
            .Select(ProfileMapper.ToSharedModel);
    }

    // GraphQL: { profileById(id: "...") { displayName isBanned } }
    // Returnerer null (ikke en fejl), hvis profilen ikke findes.
    public ProfileDto? GetProfileById(string id, IProfileService profileService)
    {
        var profile = profileService.GetById(id);

        return profile is null ? null : ProfileMapper.ToSharedModel(profile);
    }
}
