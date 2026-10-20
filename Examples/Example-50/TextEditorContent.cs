using Guinevere;

namespace Controls01;

public abstract partial class Program
{
    const string ShaderSample = """
        #version 330 core
        // A shader to edit: Ctrl+D adds the next match, Alt+click adds a caret,
        // Ctrl+F finds, Ctrl+/ comments, Ctrl+Space completes, and hovering mix explains it.
        uniform float time;
        uniform vec2 resolution;
        out vec4 color;

        float circle(vec2 point, float radius) {
            return smoothstep(radius, radius - 0.01, length(point));
        }

        void main() {
            vec2 uv = (gl_FragCoord.xy - 0.5 * resolution) / resolution.y;
            float pulse = 0.5 + 0.5 * sin(time * 2);
            vec3 inside = vec3(0.9, 0.4, 0.2);
            vec3 outside = vec3(0.1, 0.1, 0.15);
            color = vec4(mix(outside, inside, circle(uv, 0.3 * pulse)), 1.0);
        }
        """;

    static readonly Dictionary<string, string> ShaderDocs = new()
    {
        ["mix"] = "`mix(x, y, a)` blends **x** and **y** linearly: `x * (1 - a) + y * a`.",
        ["smoothstep"] = "`smoothstep(edge0, edge1, x)` eases from 0 to 1 as **x** goes from edge0 to edge1.",
        ["length"] = "`length(v)` is the Euclidean length of the vector **v**.",
        ["sin"] = "`sin(angle)` is the sine of an angle in radians.",
    };

    /// <summary>The demo's editor, kept across frames.</summary>
    internal static TextEditorState? TextEditorDemoState => _textEditor;

    static TextEditorState? _textEditor;

    /// <summary>A GLSL editor with a toy linter and hover documentation, and a way to load a huge document.</summary>
    internal static void TextEditorContent(Gui gui)
    {
        _textEditor ??= new TextEditorState(ShaderSample, LanguageServices.ForLanguage("glsl") with
        {
            Diagnostics = new ShaderLinter(),
            Hover = new ShaderHover(),
        });
        var editor = _textEditor;
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter())
        {
            using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(8).ContentAlignY(0.5f).Enter())
            {
                if (gui.Button("Shader sample"))
                {
                    DeferLayout(() => editor.Text = ShaderSample);
                }

                if (gui.Button("100 000 lines"))
                {
                    DeferLayout(() => editor.Text = string.Join("\n", Enumerable.Range(1, 100_000)
                        .Select(i => $"float value{i} = sin({i}.0) * {i % 97}.0; // line {i}")));
                }

                gui.DrawText(TextEditorStatus(editor), 12, Token(gui, "text"));
            }

            var options = new TextEditorOptions
            {
                FontSize = 14,
                Theme = _radioChoice is 0 or 2 ? TextEditorTheme.Light : TextEditorTheme.Dark,
            };
            using (gui.Node().Expand().Enter())
            {
                gui.TextEditor(editor, options);
            }
        }
    }

    static string TextEditorStatus(TextEditorState editor)
    {
        var document = editor.Document;
        var caret = document.Primary.Caret;
        var line = document.Buffer.LineOf(caret);
        var column = caret - document.Buffer.LineStart(line);
        var problems = editor.Diagnostics.Count;
        return $"Ln {line + 1}, Col {column + 1} · {document.Selections.Count} caret(s) · "
            + $"{document.Buffer.LineCount:N0} lines · {problems} problem(s)";
    }

    /// <summary>Warns about integer literals passed where GLSL wants floats, like <c>sin(time * 2)</c>.</summary>
    sealed class ShaderLinter : IDiagnosticsProvider
    {
        public Task<IReadOnlyList<TextDiagnostic>> GetDiagnosticsAsync(TextSnapshot snapshot,
            CancellationToken cancellation) => Task.Run<IReadOnlyList<TextDiagnostic>>(() =>
        {
            var text = snapshot.Buffer.Length > 200_000 ? "" : snapshot.Buffer.Text;
            return [.. System.Text.RegularExpressions.Regex.Matches(text, @"(?<=\*\s*)\d+(?![\.\d])")
                .Select(match => new TextDiagnostic(match.Index, match.Length, DiagnosticSeverity.Warning,
                    $"Integer `{match.Value}` in a float expression; write `{match.Value}.0`."))];
        }, cancellation);
    }

    /// <summary>Documentation for a few built-in functions.</summary>
    sealed class ShaderHover : IHoverProvider
    {
        public Task<HoverInfo?> GetHoverAsync(TextSnapshot snapshot, int offset, CancellationToken cancellation)
        {
            var (start, end) = TextNavigation.WordAt(snapshot.Buffer, offset);
            var word = snapshot.Buffer.GetText(start, end - start);
            return Task.FromResult(ShaderDocs.TryGetValue(word, out var doc)
                ? new HoverInfo(start, end - start, doc)
                : null);
        }
    }
}
