using Guinevere;

namespace Example_01;

public partial class Shared
{
    sealed record WorkItem(string Id, string Title, bool NeedsReview);

    static readonly string[] BoardColumns = ["Backlog", "Doing", "Done"];

    static readonly WorkItem[] workItems =
    [
        new("layout", "Design the layout", false),
        new("tests", "Write tests", false),
        new("review", "Security review", true),
        new("docs", "Update the docs", false),
        new("release", "Cut the release", true),
    ];

    static readonly Dictionary<string, int> placement = workItems.ToDictionary(item => item.Id, _ => 0);
    static readonly DragDropTag boardTag = new("demo/board");
    static string? boardMessage;

    /// <summary>A three-column board. Cards are typed payloads; "Done" refuses cards that still need review.</summary>
    static void DragAndDrop(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(12).Enter())
        {
            Label(gui, p, boardMessage ?? "Drag a card to another column, or focus it and press Space/Enter. Escape cancels.");
            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(14).Enter())
                for (var column = 0; column < BoardColumns.Length; column++)
                {
                    using var key = gui.ItemKey(column);
                    BoardColumn(gui, p, column);
                }
        }

        gui.DragGhost();
    }

    static void BoardColumn(Gui gui, ColorPalette p, int column)
    {
        var id = $"column/{column}";
        var count = workItems.Count(item => placement[item.Id] == column);
        using (gui.Node(-1, -1, id).Expand().Direction(Axis.Vertical).Padding(12).Gap(8).Enter())
        {
            gui.DrawBackgroundRect(p.SurfaceRaised, 12);
            gui.DrawText($"{BoardColumns[column]}  ({count})", 14, p.Text);
            foreach (var item in workItems.Where(item => placement[item.Id] == column))
                WorkCard(gui, p, item);

            var result = gui.DropTarget<WorkItem>(id, boardTag,
                item => placement[item.Id] != column && !(column == BoardColumns.Length - 1 && item.NeedsReview),
                item => Later(() =>
                {
                    placement[item.Id] = column;
                    boardMessage = $"{item.Title} → {BoardColumns[column]}";
                }));
            gui.DrawDropIndicator(result.State, style: new DropIndicatorStyle(p.Positive, p.Negative));
            if (result.Payload is { } hovering && result.State == DropTargetState.HoverRejected)
                gui.DrawText(hovering.NeedsReview ? "Needs review first" : "Already here", 12, p.Negative);
        }
    }

    static void WorkCard(Gui gui, ColorPalette p, WorkItem item)
    {
        using (gui.Node(-1, 54, $"card/{item.Id}").ExpandWidth().Padding(12, 0).ContentAlignY(0.5f).Gap(2).Enter())
        {
            gui.DrawBackgroundRect(p.Surface, 8);
            gui.DrawRectBorder(gui.CurrentNode.Rect, p.Border, 1f);
            gui.DrawText(item.Title, 14, p.Text);
            if (item.NeedsReview) gui.DrawText("needs review", 11, p.Warning);

            gui.DragSource($"card/{item.Id}", item, tag: boardTag, ghost: ghost =>
            {
                using (ghost.Node(190, 38).Padding(10, 0).ContentAlignY(0.5f).Enter())
                {
                    ghost.DrawBackgroundRect(p.Accent, 8);
                    ghost.DrawText(item.Title, 13, p.AccentText);
                }
            });
        }
    }
}
