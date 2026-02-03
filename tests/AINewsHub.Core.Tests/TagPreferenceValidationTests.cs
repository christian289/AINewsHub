using FluentAssertions;
using AINewsHub.Core.Interfaces;

namespace AINewsHub.Core.Tests;

public class TagPreferenceValidationTests
{
    [Fact]
    public void MaxMustIncludeTags_ShouldBeFive()
    {
        // AC13: 태그 선호도 제한 (5/10) 준수
        ITagPreferenceService.MaxMustIncludeTags.Should().Be(5);
    }

    [Fact]
    public void MaxExcludeTags_ShouldBeTen()
    {
        // AC13: 태그 선호도 제한 (5/10) 준수
        ITagPreferenceService.MaxExcludeTags.Should().Be(10);
    }
}
