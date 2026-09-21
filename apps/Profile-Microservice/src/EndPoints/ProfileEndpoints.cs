using Bizcord.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using ProfileMicroservice.Mappings;
using ProfileMicroservice.Requests;
using ProfileMicroservice.Services;

namespace ProfileMicroservice.Controllers;

[ApiController]
[Route("api/profiles")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfilesController(IProfileService profileService)
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

    [HttpGet("{id}")]
    public ActionResult<ProfileDto> GetById(string id)
    {
        var profile = _profileService.GetById(id);

        if (profile is null)
        {
            return NotFound();
        }

        return Ok(ProfileMapper.ToSharedModel(profile));
    }

    [HttpPost]
    public ActionResult<ProfileDto> Create(
        CreateProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(
                new { Error = "DisplayName is required." });
        }

        var profile =
            _profileService.Create(request.DisplayName);

        var response =
            ProfileMapper.ToSharedModel(profile);

        return CreatedAtAction(
            nameof(GetById),
            new { id = profile.Id },
            response);
    }

    [HttpPut("{id}")]
    public ActionResult<ProfileDto> Update(
        string id,
        UpdateProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(
                new { Error = "DisplayName is required." });
        }

        var profile =
            _profileService.Update(id, request.DisplayName);

        if (profile is null)
        {
            return NotFound();
        }

        return Ok(ProfileMapper.ToSharedModel(profile));
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        var deleted = _profileService.Delete(id);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}