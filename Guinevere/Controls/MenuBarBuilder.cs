namespace Guinevere;

/// <summary>
/// Builder for creating menu bars
/// </summary>
public class MenuBarBuilder
{
    private readonly Gui _gui;
    private readonly float _height;
    private readonly Color? _textColor;
    private readonly Color? _hoverColor;
    private readonly float _fontSize;
    private readonly float _padding;
    private readonly Dictionary<string, bool> _menuStates = new();

    public MenuBarBuilder(Gui gui, float height, Color? textColor, Color? hoverColor, float fontSize, float padding)
    {
        _gui = gui;
        _height = height;
        _textColor = textColor;
        _hoverColor = hoverColor;
        _fontSize = fontSize;
        _padding = padding;
    }

    /// <summary>
    /// Adds a menu to the menu bar
    /// </summary>
    public MenuBarBuilder Menu(string text, Action<FlyoutBuilder> buildMenu)
    {
        // Get or create menu state
        var menuId = $"menubar_{text}";
        var isOpen = _menuStates.TryGetValue(menuId, out var state) && state;

        using (_gui.Node().Height(_height).Padding(_padding, 0).Enter())
        {
            bool isHovered;
            bool isClicked;
            var hasFocus = false;

            if (_gui.Pass == Pass.Pass2Render)
            {
                // Register as focusable for keyboard navigation
                _gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
                var interactable = _gui.GetInteractable();
                isHovered = interactable.OnHover();
                isClicked = interactable.OnClick();
                hasFocus = _gui.HasFocus();

                // Draw focus indicator if focused
                if (hasFocus)
                {
                    var rect = _gui.CurrentNode.Rect;
                    var focusRect = new Rect(rect.X - 2, rect.Y - 2, rect.W + 4, rect.H + 4);
                    _gui.DrawRectBorder(focusRect, Color.FromArgb(255, 100, 149, 237), 2f, 6);
                }

                // Keyboard navigation: Enter/Space to open/close
                var activated = isClicked;
                if (hasFocus && (_gui.Input.IsKeyPressed(KeyboardKey.Space) || _gui.Input.IsKeyPressed(KeyboardKey.Enter)))
                {
                    activated = true;
                }
                if (activated)
                {
                    isOpen = !isOpen;
                    _menuStates[menuId] = isOpen;
                }
            }

            _gui.DrawText(text, _fontSize, _textColor ?? Color.Black, centerInRect: false);
        }

        // Render flyout if open
        if (isOpen)
        {
            var rect = _gui.CurrentNode.Rect;
            var menuPosition = new Vector2(rect.X, rect.Y + rect.H);

            _gui.Flyout(ref isOpen, menuPosition, buildMenu);
            _menuStates[menuId] = isOpen; // Update state after flyout handling
        }

        return this;
    }
}
