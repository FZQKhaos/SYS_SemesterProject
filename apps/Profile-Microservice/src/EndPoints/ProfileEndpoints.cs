using Bizcord.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using ProfileMicroservice.Mappings;
using ProfileMicroservice.Requests;
using ProfileMicroservice.Services;
using Bizcord.Shared.Events;
using ProfileMicroservice.Messaging;
using Rebus.Bus;

namespace ProfileMicroservice.Controllers;

[ApiController]
[Route("api/profiles")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IMessageClient _messageClient;
    private readonly IBus _bus;

    public ProfilesController(
        IProfileService profileService,
        IMessageClient messageClient,
        IBus bus)
    {
        _profileService = profileService;
        _messageClient = messageClient;
        _bus = bus;
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

    [HttpPost("{id}/ban")]
    public async Task<IActionResult> Ban(string id)
    {
        var result = _profileService.Ban(id);

        if (result == BanResult.NotFound)
        {
            return NotFound();
        }

        if (result == BanResult.AlreadyBanned)
        {
            return Conflict(new { Error = "User is already banned." });
        }

        var banId = Guid.NewGuid();

        await _bus.Publish(new UserBannedEvent
        {
            BanId = banId,
            UserId = Guid.Parse(id),
            BannedAt = DateTime.UtcNow
        });

        return Accepted(new { BanId = banId });
    }

    [HttpPost("{id}/unban")]
    public async Task<IActionResult> Unban(
        string id,
        CancellationToken cancellationToken)
    {
        var found = _profileService.Unban(id);

        if (!found)
        {
            return NotFound();
        }

        var unbanId = Guid.NewGuid();

        await _messageClient.PublishAsync(
            new UserUnbannedEvent
            {
                UnbanId = unbanId,
                UserId = Guid.Parse(id),
                UnbannedAt = DateTime.UtcNow
            },
            cancellationToken);

        return Accepted(new { UnbanId = unbanId });
    }
}