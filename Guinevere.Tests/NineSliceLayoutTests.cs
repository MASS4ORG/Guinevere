namespace Guinevere.Tests;

/// <summary>Checks the shared grid used by slice renderers and authoring tools.</summary>
public sealed class NineSliceLayoutTests
{
    /// <summary>All nine patches partition the source and destination without gaps or overlaps.</summary>
    [Theory]
    [InlineData(100, 80)]
    [InlineData(3, 2)]
    [InlineData(0, 0)]
    public void PatchesPartitionTheGrid(float width, float height)
    {
        var grid = new NineSliceLayout(20, 16, new Rect(10, 12, width, height), new Insets(3, 2, 5, 4));
        var sourceArea = 0f;
        var destinationArea = 0f;
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var (source, destination) = grid.Patch(row, column);
                sourceArea += source.W * source.H;
                destinationArea += destination.W * destination.H;
                Assert.True(destination.W >= 0 && destination.H >= 0);
                if (column < 2)
                {
                    Assert.Equal(destination.X + destination.W, grid.Patch(row, column + 1).Destination.X);
                }

                if (row < 2)
                {
                    Assert.Equal(destination.Y + destination.H, grid.Patch(row + 1, column).Destination.Y);
                }
            }
        }

        Assert.Equal(320, sourceArea);
        Assert.Equal(width * height, destinationArea, 3);
        Assert.Equal(new Rect(3, 2, 12, 10), grid.SourceCenter);
        if (width == 100)
        {
            Assert.Equal(new Rect(13, 14, 92, 74), grid.DestinationCenter);
        }
        else if (width > 0)
        {
            Assert.Equal(3f / 8f, grid.Patch(0, 0).Destination.W / width, 3);
        }
        else
        {
            Assert.Equal(0, grid.Patch(0, 0).Destination.W);
        }
    }

    /// <summary>Invalid source dimensions and borders cannot produce overlapping or nonfinite patches.</summary>
    [Fact]
    public void ClampsSourceAndInvalidValues()
    {
        var grid = new NineSliceLayout(20, 16, new Rect(0, 0, -4, float.NaN), new Insets(30, -1, 30, float.NaN));
        Assert.Equal(new Insets(20, 0, 0, 0), grid.Border);
        Assert.Equal(default, grid.Destination);
        Assert.Equal(new Rect(20, 0, 0, 16), grid.SourceCenter);
        var empty = new NineSliceLayout(float.NaN, float.PositiveInfinity, default, new Insets(4));
        Assert.Equal(default, empty.Source);
        Assert.Equal(default, empty.Border);
    }

    /// <summary>Patch coordinates must address the three rows and three columns.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 3)]
    public void RejectsInvalidPatchCoordinates(int row, int column) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new NineSliceLayout().Patch(row, column));
}
