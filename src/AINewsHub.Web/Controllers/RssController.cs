using Microsoft.AspNetCore.Mvc;
using AINewsHub.Core.Interfaces;

namespace AINewsHub.Web.Controllers;

[ApiController]
public class RssController : ControllerBase
{
    private readonly IRssService _rssService;

    public RssController(IRssService rssService)
    {
        _rssService = rssService;
    }

    /// <summary>
    /// Get personalized RSS feed
    /// </summary>
    [HttpGet("rss/{snowflakeId:long}")]
    [Produces("application/rss+xml")]
    public async Task<IActionResult> GetRssFeed(long snowflakeId)
    {
        try
        {
            var feed = await _rssService.GenerateRssFeedAsync(snowflakeId);
            return Content(feed, "application/rss+xml");
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "User not found" });
        }
    }
}
