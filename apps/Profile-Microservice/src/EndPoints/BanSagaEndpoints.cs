using Microsoft.AspNetCore.Mvc;
using ProfileMicroservice.Services;

namespace ProfileMicroservice.Controllers;

[ApiController]
[Route("api/bans")]
public class BanSagaController : ControllerBase
{
    private readonly IBanSagaTracker _tracker;

    public BanSagaController(IBanSagaTracker tracker)
    {
        _tracker = tracker;
    }

    [HttpGet("{banId:guid}")]
    public IActionResult GetState(Guid banId)
    {
        var state = _tracker.Get(banId);

        if (state is null)
        {
            return NotFound();
        }

        return Ok(new { BanId = banId, State = state.Value.ToString() });
    }
}