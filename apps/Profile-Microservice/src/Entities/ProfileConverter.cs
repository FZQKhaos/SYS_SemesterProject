namespace ProfileMicroservice.Entities;

public class ProfileConverter : IProfile<Profile, ProfileDto>
{
    public ProfileDto Convert(Profile source)
    {
        return new ProfileDto
        {
            Id = source.Id,
            CreatedAt = source.CreatedAt,
            DisplayName = source.DisplayName,
            Friends = source.Friends
                .Select(f => new FriendDto
                {
                    Id = f.Id,
                    CreatedAt = f.CreatedAt,
                    DisplayName = f.DisplayName
                })
                .ToList()
        };
    }

    public Profile Convert(ProfileDto source)
    {
        return new Profile
        {
            Id = source.Id,
            CreatedAt = source.CreatedAt,
            DisplayName = source.DisplayName,
            Friends = source.Friends
                .Select(f => new Friend
                {
                    Id = f.Id,
                    CreatedAt = f.CreatedAt,
                    DisplayName = f.DisplayName
                })
                .ToList()
        };
    }
}
