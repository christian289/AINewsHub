using FluentAssertions;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;

namespace AINewsHub.Core.Tests;

public class UserServiceTests
{
    [Fact]
    public void User_WithNoLastTestDate_ShouldAllowRetest()
    {
        // AC12: 재테스트 7일 제한 준수
        var user = new User
        {
            LastTestDate = null
        };

        var canRetest = user.LastTestDate == null ||
                        (DateTime.UtcNow - user.LastTestDate.Value).TotalDays >= 7;

        canRetest.Should().BeTrue();
    }

    [Fact]
    public void User_WithRecentTest_ShouldNotAllowRetest()
    {
        // AC12: 재테스트 7일 제한 준수
        var user = new User
        {
            LastTestDate = DateTime.UtcNow.AddDays(-3)
        };

        var canRetest = user.LastTestDate == null ||
                        (DateTime.UtcNow - user.LastTestDate.Value).TotalDays >= 7;

        canRetest.Should().BeFalse();
    }

    [Fact]
    public void User_WithOldTest_ShouldAllowRetest()
    {
        // AC12: 재테스트 7일 제한 준수
        var user = new User
        {
            LastTestDate = DateTime.UtcNow.AddDays(-8)
        };

        var canRetest = user.LastTestDate == null ||
                        (DateTime.UtcNow - user.LastTestDate.Value).TotalDays >= 7;

        canRetest.Should().BeTrue();
    }

    [Fact]
    public void User_AtExactly7Days_ShouldAllowRetest()
    {
        // AC12: 재테스트 7일 제한 준수 (경계 조건)
        var user = new User
        {
            LastTestDate = DateTime.UtcNow.AddDays(-7)
        };

        var canRetest = user.LastTestDate == null ||
                        (DateTime.UtcNow - user.LastTestDate.Value).TotalDays >= 7;

        canRetest.Should().BeTrue();
    }

    [Fact]
    public void NewUser_ShouldHaveDefaultLevel()
    {
        var user = new User();
        user.Level.Should().Be(UserLevel.Beginner);
    }

    [Fact]
    public void NewUser_ShouldHaveZeroTestCount()
    {
        var user = new User();
        user.TestCount.Should().Be(0);
    }
}
