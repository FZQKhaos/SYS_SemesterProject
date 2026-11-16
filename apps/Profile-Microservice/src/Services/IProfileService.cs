using ProfileMicroservice.Entities;

namespace ProfileMicroservice.Services;

public interface IProfileService
{
    IReadOnlyCollection<Profile> GetAll();

    Profile? GetById(string id);

    Profile Create(string displayName);

    Profile? Update(string id, string displayName);

    bool Delete(string id);
    
    Profile? RecordMessage(string id, DateTime postedAt);
    
    BanResult Ban(string id);
    bool Unban(string id);
    
}