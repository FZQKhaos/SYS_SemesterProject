using Bizcord.Shared.Models;
using ProfileMicroservice.Mappings;
using ProfileMicroservice.Services;

namespace ProfileMicroservice.GraphQL;

// Input-typer: det klienten sender med i en mutation. Egne typer i stedet for at genbruge
// REST-requests, så GraphQL-skemaet kan udvikle sig uafhængigt af REST-API'et.
public sealed record CreateProfileInput(string DisplayName);

public sealed record UpdateProfileInput(string Id, string DisplayName);

// Presentation layer: mutationer (ændringer). Samme validering som ProfilesController,
// men fejl meldes som GraphQL-fejl i stedet for HTTP-statuskoder.
public class ProfileMutation
{
    public ProfileDto CreateProfile(CreateProfileInput input, IProfileService profileService)
    {
        RequireDisplayName(input.DisplayName);

        var profile = profileService.Create(input.DisplayName);

        return ProfileMapper.ToSharedModel(profile);
    }

    public ProfileDto UpdateProfile(UpdateProfileInput input, IProfileService profileService)
    {
        RequireDisplayName(input.DisplayName);

        var profile = profileService.Update(input.Id, input.DisplayName)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Profile '{input.Id}' was not found.")
                    .SetCode("PROFILE_NOT_FOUND")
                    .Build());

        return ProfileMapper.ToSharedModel(profile);
    }

    public bool DeleteProfile(string id, IProfileService profileService)
    {
        return profileService.Delete(id);
    }

    private static void RequireDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("DisplayName is required.")
                    .SetCode("DISPLAYNAME_REQUIRED")
                    .Build());
        }
    }
}
