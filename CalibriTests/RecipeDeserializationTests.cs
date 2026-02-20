using System.Xml.Serialization;
using CalibriCore.Models;
using Xunit;

namespace CalibriTests;

public class RecipeDeserializationTests
{
    [Fact]
    public void VoltageCalibrationRecipe_ShouldDeserializeCorrectly()
    {
        // Arrange
        var xmlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "CalibriWpf", "Configs", "Recipes", "voltage_calibration.xml");

        // Act
        Recipe? recipe;
        var serializer = new XmlSerializer(typeof(Recipe));
        using (var reader = new StreamReader(xmlPath))
        {
            recipe = (Recipe?)serializer.Deserialize(reader);
        }

        // Assert
        Assert.NotNull(recipe);
        Assert.NotNull(recipe.Name);
        Assert.NotNull(recipe.Steps);
        Assert.NotEmpty(recipe.Steps);
    }

    [Fact]
    public void RecipeStep_ShouldHaveCorrectProperties()
    {
        // Arrange
        var xmlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "CalibriWpf", "Configs", "Recipes", "voltage_calibration.xml");

        // Act
        Recipe? recipe;
        var serializer = new XmlSerializer(typeof(Recipe));
        using (var reader = new StreamReader(xmlPath))
        {
            recipe = (Recipe?)serializer.Deserialize(reader);
        }

        // Assert
        Assert.NotNull(recipe);
        
        // Check first step has required properties
        var firstStep = recipe.Steps.First();
        Assert.NotNull(firstStep.Name);
        Assert.True(firstStep.Order > 0);
        Assert.True(Enum.IsDefined(typeof(StepType), firstStep.Type));
    }

    [Fact]
    public void Recipe_WithTolerance_ShouldDeserializeCorrectly()
    {
        // Arrange
        var xmlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "CalibriWpf", "Configs", "Recipes", "voltage_calibration.xml");

        // Act
        Recipe? recipe;
        var serializer = new XmlSerializer(typeof(Recipe));
        using (var reader = new StreamReader(xmlPath))
        {
            recipe = (Recipe?)serializer.Deserialize(reader);
        }

        // Assert - find a step with tolerance
        var stepWithTolerance = recipe?.Steps.FirstOrDefault(s => s.Tolerance != null);
        Assert.NotNull(stepWithTolerance);
        Assert.NotNull(stepWithTolerance.Tolerance);
        
        // Test tolerance calculation
        var (lower, upper) = stepWithTolerance.Tolerance.GetLimits();
        Assert.True(upper > lower);
    }

    [Fact]
    public void RecipeStepParameters_ShouldDeserializeCorrectly()
    {
        // Arrange
        var xmlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "CalibriWpf", "Configs", "Recipes", "voltage_calibration.xml");

        // Act
        Recipe? recipe;
        var serializer = new XmlSerializer(typeof(Recipe));
        using (var reader = new StreamReader(xmlPath))
        {
            recipe = (Recipe?)serializer.Deserialize(reader);
        }

        // Assert - find a step with parameters
        var stepWithParams = recipe?.Steps.FirstOrDefault(s => s.Parameters != null);
        Assert.NotNull(stepWithParams);
        Assert.NotNull(stepWithParams.Parameters);
        
        // At least one parameter should be set (could be Voltage, Current, Range, RouteName, etc.)
        var hasAnyParam = stepWithParams.Parameters.Voltage.HasValue
            || stepWithParams.Parameters.Current.HasValue
            || stepWithParams.Parameters.Range.HasValue
            || !string.IsNullOrEmpty(stepWithParams.Parameters.RouteName);
        Assert.True(hasAnyParam, "Step should have at least one parameter set");
    }
}
