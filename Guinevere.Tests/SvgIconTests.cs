namespace Guinevere.Tests;

/// <summary>Tests for the optional <c>Guinevere.Svg</c> package.</summary>
public class SvgIconTests
{
    const string Square = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 12"><rect width="24" height="12" fill="#00f"/></svg>""";

    /// <summary>Markup becomes a picture whose cull rectangle is the view box.</summary>
    [Fact]
    public void FromMarkup_UsesViewBox()
    {
        var icon = SvgIcon.FromMarkup(Square, tintable: true)!;

        Assert.Equal((IconKind.Picture, true), (icon.Kind, icon.Tintable));
        Assert.Equal((24f, 12f), (icon.Picture!.CullRect.Width, icon.Picture.CullRect.Height));
    }

    /// <summary>Files and streams decode; the decoder only claims <c>.svg</c>.</summary>
    [Fact]
    public void FromFileAndDecoder_Decode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"icon-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, Square);
        try
        {
            Assert.Equal(IconKind.Picture, SvgIcon.FromFile(path)!.Kind);
            Assert.Equal(IconKind.Picture, Icon.FromFile(path, [SvgIconDecoder.Instance])!.Kind);
        }
        finally
        {
            File.Delete(path);
        }

        Assert.True(SvgIconDecoder.Instance.CanDecode(".SVG"));
        Assert.False(SvgIconDecoder.Instance.CanDecode(".png"));
    }

    /// <summary>Markup that is not XML is not an icon.</summary>
    [Fact]
    public void InvalidMarkup_IsNull()
    {
        Assert.Null(SvgIcon.FromMarkup("not svg"));
        Assert.Null(SvgIcon.FromStream(new MemoryStream("<svg"u8.ToArray())));
    }

    /// <summary>Arguments are validated.</summary>
    [Fact]
    public void Arguments_AreValidated()
    {
        Assert.Throws<ArgumentException>(() => SvgIcon.FromMarkup(""));
        Assert.Throws<ArgumentException>(() => SvgIcon.FromFile(""));
        Assert.Throws<ArgumentNullException>(() => SvgIcon.FromStream(null!));
    }
}
