using SkiaSharp;

namespace Example02;

static class DemoIcons
{
    public static readonly IReadOnlyDictionary<string, Icon> Actions =
        new Dictionary<string, Icon>(StringComparer.Ordinal)
        {
            ["Undo"] = Make("M9 4L4 9L9 14 M4 9H13C21 9 21 20 13 20"),
            ["Redo"] = Make("M15 4L20 9L15 14 M20 9H11C3 9 3 20 11 20"),
            ["Frame all"] = Make("M9 3H3V9 M15 3H21V9 M3 15V21H9 M15 21H21V15"),
            ["Add node"] = Make("M12 4V20 M4 12H20"),
            ["Decrease"] = Make("M4 12H20"),
            ["Run"] = Make("M7 4L20 12L7 20Z"),
            ["Pause"] = Make("M8 4V20 M16 4V20"),
            ["Step"] = Make("M5 4L17 12L5 20Z M21 4V20"),
            ["Previous instance"] = Make("M16 4L8 12L16 20"),
            ["Next instance"] = Make("M8 4L16 12L8 20"),
            ["Send to instance"] = Make("M3 3L21 12L3 21L6 12Z M6 12H21")
        };

    static Icon Make(string data)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, 24, 24));
        using var path = SKPath.ParseSvgPathData(data);
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        canvas.DrawPath(path, paint);
        return Icon.FromPicture(recorder.EndRecording(), tintable: true);
    }
}
