using Xunit;

namespace CalibriTests;

public class RecipeModelTests
{
    [Fact]
    public void RecipeStep_ShouldHaveDefaultValues()
    {
        var step = new CalibriCore.Models.RecipeStep();
        
        Assert.Equal(CalibriCore.Models.StepStatus.Pending, step.Status);
        Assert.Equal(CalibriCore.Models.FailAction.Abort, step.OnFail);
    }

    [Fact]
    public void Tolerance_GetLimits_ReturnsCorrectValues()
    {
        var tolerance = new CalibriCore.Models.Tolerance
        {
            Nominal = 100,
            ErrorPercent = 5
        };
        
        var (lower, upper) = tolerance.GetLimits();
        
        Assert.Equal(95, lower);
        Assert.Equal(105, upper);
    }

    [Fact]
    public void Tolerance_IsWithinTolerance_ReturnsTrue()
    {
        var tolerance = new CalibriCore.Models.Tolerance
        {
            Nominal = 100,
            ErrorPercent = 5
        };
        
        Assert.True(tolerance.IsWithinTolerance(100));
        Assert.True(tolerance.IsWithinTolerance(95));
        Assert.True(tolerance.IsWithinTolerance(105));
        Assert.False(tolerance.IsWithinTolerance(90));
        Assert.False(tolerance.IsWithinTolerance(110));
    }
}
