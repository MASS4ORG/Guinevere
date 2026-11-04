using Example02;
using Guinevere.Tests.Mocks;
using MASS4.Graph;
using MASS4.Graph.Dataflow;
using GraphPoint = Guinevere.Graphs.GraphPoint;
using GraphSeverity = MASS4.Graph.DiagnosticSeverity;

namespace Guinevere.Tests.Graphs;

public sealed class GraphDomainScenesTests
{
    [Fact]
    public async Task ShaderScenarioRendersFragmentPreviewsAndGeneratesGlsl()
    {
        using var harness = new FrameHarness(1400, 900);
        var playground = new GraphPlayground();
        playground.SelectScenario(4);
        var shader = playground.Shader;
        await Settle(harness, playground, shader);
        var document = shader.Sandbox.Graph.Editor.Document;
        Assert.DoesNotContain(shader.Sandbox.View.Validation, static d => d.Severity == GraphSeverity.Error);
        Assert.Single(document.Nodes, static node => node.TypeId == ShaderDomain.ScalarToColor.Id);
        Assert.Contains(shader.Sandbox.View.Palette("check"), static type => type.Id == ShaderDomain.Checker.Id);

        var albedo = Assert.Single(shader.Outputs);
        var output = shader.Frame.Previews[albedo];
        Assert.Equal(ShaderDomain.PreviewSize * ShaderDomain.PreviewSize, output.Pixels.Length);
        Assert.Equal(Expected(new Rgba(0.95f, 0.92f, 0.85f, 1)), output.Pixels[0]);
        Assert.Equal(Expected(new Rgba(0.18f, 0.24f, 0.36f, 1)), output.Pixels[ShaderDomain.PreviewSize / 4]);
        var checker = document.Nodes.Single(static node => node.TypeId == ShaderDomain.Checker.Id);
        Assert.True(shader.Frame.Previews.ContainsKey(checker.Id));
        Assert.Contains("uniform vec4 tint;", shader.Generated, StringComparison.Ordinal);
        Assert.Contains("out vec4 albedo;", shader.Generated, StringComparison.Ordinal);
        Assert.Contains("vec2 n0 = uv;", shader.Generated, StringComparison.Ordinal);
        Assert.Contains("vec4 n1 = vec4(0.95, 0.92, 0.85, 1.0);", shader.Generated, StringComparison.Ordinal);
        Assert.Contains("vec4 n3 = checker(n0, n1, n2, 4.0);", shader.Generated, StringComparison.Ordinal);
        Assert.Contains("vec4 n4 = n3 * tint;", shader.Generated, StringComparison.Ordinal);
        Assert.Contains("vec4 n6 = vec4(n5);", shader.Generated, StringComparison.Ordinal);
        Assert.Contains("vec4 n8 = mix(n4, n6, n7);", shader.Generated, StringComparison.Ordinal);
        Assert.Contains("albedo = n8;", shader.Generated, StringComparison.Ordinal);

        Assert.True(shader.Sandbox.Step(checker.Id, 1).Succeeded);
        Assert.Equal("5 × 5 tiles", shader.Sandbox.Graph.FormatPayload(document.GetNode(checker.Id)));
        await Settle(harness, playground, shader);
        Assert.Contains("checker(n0, n1, n2, 5.0)", shader.Generated, StringComparison.Ordinal);
        Assert.True(shader.Sandbox.View.Undo().Succeeded);
        Assert.Equal(new ScalarPayload(4), document.GetNode(checker.Id).Payload);
    }

    [Fact]
    public async Task ShaderDocumentRoundTripsAndRebindsPreviews()
    {
        using var harness = new FrameHarness(1400, 900);
        var playground = new GraphPlayground();
        playground.SelectScenario(4);
        var shader = playground.Shader;
        await Settle(harness, playground, shader);
        var json = shader.Sandbox.View.Serialize();
        var loaded = shader.Sandbox.Load(json);
        Assert.Empty(loaded.Diagnostics);
        Assert.Same(loaded.View, shader.Sandbox.View);
        await Settle(harness, playground, shader);
        var albedo = Assert.Single(shader.Outputs);
        Assert.True(shader.Frame.Previews.ContainsKey(albedo));
        Assert.Equal(json, shader.Sandbox.View.Serialize());
    }

    [Fact]
    public async Task CompositorScenarioRendersThumbnailsAtThePlayhead()
    {
        using var harness = new FrameHarness(1400, 900);
        var playground = new GraphPlayground();
        playground.SelectScenario(5);
        var compositor = playground.Compositor;
        Assert.True(compositor.Playing);
        compositor.Playing = false;
        compositor.Seek(1);
        await Settle(harness, playground, compositor);
        Assert.Equal(1, compositor.Frame.Seconds);
        var document = compositor.Sandbox.Graph.Editor.Document;
        foreach (var type in new[] { CompositorDomain.Ramp, CompositorDomain.Pulse, CompositorDomain.Over })
        {
            var node = document.Nodes.Single(n => n.TypeId == type.Id);
            Assert.Equal(24 * 12, compositor.Frame.Previews[node.Id].Pixels.Length);
        }
        var output = compositor.Frame.Previews[Assert.Single(compositor.Outputs)];
        for (var x = 0; x < 24; x += 7)
        {
            var luma = CompositorDomain.PulseAt(0.5, 1) * 0.5 + CompositorDomain.RampAt(x, 24, 0.25, 1) * 0.5;
            Assert.Equal(NodePreview.Pack((float)luma, (float)luma, (float)luma), output.Pixels[x]);
        }
        Assert.Equal("""
            # composition · 24 × 12 luma
            frame = over(
                top: pulse(0.50 Hz),
                bottom: ramp(0.25 widths/s),
                mix: envelope(time, 2.00 s))
            """.ReplaceLineEndings("\n"), compositor.Generated);
        Assert.Throws<ArgumentOutOfRangeException>(() => compositor.Seek(-1));
        compositor.Playing = true;
        await Settle(harness, playground, compositor);
        Assert.True(compositor.Frame.Seconds >= 1);
    }

    [Fact]
    public async Task CompositorReportsRenderFailuresAndCompileErrors()
    {
        using var harness = new FrameHarness(1400, 900);
        var playground = new GraphPlayground();
        playground.SelectScenario(5);
        var compositor = playground.Compositor;
        compositor.Playing = false;
        await Settle(harness, playground, compositor);
        var document = compositor.Sandbox.Graph.Editor.Document;
        var ramp = document.Nodes.Single(static n => n.TypeId == CompositorDomain.Ramp.Id);
        Assert.True(compositor.Sandbox.Graph.Editor.Execute(new SetPayloadCommand(ramp.Id, "bad")).Succeeded);
        await Settle(harness, playground, compositor);
        Assert.IsType<InvalidCastException>(compositor.Renderer.Error);
        Assert.False(compositor.Sandbox.Step(ramp.Id, 1).Succeeded);
        Assert.True(compositor.Sandbox.View.Undo().Succeeded);

        // A rule-free core editor admits a frame cycle, which only the dataflow compiler rejects.
        var over = document.GetNode(compositor.Sandbox.View.AddNode(CompositorDomain.Over, new GraphPoint(0, 900)));
        Assert.True(new GraphEditor(document).Connect(over.GetPort(CompositorDomain.OutKey).Id,
            over.GetPort(CompositorDomain.AKey).Id).Succeeded);
        harness.Frame(playground.Draw);
        Assert.Contains(compositor.Sandbox.View.Validation, static d => d.Severity == GraphSeverity.Error);
        Assert.Empty(compositor.Outputs);
        Assert.Same(PreviewSnapshot.Empty, compositor.Renderer.Snapshot);
        harness.Frame(playground.Draw);
    }

    [Fact]
    public void DomainSteppersFormatAndClampTheirPayloads()
    {
        var shader = new SandboxScene("Material", "material", new ShaderDomain());
        var domain = shader.Graph;
        var nodes = shader.View.Editor.Document.Nodes;
        var scalar = nodes.First(static n => n.TypeId == ShaderDomain.ScalarConstant.Id);
        var color = nodes.First(static n => n.TypeId == ShaderDomain.ColorConstant.Id);
        Assert.Equal("0.50", domain.FormatPayload(scalar));
        Assert.Equal(new ScalarPayload(0.55f), domain.Stepped(scalar, 1));
        Assert.Equal("#F2EBD9", domain.FormatPayload(color));
        var rotated = ((ColorPayload)domain.Stepped(color, 12)!).Value;
        Assert.Equal(((ColorPayload)color.Payload!).Value.R, rotated.R, 3);
        Assert.Null(domain.Stepped(nodes.First(static n => n.TypeId == ShaderDomain.Lerp.Id), 1));
        Assert.Equal("", domain.FormatPayload(nodes.First(static n => n.TypeId == ShaderDomain.Lerp.Id)));
        Assert.StartsWith("Parameter · tint", nodes.Select(domain.Describe).Single(static t => t.Contains("tint")),
            StringComparison.Ordinal);

        var compositor = new SandboxScene("Video", "video", new CompositorDomain());
        var media = compositor.Graph;
        var all = compositor.View.Editor.Document.Nodes;
        var pulse = all.Single(static n => n.TypeId == CompositorDomain.Pulse.Id);
        var envelope = all.Single(static n => n.TypeId == CompositorDomain.Envelope.Id);
        Assert.Equal("0.50 Hz", media.FormatPayload(pulse));
        Assert.Equal(new MediaPayload(0), media.Stepped(pulse, -4));
        Assert.Equal(new MediaPayload(0.5), media.Stepped(envelope, -9));
        Assert.Equal("2.00 s", media.FormatPayload(envelope));
        Assert.Equal("Output · frame", media.Describe(all.Single(static n => n.TypeId == DataflowNodes.Output.Id)));
        Assert.Equal(1, CompositorDomain.EnvelopeAt(3, 0));
    }

    [Fact]
    public void PreviewPixelsClampAndDrawAsCells()
    {
        Assert.Equal(0xFF0000FFu, NodePreview.Pack(2, -1, float.NaN));
        Assert.Equal(new Rgba(0.5f, 0.5f, 0.5f, 0.5f), Rgba.Lerp(Rgba.Splat(0), Rgba.Splat(1), 0.5f));
        using var harness = new FrameHarness(40, 40);
        var preview = new NodePreview(2, 1, [0xFF0000FF, 0x0000FFFF]);
        harness.Frame(gui =>
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                preview.Draw(gui, new Rect(0, 0, 40, 40));
            }
        });
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Red, pixels.GetPixel(10, 20));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(30, 20));
    }

    static uint Expected(Rgba texel)
    {
        var color = Rgba.Lerp(texel * new Rgba(1, 0.65f, 0.35f, 1), Rgba.Splat(0.5f), 0.25f);
        return NodePreview.Pack(color.R, color.G, color.B);
    }

    static async Task Settle(FrameHarness harness, GraphPlayground playground, PreviewScene scene)
    {
        harness.Frame(playground.Draw);
        await scene.Renderer.Completion;
        harness.Frame(playground.Draw);
        await scene.Renderer.Completion;
        harness.Frame(playground.Draw);
    }
}
