using Guinevere;

namespace Example_75_PaperUI_Dashboard;

public abstract class Program
{
    static Gui _gui = null!;
    static GuiWindow _win = null!;

    // State variables
    static int _selectedTab;
    static double _sliderValue = 0.5;
    static readonly bool[] ToggleStates = [true, false, true, false, true];

    static bool _isDarkTheme = true;
    static double _time;
    const float CardBorder = 8;

    // Theme colors
    static Color _backgroundColor;
    static Color _cardBackground;
    static Color _primaryColor;
    static Color _secondaryColor;
    static Color _textColor;
    static Color _lightTextColor;
    static Color[] _colorPalette = null!;

    // Example data
    static readonly string[] TabNames = ["Dashboard", "Analytics", "Profile", "Settings"];
    static readonly double[] ChartData = [0.2, 0.5, 0.3, 0.8, 0.4, 0.7, 0.6];

    public static void Main()
    {
        InitializeTheme();

        _gui = new Gui();
        _win = new GuiWindow(_gui, 1100, 850);

        _win.RunGui(() =>
        {
            _time += _gui.Time.DeltaTime;

            // Main background
            _gui.DrawBackgroundRect(_backgroundColor);

            // Main container
            using (_gui.Node().Expand().Margin(15).Gap(10).Enter())
            {
                // Top navigation bar
                RenderTopNavBar();

                // Main content area
                using (_gui.Node().Expand().Direction(Axis.Horizontal).Gap(15).Enter())
                {
                    // Left sidebar
                    RenderSidebar();

                    // Main content
                    RenderMainContent();
                }

                // Footer
                RenderFooter();
            }
        });
    }

    static void InitializeTheme()
    {
        if (_isDarkTheme)
        {
            _backgroundColor = Color.FromArgb(18, 18, 23);
            _cardBackground = Color.FromArgb(30, 30, 46);
            _primaryColor = Color.FromArgb(94, 104, 202);
            _secondaryColor = Color.FromArgb(162, 155, 254);
            _textColor = Color.FromArgb(226, 232, 240);
            _lightTextColor = Color.FromArgb(148, 163, 184);
            _colorPalette =
            [
                Color.FromArgb(94, 234, 212),
                Color.FromArgb(162, 155, 254),
                Color.FromArgb(249, 115, 22),
                Color.FromArgb(248, 113, 113),
                Color.FromArgb(250, 204, 21)
            ];
        }
        else
        {
            _backgroundColor = Color.FromArgb(243, 244, 246);
            _cardBackground = Color.White;
            _primaryColor = Color.FromArgb(59, 130, 246);
            _secondaryColor = Color.FromArgb(16, 185, 129);
            _textColor = Color.FromArgb(31, 41, 55);
            _lightTextColor = Color.FromArgb(107, 114, 128);
            _colorPalette =
            [
                Color.FromArgb(59, 130, 246),
                Color.FromArgb(16, 185, 129),
                Color.FromArgb(239, 68, 68),
                Color.FromArgb(245, 158, 11),
                Color.FromArgb(139, 92, 246)
            ];
        }
    }

    static void RenderTopNavBar()
    {
        using (_gui.Node().Height(70).Direction(Axis.Horizontal).AlignContent(.5f).Enter())
        {
            _gui.DrawBackgroundRect(_cardBackground, CardBorder);

            // Logo section
            using (_gui.Node(180).Margin(15).Enter())
            {
                _gui.DrawText("📊 Dashboard", 20, _textColor);
            }

            // Spacer to push right items to the right
            _gui.Node().Expand();

            // Search bar
            using (_gui.Node(300, 40).Margin(10, 0).Enter())
            {
                _gui.DrawBackgroundRect(Color.FromArgb(50, 0, 0, 0), CardBorder);
                using (_gui.Node().Expand().Margin(10).Enter())
                {
                    _gui.DrawText("🔍 Search...", 14, _lightTextColor);
                }
            }

            // Theme toggle
            using (_gui.Node(40, 40).Margin(5, 0).AlignContent(.5f).Enter())
            {
                var element = _gui.GetInteractable();
                var buttonColor = element.On(Interactions.Hover) ? _secondaryColor : _primaryColor;

                _gui.DrawBackgroundRect(buttonColor, 20);
                _gui.DrawText(_isDarkTheme ? "☀" : "🌙", 16);

                if (element.OnClick())
                {
                    _isDarkTheme = !_isDarkTheme;
                    InitializeTheme();
                }
            }

            // User profile
            using (_gui.Node(40, 40).AlignSelf(0.5f).Margin(15, 10).AlignContent(.5f).Enter())
            {
                _gui.DrawBackgroundRect(_secondaryColor, 20);
                _gui.DrawText("👤", 16, Color.White);
            }
        }
    }

    static void RenderSidebar()
    {
        using (_gui.Node(240).Enter())
        {
            _gui.DrawBackgroundRect(_cardBackground, CardBorder);

            using (_gui.Node().Expand().Margin(15).Gap(10).Enter())
            {
                // Menu header
                _gui.DrawText("Menu", 26, _textColor).ExpandWidth().AlignContent(0.5f);

                // Menu items
                string[] menuIcons = ["🏠", "📊", "👤", "⚙️"];

                for (var i = 0; i < TabNames.Length; i++)
                {
                    using (_gui.Node().Height(50).Direction(Axis.Horizontal).Gap(10).Enter())
                    {
                        var element = _gui.GetInteractable();
                        var isSelected = i == _selectedTab;
                        var isHovered = element.On(Interactions.Hover);

                        var bgColor = isSelected ? Color.FromArgb(50, _primaryColor) :
                            isHovered ? Color.FromArgb(25, _primaryColor) : Color.Transparent;

                        _gui.DrawBackgroundRect(bgColor, CardBorder);

                        if (isSelected)
                        {
                            var borderRect = new Rect(_gui.CurrentNode.Rect.X, _gui.CurrentNode.Rect.Y,
                                3, _gui.CurrentNode.Rect.H);
                            _gui.DrawRect(borderRect, _primaryColor);
                        }

                        // Icon
                        using (_gui.Node(30, 30).AlignSelf(0.5f).Margin(10, 0).Enter())
                        {
                            _gui.DrawText(menuIcons[i], 16);
                        }

                        // Text
                        using (_gui.Node().Expand().AlignSelf(0.5f).Enter())
                        {
                            _gui.DrawText(TabNames[i], 14, _textColor);
                        }

                        if (element.OnClick())
                        {
                            _selectedTab = i;
                        }
                    }
                }

                // Upgrade section
                using (_gui.Node().Height(120).Margin(0, 20).Enter())
                {
                    _gui.DrawBackgroundRect(_primaryColor, CardBorder);

                    using (_gui.Node().Expand().Margin(15).Gap(10).AlignContent(0.5f).Enter())
                    {
                        _gui.DrawText("Upgrade to Pro", 14, Color.White);

                        using (_gui.Node().Height(30).Enter())
                        {
                            _gui.DrawBackgroundRect(Color.White, 15);
                            _gui.DrawText("Upgrade", 12, _primaryColor);
                        }
                    }
                }
            }
        }
    }

    static void RenderMainContent()
    {
        using (_gui.Node().Expand().Enter())
        {
            using (_gui.Node().Expand().Gap(15).Enter())
            {
                // Tab navigation
                RenderTabNavigation();

                // Tab content
                switch (_selectedTab)
                {
                    case 0: RenderDashboardTab(); break;
                    case 1: RenderAnalyticsTab(); break;
                    case 2: RenderProfileTab(); break;
                    case 3: RenderSettingsTab(); break;
                }
            }
        }
    }

    static void RenderTabNavigation()
    {
        using (_gui.Node().Height(50).Direction(Axis.Horizontal).Enter())
        {
            _gui.DrawBackgroundRect(_cardBackground, CardBorder);

            for (var i = 0; i < TabNames.Length; i++)
            {
                using (_gui.Node().Expand().AlignContent(0.5f).Enter())
                {
                    var element = _gui.GetInteractable();
                    var isSelected = i == _selectedTab;
                    var textColor = isSelected ? _primaryColor : _lightTextColor;

                    _gui.DrawText(TabNames[i], 16, textColor);

                    if (isSelected)
                    {
                        var indicatorRect = new Rect(_gui.CurrentNode.Rect.X,
                            _gui.CurrentNode.Rect.Y + _gui.CurrentNode.Rect.H - 3,
                            _gui.CurrentNode.Rect.W, 3);
                        _gui.DrawRect(indicatorRect, _primaryColor);
                    }

                    if (element.OnClick())
                    {
                        _selectedTab = i;
                    }
                }
            }
        }
    }

    static void RenderDashboardTab()
    {
        using (_gui.Node().Expand().Gap(20).Enter())
        {
            // Cards row with hover-only effects
            using (_gui.Node().Height(120).Direction(Axis.Horizontal).Gap(15).Enter())
            {
                string[] statNames = ["Total Users", "Revenue", "Projects", "Conversion"];
                string[] statValues = ["3,456", "$12,345", "24", "8.5%"];

                for (var i = 0; i < 4; i++)
                {
                    using (_gui.Node().Expand().Enter())
                    {
                        var element = _gui.GetInteractable();
                        var isHovered = element.On(Interactions.Hover);
                        var node = _gui.CurrentNode;

                        using (_gui.Node().Expand().Margin(2).Enter())
                        {
                            _gui.DrawBackgroundRect(_cardBackground, CardBorder);
                            if (isHovered)
                            {
                                _gui.DrawRectBorder(_gui.CurrentNode.Rect, _colorPalette[i], 2, CardBorder);
                                node.Size(node.Rect.W + 4);
                            }

                            using (_gui.Node().Expand().Margin(15).Gap(5).Enter())
                            {
                                // Icon - only animates on hover
                                using (_gui.Node(40, 40).Enter())
                                {
                                    if (isHovered)
                                    {
                                        _gui.DrawBackgroundRect(_colorPalette[i], _gui.CurrentNode.Rect.W / 2);
                                    }
                                    else
                                    {
                                        _gui.DrawBackgroundRect(_colorPalette[i], CardBorder);
                                    }
                                }

                                // Label
                                using (_gui.Node().Height(20).AlignContent(0.0f).Enter())
                                {
                                    _gui.DrawText(statNames[i], 12, _lightTextColor);
                                }

                                // Value
                                using (_gui.Node().Height(30).AlignContent(0.0f).Enter())
                                {
                                    _gui.DrawText(statValues[i], 24, _textColor);
                                }
                            }
                        }
                    }
                }
            }

            // Chart section
            using (_gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
            {
                // Main chart
                using (_gui.Node().Expand().Enter())
                {
                    _gui.DrawBackgroundRect(Color.FromArgb(250, _cardBackground), CardBorder);

                    using (_gui.Node().Expand().Margin(20).Gap(10).Enter())
                    {
                        // Chart header
                        using (_gui.Node().Height(40).Direction(Axis.Horizontal).Enter())
                        {
                            using (_gui.Node().Expand().AlignContent(0.5f).Enter())
                            {
                                _gui.DrawText("Performance Overview", 18, _textColor);
                            }

                            // Period buttons
                            string[] periods = ["Day", "Week", "Month", "Year"];
                            using (_gui.Node(280, 30).Direction(Axis.Horizontal).Gap(5).Enter())
                            {
                                foreach (var period in periods)
                                {
                                    using (_gui.Node(60, 30).Enter())
                                    {
                                        var isActive = period == "Week";
                                        var btnColor = isActive ? _primaryColor : Color.FromArgb(50, 0, 0, 0);
                                        var txtColor = isActive ? Color.White : _lightTextColor;

                                        _gui.DrawBackgroundRect(btnColor, CardBorder);
                                        _gui.DrawText(period, 12, txtColor);
                                    }
                                }
                            }
                        }

                        // Chart area
                        using (_gui.Node().Expand().Enter())
                        {
                            DrawSimpleChart();
                        }
                    }
                }

                // Activity panel
                using (_gui.Node(300).Enter())
                {
                    _gui.DrawBackgroundRect(Color.FromArgb(250, _cardBackground), CardBorder);

                    using (_gui.Node().Expand(.3f).Margin(15).Gap(10).Enter())
                    {
                        // Header
                        _gui.DrawText("Recent Activity", 16, _textColor);

                        // Activity items
                        string[] activities =
                        [
                            "John updated the project",
                            "Alice completed a task",
                            "New user registered",
                            "Project deadline updated",
                            "Team meeting scheduled"
                        ];

                        string[] times = ["5m ago", "23m ago", "1h ago", "2h ago", "3h ago"];

                        for (var i = 0; i < activities.Length; i++)
                        {
                            using (_gui.Node().Height(50).Direction(Axis.Horizontal).Gap(10).Enter())
                            {
                                // Animated activity dot with pulsing effect
                                using (_gui.Node(50, CardBorder).AlignSelf(0.3f).Enter())
                                {
                                    var dotRect = new Rect(
                                        _gui.CurrentNode.Rect.X - 6f,
                                        _gui.CurrentNode.Rect.Y + 6f,
                                        40, 40);
                                    _gui.DrawRect(dotRect, _colorPalette[i % _colorPalette.Length], 10);
                                }

                                // Activity text
                                using (_gui.Node().Expand().Gap(2).Enter())
                                {
                                    using (_gui.Node().Height(20).AlignContent(0.0f).Enter())
                                    {
                                        _gui.DrawText(activities[i], 12, _textColor);
                                    }

                                    using (_gui.Node().Height(15).AlignContent(0.0f).Enter())
                                    {
                                        _gui.DrawText(times[i], 10, _lightTextColor);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    static void RenderAnalyticsTab()
    {
        using (_gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
        {
            // Left panel with analytics
            using (_gui.Node().Expand().Gap(20).Enter())
            {
                // Analytics header
                using (_gui.Node().Height(60).AlignContent(0.5f).Enter())
                {
                    _gui.DrawText("Analytics Dashboard", 24, _textColor);
                }

                // Interactive slider demo
                using (_gui.Node().Height(100).Gap(10).Enter())
                {
                    using (_gui.Node().Height(30).AlignContent(0.5f).Enter())
                    {
                        _gui.DrawText($"Data Value: {_sliderValue:F2}", 16, _textColor);
                    }

                    using (_gui.Node().Height(20).Enter())
                    {
                        var sliderRect = _gui.CurrentNode.Rect;
                        var element = _gui.GetInteractable();

                        // Track
                        _gui.DrawRect(sliderRect, Color.FromArgb(100, _lightTextColor), 10);

                        // Fill
                        var fillRect = new Rect(sliderRect.X, sliderRect.Y,
                            sliderRect.W * (float)_sliderValue, sliderRect.H);
                        _gui.DrawRect(fillRect, _primaryColor, 10);

                        // Handle
                        var handleX = sliderRect.X + sliderRect.W * (float)_sliderValue - 10;
                        var handleRect = new Rect(handleX, sliderRect.Y - 5, 20, 30);
                        _gui.DrawRect(handleRect, _textColor, 10);

                        if (element.On(Interactions.Hold))
                        {
                            var relativeX = _gui.Input.MousePosition.X - sliderRect.X;
                            _sliderValue = Math.Clamp(relativeX / sliderRect.W, 0, 1);
                        }
                    }
                }

                // Pie chart visualization
                using (_gui.Node().Expand().Enter())
                {
                    DrawPieChart();
                }
            }

            // Right panel with data items
            using (_gui.Node().Expand().Padding(10).Gap(10).Enter())
            {
                for (var i = 0; i < 10; i++)
                {
                    using (_gui.Node().Height(75).ExpandWidth().Direction(Axis.Horizontal).Padding(10).Gap(10).Enter())
                    {
                        var barHue = (_time * 40 + i * 60) % 360;
                        var barColor = HsvToColor(barHue, 0.6, 0.8);
                        _gui.DrawBackgroundRect(barColor, 10);
                        using (_gui.Node(50, 50).Gap(2).Enter())
                        {
                            _gui.DrawCircle(_gui.CurrentNode.Rect.Center, 25, _textColor);
                        }

                        // Data text
                        using (_gui.Node().Expand().Gap(2).Enter())
                        {
                            using (_gui.Node().Height(20).AlignContent(0.0f).Enter())
                            {
                                _gui.DrawText($"Item {i}", 18, _textColor);
                            }

                            using (_gui.Node().Height(15).AlignContent(0.0f).Enter())
                            {
                                _gui.DrawText($"Interactive with animations", 10, _textColor);
                            }
                        }
                    }
                }
            }
        }
    }

    static void RenderProfileTab()
    {
        using (_gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
        {
            // Profile info
            using (_gui.Node().Expand(.4f).Enter())
            {
                _gui.DrawBackgroundRect(Color.FromArgb(250, _cardBackground), CardBorder);

                using (_gui.Node().Expand().Margin(20).Gap(15).AlignContent(0.5f).Enter())
                {
                    // Avatar
                    using (_gui.Node(100, 100).AlignContent(.5f).Enter())
                    {
                        _gui.DrawBackgroundRect(_secondaryColor);
                        _gui.DrawText("J", 32, Color.White);
                    }

                    // Name
                    using (_gui.Node().Height(40).AlignContent(0.5f).Enter())
                    {
                        _gui.DrawText("John Doe", 24, _textColor);
                    }

                    // Title
                    using (_gui.Node().Height(30).AlignContent(0.5f).Enter())
                    {
                        _gui.DrawText("Senior Developer", 16, _lightTextColor);
                    }

                    // Stats
                    using (_gui.Node().Height(80).Direction(Axis.Horizontal).Enter())
                    {
                        string[] labels = ["Projects", "Tasks", "Teams"];
                        string[] values = ["24", "148", "5"];

                        for (var i = 0; i < 3; i++)
                        {
                            using (_gui.Node().Expand().Gap(5).AlignContent(0.5f).Enter())
                            {
                                using (_gui.Node().Height(30).AlignContent(0.5f).Enter())
                                {
                                    _gui.DrawText(values[i], 20, _primaryColor);
                                }

                                using (_gui.Node().Height(20).AlignContent(0.5f).Enter())
                                {
                                    _gui.DrawText(labels[i], 12, _lightTextColor);
                                }
                            }
                        }
                    }
                }
            }

            // Skills and activity
            using (_gui.Node().Expand(.6f).Gap(20).Enter())
            {
                // Activity tracker with the animated contribution grid
                using (_gui.Node().Expand().Margin(20).Gap(15).Enter())
                {
                    _gui.DrawBackgroundRect(Color.FromArgb(250, _cardBackground), CardBorder);
                    using (_gui.Node().Height(30).AlignContent(0.5f).Enter())
                    {
                        _gui.DrawText("Activity Tracker", 18, _textColor);
                    }

                    // Monthly contribution grid (7 columns x 4 rows)
                    using (_gui.Node().Expand().Margin(10).Direction(Axis.Horizontal).Enter())
                    {
                        var columns = 7; // Days of the week
                        var rows = 4; // Weeks in month

                        string[] days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

                        for (var day = 0; day < columns; day++)
                        {
                            using (_gui.Node(50).ExpandHeight().Gap(10).Enter())
                            {
                                _gui.DrawText(days[day], color: _textColor);
                                for (var week = 0; week < rows; week++)
                                {
                                    var speedMultiplier =
                                        5.0f; // Adjust this to make the oscillation faster or slower

                                    var intensity = Math.Sin(
                                        _time * speedMultiplier +
                                        day * 0.3f +
                                        week * 0.3f
                                    ) * 0.5f + 0.5f;

                                    var cellColor = Color.FromArgb(
                                        (int)(50 + intensity * 200),
                                        _primaryColor);

                                    using (_gui.Node(35, 35).Enter())
                                    {
                                        _gui.DrawBackgroundRect(cellColor, CardBorder);
                                    }
                                }
                            }
                        }
                    }
                }

                // Skills section
                using (_gui.Node().Expand().Enter())
                {
                    _gui.DrawBackgroundRect(Color.FromArgb(250, _cardBackground), CardBorder);

                    using (_gui.Node().Expand().Margin(20).Gap(15).Enter())
                    {
                        using (_gui.Node().Height(30).AlignContent(0.5f).Enter())
                        {
                            _gui.DrawText("Skills", 18, _textColor);
                        }

                        string[] skills = ["Programming", "Design", "Communication", "Leadership"];
                        double[] levels = [0.9, 0.75, 0.8, 0.6];

                        for (var i = 0; i < skills.Length; i++)
                        {
                            using (_gui.Node().Height(50).Gap(8).Enter())
                            {
                                using (_gui.Node().Height(20).AlignContent(0.0f).Enter())
                                {
                                    _gui.DrawText(skills[i], 14, _textColor);
                                }

                                using (_gui.Node().Height(12).Enter())
                                {
                                    var barRect = _gui.CurrentNode.Rect;
                                    _gui.DrawRect(barRect, Color.FromArgb(100, _lightTextColor), 6);

                                    var fillRect = new Rect(barRect.X, barRect.Y,
                                        barRect.W * (float)levels[i], barRect.H);
                                    _gui.DrawRect(fillRect, _colorPalette[i % _colorPalette.Length], 6);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    static void RenderSettingsTab()
    {
        using (_gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
        {
            // Settings categories
            using (_gui.Node(200).Enter())
            {
                _gui.DrawBackgroundRect(Color.FromArgb(250, _cardBackground), CardBorder);

                using (_gui.Node().Expand().Margin(15).Gap(5).Enter())
                {
                    string[] categories = ["General", "Account", "Appearance", "Privacy"];

                    for (var i = 0; i < categories.Length; i++)
                    {
                        using (_gui.Node().Height(40).Enter())
                        {
                            var isSelected = i == 0;
                            var bgColor = isSelected ? Color.FromArgb(50, _primaryColor) : Color.Transparent;
                            var textColor = isSelected ? _primaryColor : _textColor;

                            _gui.DrawBackgroundRect(bgColor, CardBorder);
                            _gui.DrawText(categories[i], 14, textColor);
                        }
                    }
                }
            }

            // Settings options
            using (_gui.Node().Expand().Enter())
            {
                _gui.DrawBackgroundRect(Color.FromArgb(250, _cardBackground), CardBorder);

                using (_gui.Node().Expand().Margin(20).Gap(15).Enter())
                {
                    using (_gui.Node().Height(40).AlignContent(0.5f).Enter())
                    {
                        _gui.DrawText("General Settings", 20, _textColor);
                    }

                    string[] options =
                    [
                        "Enable notifications",
                        "Dark mode",
                        "Auto-save changes",
                        "Show analytics"
                    ];

                    for (var i = 0; i < options.Length; i++)
                    {
                        using (_gui.Node().Height(50).Direction(Axis.Horizontal).Enter())
                        {
                            using (_gui.Node().Expand().AlignSelf(0.5f).Enter())
                            {
                                _gui.DrawText(options[i], 16, _textColor);
                            }

                            // Toggle switch
                            using (_gui.Node(60, 30).AlignSelf(0.5f).Enter())
                            {
                                var element = _gui.GetInteractable();
                                var isOn = ToggleStates[i];
                                var switchColor = isOn ? _primaryColor : Color.FromArgb(100, _lightTextColor);

                                _gui.DrawBackgroundRect(switchColor, 15);

                                // Toggle dot
                                float dotX = isOn ? 35 : 5;
                                var dotRect = new Rect(_gui.CurrentNode.Rect.X + dotX,
                                    _gui.CurrentNode.Rect.Y + 3, 24, 24);
                                _gui.DrawRect(dotRect, Color.White, 12);

                                if (element.OnClick())
                                {
                                    ToggleStates[i] = !ToggleStates[i];
                                }
                            }
                        }
                    }

                    // Save button
                    using (_gui.Node().Height(50).Margin(0, 20).Enter())
                    {
                        var element = _gui.GetInteractable();
                        var btnColor = element.On(Interactions.Hover) ? _secondaryColor : _primaryColor;

                        _gui.DrawBackgroundRect(btnColor, CardBorder);
                        _gui.DrawText("Save Changes", 16, Color.White);
                    }
                }
            }
        }
    }

    static void RenderFooter()
    {
        using (_gui.Node().Height(50).Direction(Axis.Horizontal).Gap(10).AlignContent(.5f).Padding(10).Enter())
        {
            _gui.DrawBackgroundRect(_cardBackground, CardBorder);

            _gui.DrawText("© 2025 Dashboard Demo", 12, _lightTextColor);
            _gui.Node().Expand();
            _gui.DrawText($"{_gui.Time.DeltaTime * 1000:F4} ms", 12, _lightTextColor);
            _gui.DrawText($"{_gui.Time.SmoothFps:N0} FPS", 12, _lightTextColor);
        }
    }

    static void DrawSimpleChart()
    {
        var rect = _gui.CurrentNode.Rect;

        // Draw the grid background
        for (var i = 0; i <= 5; i++)
        {
            var y = rect.Y + (rect.H / 5) * i;
            var gridRect = new Rect(rect.X, y, rect.W, 1);
            _gui.DrawRect(gridRect, Color.FromArgb(50, _lightTextColor));
        }

        // Draw data points as simple bars
        var barWidth = rect.W / ChartData.Length * 0.8f;
        var spacing = rect.W / ChartData.Length;

        for (var i = 0; i < ChartData.Length; i++)
        {
            var animatedValue = (float)(ChartData[i] + Math.Sin(_time + i * 0.5) * 0.1);
            animatedValue = Math.Clamp(animatedValue, 0.1f, 1.0f);

            var barHeight = rect.H * animatedValue;
            var x = rect.X + i * spacing + (spacing - barWidth) / 2;
            var y = rect.Y + rect.H - barHeight;

            var barRect = new Rect(x, y, barWidth, barHeight);
            _gui.DrawRect(barRect, _primaryColor, 4);
        }
    }

    static void DrawPieChart()
    {
        var rect = _gui.CurrentNode.Rect;
        var center = rect.Center;
        var radius = Math.Min(rect.W, rect.H) * 0.4f * .5f;

        double[] values = [_sliderValue, 0.2, 0.15, 0.25, 0.1];
        var total = values.Sum();

        _gui.DrawCircle(center, radius * 2 + 2, _textColor);

        // Normalize values
        for (var i = 0; i < values.Length; i++)
        {
            values[i] /= total;
        }

        // var startAngle = (float)(_time * 5); // Slow rotation animation
        var startAngle = 0f;

        for (var i = 0; i < values.Length; i++)
        {
            var sweepAngle = (float)(values[i] * 360);

            // Create proper arc shape for pie slice
            var arcShape = Shape.Pie(radius, radius, Angle.Degrees(startAngle), Angle.Degrees(sweepAngle));
            _gui.DrawShape(center, arcShape).SolidColor(_colorPalette[i]);

            startAngle += sweepAngle;
        }

        // Draw the animated center circle
        _gui.DrawCircle(center, 60, _textColor);
    }

    static Color HsvToColor(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        var m = value - c;

        double r, g, b;

        if (hue < 60)
        {
            r = c;
            g = x;
            b = 0;
        }
        else if (hue < 120)
        {
            r = x;
            g = c;
            b = 0;
        }
        else if (hue < 180)
        {
            r = 0;
            g = c;
            b = x;
        }
        else if (hue < 240)
        {
            r = 0;
            g = x;
            b = c;
        }
        else if (hue < 300)
        {
            r = x;
            g = 0;
            b = c;
        }
        else
        {
            r = c;
            g = 0;
            b = x;
        }

        return Color.FromArgb(255,
            (int)((r + m) * 255),
            (int)((g + m) * 255),
            (int)((b + m) * 255));
    }
}

public static class ColorExtensions
{
    public static Color Desaturate(this Color color, float saturationFactor = 1.0f)
    {
        // Clamp saturation factor between 0 (fully desaturated) and 1 (original color)
        saturationFactor = Math.Clamp(saturationFactor, 0f, 1f);

        // Calculate luminance
        var luminance = (int)(color.R * 0.299 + color.G * 0.587 + color.B * 0.114);

        // Interpolate between original color and grayscale based on the saturation factor
        var r = (int)(color.R * saturationFactor + luminance * (1 - saturationFactor));
        var g = (int)(color.G * saturationFactor + luminance * (1 - saturationFactor));
        var b = (int)(color.B * saturationFactor + luminance * (1 - saturationFactor));

        return Color.FromArgb(color.A, r, g, b);
    }
}
