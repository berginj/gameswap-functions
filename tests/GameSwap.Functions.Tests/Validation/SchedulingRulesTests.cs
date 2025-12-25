using GameSwap.Functions.Storage;
using Xunit;

namespace GameSwap.Functions.Tests.Validation;

public class SchedulingRulesTests
{
    [Fact]
    public void Detects_overlap_between_ranges()
    {
        var overlap = SchedulingRules.Overlaps(9 * 60, 10 * 60, 9 * 60 + 30, 11 * 60);
        var noOverlap = SchedulingRules.Overlaps(9 * 60, 10 * 60, 10 * 60, 11 * 60);

        Assert.True(overlap);
        Assert.False(noOverlap);
    }
}
