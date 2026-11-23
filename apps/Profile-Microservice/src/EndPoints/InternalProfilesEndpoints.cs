using Bizcord.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProfileMicroservice.Mappings;
using ProfileMicroservice.Services;

namespace ProfileMicroservice.Controllers;

[ApiController]
[Route("api/internal/profiles")]
[Authorize(Policy = "InternalService")]
public class InternalProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;

    public InternalProfilesController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ProfileDto>> GetAll()
    {
        var profiles = _profileService
            .GetAll()
            .Select(ProfileMapper.ToSharedModel);

        return Ok(profiles);
    }
}