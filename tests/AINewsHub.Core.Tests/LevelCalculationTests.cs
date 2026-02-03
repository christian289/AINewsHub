using FluentAssertions;
using AINewsHub.Core.Enums;

namespace AINewsHub.Core.Tests;

public class LevelCalculationTests
{
    private static UserLevel CalculateLevel(int correctAnswers) =>
        correctAnswers switch
        {
            >= 6 => UserLevel.Advanced,
            >= 4 => UserLevel.Intermediate,
            _ => UserLevel.Beginner
        };

    [Theory]
    [InlineData(0, UserLevel.Beginner)]
    [InlineData(1, UserLevel.Beginner)]
    [InlineData(2, UserLevel.Beginner)]
    [InlineData(3, UserLevel.Beginner)]
    public void CorrectAnswers_LessThan4_ShouldBeBeginner(int correct, UserLevel expected)
    {
        CalculateLevel(correct).Should().Be(expected);
    }

    [Theory]
    [InlineData(4, UserLevel.Intermediate)]
    [InlineData(5, UserLevel.Intermediate)]
    public void CorrectAnswers_4Or5_ShouldBeIntermediate(int correct, UserLevel expected)
    {
        CalculateLevel(correct).Should().Be(expected);
    }

    [Theory]
    [InlineData(6, UserLevel.Advanced)]
    [InlineData(7, UserLevel.Advanced)]
    [InlineData(8, UserLevel.Advanced)]
    public void CorrectAnswers_6OrMore_ShouldBeAdvanced(int correct, UserLevel expected)
    {
        CalculateLevel(correct).Should().Be(expected);
    }
}
