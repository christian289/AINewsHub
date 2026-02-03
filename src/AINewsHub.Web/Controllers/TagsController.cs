using Microsoft.AspNetCore.Mvc;
using AINewsHub.Core.Interfaces;

namespace AINewsHub.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly ITagPreferenceService _tagPreferenceService;

    public TagsController(ITagPreferenceService tagPreferenceService)
    {
        _tagPreferenceService = tagPreferenceService;
    }

    /// <summary>
    /// Get all available tags
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TagResponse>>> GetTags()
    {
        var tags = await _tagPreferenceService.GetAllTagsAsync();
        return Ok(tags.Select(t => new TagResponse(t.Id, t.Name, t.Category, t.UsageCount)));
    }
}

public record TagResponse(int Id, string Name, string? Category, int UsageCount);
