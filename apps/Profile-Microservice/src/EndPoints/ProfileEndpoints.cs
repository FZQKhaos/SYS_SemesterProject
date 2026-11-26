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
    private readonly IServiceProvider _services;
    private readonly RebusReadiness _rebusReadiness;

    public ProfilesController(
        IProfileService profileService,
        IMessageClient messageClient,
        IServiceProvider services,
        RebusReadiness rebusReadiness)
    {
        _profileService = profileService;
        _messageClient = messageClient;
        _services = services;
        _rebusReadiness = rebusReadiness;
    }
    [HttpGet]
    public ActionResult<IEnumerable<ProfileDto>> GetAll()
    {
        var profiles = _profileService
            .GetAll()
            .Select(ProfileMapper.ToSharedModel);

        return Ok(profiles);
    }
    
    
    [HttpGet("me")]
    public IActionResult GetMe([FromHeader(Name = "X-UserId")] string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        return Ok(new { UserId = userId });
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
        // Tjek bussen FØR profilen ændres, ellers kunne en bruger blive bannet uden at sagaen starter.
        if (!_rebusReadiness.IsReady)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { Error = "The ban saga is not ready yet. Try again shortly." });
        }

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

        // Bussen hentes først her, for den må kun oprettes, når RabbitMQ er klar (se RebusStartupService).
        var bus = _services.GetRequiredService<IBus>();

        await bus.Publish(new UserBannedEvent
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