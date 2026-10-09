namespace Guinevere;

/// <summary>Reads font roles from stylesheet tokens and applies them to scopes.</summary>
public static class StyledFonts
{
    extension(Gui gui)
    {
        /// <summary>Resolves font-ui, font-ui-mono, font-code, font-icon or font-emoji with scope token overrides.</summary>
        public Font? ResolveFontRole(FontRole role, float size = 12f, int weight = 400, bool italic = false,
            LayoutNodeScope? scope = null)
        {
            var sheets = gui.StyleSheets;
            StylingExtensions.FontsOf(gui).Synchronize(sheets);
            var token = RoleToken(role);
            scope ??= gui.LayoutNodeScopeStack.Count > 0 ? gui.CurrentNodeScope : null;
            var scoped = scope?.Get<StyleTokens>().Values;
            var families = scoped?.GetValueOrDefault($"--{token}") ?? sheets.GetToken(token);
            if (families is null) return gui.Fonts.ResolveRole(role, size, weight, italic);
            families = StyleSheet.Compute(families,
                name => scoped?.GetValueOrDefault(name) ?? sheets.GetToken(name));
            return gui.Fonts.Resolve(families, size, weight, italic);
        }

        /// <summary>Applies a token-selected font role to the scope; icon and emoji roles set their glyph slots.</summary>
        public void UseFontRole(FontRole role, LayoutNodeScope? scope = null)
        {
            scope ??= gui.CurrentNodeScope;
            if (gui.ResolveFontRole(role, scope: scope) is not { } font) return;
            if (role == FontRole.Icon) gui.SetWidgetIconFont(font, scope);
            else if (role == FontRole.Emoji) gui.SetEmojiFont(font, scope);
            else gui.SetTextFont(font, scope);
        }
    }

    static string RoleToken(FontRole role) => role switch
    {
        FontRole.Ui => "font-ui",
        FontRole.UiMono => "font-ui-mono",
        FontRole.Code => "font-code",
        FontRole.Icon => "font-icon",
        FontRole.Emoji => "font-emoji",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
