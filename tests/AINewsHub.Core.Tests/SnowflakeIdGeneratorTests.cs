using FluentAssertions;
using AINewsHub.Infrastructure.Services;

namespace AINewsHub.Core.Tests;

public class SnowflakeIdGeneratorTests
{
    [Fact]
    public void NextId_ShouldGenerateUniqueIds()
    {
        // Arrange
        var generator = new SnowflakeIdGenerator(machineId: 1);
        var ids = new HashSet<long>();

        // Act
        for (int i = 0; i < 10000; i++)
        {
            ids.Add(generator.NextId());
        }

        // Assert - AC11: Snowflake ID 충돌률 0%
        ids.Should().HaveCount(10000, "all generated IDs should be unique");
    }

    [Fact]
    public void NextId_ShouldGeneratePositiveIds()
    {
        var generator = new SnowflakeIdGenerator(machineId: 1);

        for (int i = 0; i < 100; i++)
        {
            var id = generator.NextId();
            id.Should().BePositive();
        }
    }

    [Fact]
    public void NextId_ShouldGenerateIncreasingIds()
    {
        var generator = new SnowflakeIdGenerator(machineId: 1);
        var previousId = 0L;

        for (int i = 0; i < 100; i++)
        {
            var id = generator.NextId();
            id.Should().BeGreaterThan(previousId);
            previousId = id;
        }
    }

    [Fact]
    public void ExtractTimestamp_ShouldReturnRecentTime()
    {
        var generator = new SnowflakeIdGenerator(machineId: 1);
        var before = DateTime.UtcNow;
        var id = generator.NextId();
        var after = DateTime.UtcNow;

        var timestamp = generator.ExtractTimestamp(id);

        timestamp.Should().BeOnOrAfter(before.AddMilliseconds(-1));
        timestamp.Should().BeOnOrBefore(after.AddMilliseconds(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(512)]
    [InlineData(1023)]
    public void Constructor_ShouldAcceptValidMachineIds(long machineId)
    {
        var generator = new SnowflakeIdGenerator(machineId);
        generator.NextId().Should().BePositive();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1024)]
    [InlineData(2000)]
    public void Constructor_ShouldRejectInvalidMachineIds(long machineId)
    {
        var act = () => new SnowflakeIdGenerator(machineId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MultipleGenerators_ShouldProduceDifferentIds()
    {
        var generator1 = new SnowflakeIdGenerator(machineId: 1);
        var generator2 = new SnowflakeIdGenerator(machineId: 2);

        var id1 = generator1.NextId();
        var id2 = generator2.NextId();

        id1.Should().NotBe(id2);
    }
}
