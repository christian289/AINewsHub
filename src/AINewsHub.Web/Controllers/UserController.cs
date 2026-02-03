using Microsoft.AspNetCore.Mvc;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Interfaces;

namespace AINewsHub.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ITagPreferenceService _tagPreferenceService;

    public UserController(IUserService userService, ITagPreferenceService tagPreferenceService)
    {
        _userService = userService;
        _tagPreferenceService = tagPreferenceService;
    }

    /// <summary>
    /// Initialize a new user or get existing by Snowflake ID
    /// </summary>
    [HttpPost("init")]
    public async Task<ActionResult<UserResponse>> InitUser([FromBody] InitUserRequest? request = null)
    {
        User user;

        if (request?.SnowflakeId != null)
        {
            user = await _userService.GetUserBySnowflakeIdAsync(request.SnowflakeId.Value)
                   ?? await _userService.CreateUserAsync();
        }
        else
        {
            user = await _userService.CreateUserAsync();
        }

        return Ok(new UserResponse(user));
    }

    /// <summary>
    /// Get user by Snowflake ID
    /// </summary>
    [HttpGet("{snowflakeId:long}")]
    public async Task<ActionResult<UserResponse>> GetUser(long snowflakeId)
    {
        var user = await _userService.GetUserBySnowflakeIdAsync(snowflakeId);
        if (user == null)
            return NotFound(new { error = "User not found" });

        await _userService.UpdateLastActiveAsync(snowflakeId);
        return Ok(new UserResponse(user));
    }

    /// <summary>
    /// Check if user can take retest (7-day limit)
    /// </summary>
    [HttpGet("{snowflakeId:long}/can-retest")]
    public async Task<ActionResult<CanRetestResponse>> CanRetest(long snowflakeId)
    {
        var user = await _userService.GetUserBySnowflakeIdAsync(snowflakeId);
        if (user == null)
            return NotFound(new { error = "User not found" });

        var canRetest = await _userService.CanRetestAsync(snowflakeId);
        var nextTestDate = user.LastTestDate?.AddDays(7);

        return Ok(new CanRetestResponse(canRetest, nextTestDate));
    }

    /// <summary>
    /// Recover user from RSS URL
    /// </summary>
    [HttpPost("recover")]
    public async Task<ActionResult<UserResponse>> RecoverUser([FromBody] RecoverRequest request)
    {
        try
        {
            var user = await _userService.RecoverUserFromRssUrlAsync(request.RssUrl);
            return Ok(new UserResponse(user));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update tag preferences
    /// </summary>
    [HttpPut("{snowflakeId:long}/preferences")]
    public async Task<ActionResult> UpdatePreferences(
        long snowflakeId,
        [FromBody] PreferencesRequest request)
    {
        var user = await _userService.GetUserBySnowflakeIdAsync(snowflakeId);
        if (user == null)
            return NotFound(new { error = "User not found" });

        try
        {
            await _tagPreferenceService.SetPreferencesAsync(
                user.Id,
                request.MustIncludeTagIds,
                request.ExcludeTagIds);
            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

// DTOs
public record InitUserRequest(long? SnowflakeId);
public record RecoverRequest(string RssUrl);
public record PreferencesRequest(IEnumerable<int> MustIncludeTagIds, IEnumerable<int> ExcludeTagIds);
public record CanRetestResponse(bool CanRetest, DateTime? NextTestDate);

public record UserResponse(
    int Id,
    long SnowflakeId,
    string Level,
    DateTime? LastTestDate,
    int TestCount,
    DateTime CreatedAt,
    DateTime LastActiveAt,
    IEnumerable<TagPreferenceResponse> TagPreferences)
{
    public UserResponse(User user) : this(
        user.Id,
        user.SnowflakeId,
        user.Level.ToString(),
        user.LastTestDate,
        user.TestCount,
        user.CreatedAt,
        user.LastActiveAt,
        user.TagPreferences.Select(tp => new TagPreferenceResponse(tp.Tag.Id, tp.Tag.Name, tp.PreferenceType.ToString())))
    { }
}

public record TagPreferenceResponse(int TagId, string TagName, string PreferenceType);
