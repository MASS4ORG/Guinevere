namespace Guinevere;

/// <summary>Builds immediate-mode layouts and queues their drawing commands.</summary>
public partial class Gui
{
    /// <summary>Queues a rectangle border using paint values captured by the draw command.</summary>
    public void DrawRectBorder(Rect screenRect, Color color, float thickness = 1f,
        float radius = 0f, Corner corners = Corner.All) =>
        AddPrimitive(new PrimitiveCommand(PrimitiveKind.Rect, screenRect, color, radius, corners, thickness, true));

    /// <summary>Queues a rectangle border at the given position and size.</summary>
    public void DrawRectBorder(Vector2 topLeft, Vector2 size, Color color, float thickness = 1f,
        float radius = 0f, Corner corners = Corner.All) =>
        DrawRectBorder(new Rect(topLeft.X, topLeft.Y, size.X, size.Y), color, thickness, radius, corners);

    /// <summary>Queues a filled rectangle; a null color uses black.</summary>
    public void DrawRectFilled(Rect screenRect, Color? color, float radius = 0f, Corner corners = Corner.All) =>
        AddPrimitive(new PrimitiveCommand(PrimitiveKind.Rect, screenRect, color ?? Color.Black, radius, corners));

    /// <summary>Queues a filled rectangle defined by its edges; a null color uses black.</summary>
    public void DrawRectFilled(float left, float top, float right, float bottom, Color? color,
        float radius = 0f, Corner corners = Corner.All) =>
        DrawRectFilled(new Rect(left, top, right - left, bottom - top), color, radius, corners);

    /// <summary>Queues a filled rectangle using paint values captured by the draw command.</summary>
    public void DrawRect(Rect rect, Color color, float radius = 0f, Corner corners = Corner.All) =>
        DrawRectFilled(rect, color, radius, corners);

    /// <summary>Queues a filled rectangle at the given position and size.</summary>
    public void DrawRect(Vector2 position, Vector2 size, Color color, float radius = 0f,
        Corner corners = Corner.All) =>
        DrawRectFilled(new Rect(position.X, position.Y, size.X, size.Y), color, radius, corners);

    /// <summary>Queues a filled rectangle; a null color uses black.</summary>
    public void DrawRect(Rect rect, Color? color, float radius = 0f, Corner corners = Corner.All) =>
        DrawRectFilled(rect, color, radius, corners);

    /// <summary>Queues a circle border using paint values captured by the draw command.</summary>
    public void DrawCircleBorder(Vector2 center, float radius, Color color, float thickness = 1f) =>
        AddPrimitive(new PrimitiveCommand(PrimitiveKind.Circle, default, color, radius,
            Thickness: thickness, Stroke: true, A: center));

    /// <summary>Queues a filled circle using paint values captured by the draw command.</summary>
    public void DrawCircleFilled(Vector2 center, float radius, Color color) =>
        AddPrimitive(new PrimitiveCommand(PrimitiveKind.Circle, default, color, radius, A: center));

    /// <summary>Queues a filled circle using paint values captured by the draw command.</summary>
    public void DrawCircle(Vector2 center, float radius, Color color) => DrawCircleFilled(center, radius, color);

    /// <summary>Queues a triangle with interpolated vertex colors; omitted colors use the first color.</summary>
    public void DrawTriangleFilled(Vector2 a, Vector2 b, Vector2 c, Color colorA,
        Color? colorB = null, Color? colorC = null) =>
        AddPrimitive(new PrimitiveCommand(PrimitiveKind.Triangle, default, colorA, A: a, B: b, C: c,
            ColorB: colorB ?? colorA, ColorC: colorC ?? colorA));

    /// <summary>Queues a triangle with interpolated vertex colors; omitted colors use the first color.</summary>
    public void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color colorA,
        Color? colorB = null, Color? colorC = null) => DrawTriangleFilled(a, b, c, colorA, colorB, colorC);

    /// <summary>Queues one filled rectangle behind the current node's other commands; null uses white.</summary>
    public void DrawBackgroundRect(Color? color, float radius = 0f, Corner corners = Corner.All) =>
        AddPrimitive(new PrimitiveCommand(PrimitiveKind.Rect, CurrentNode.Rect, color ?? Color.White, radius, corners),
            prepend: true);

    /// <summary>Queues a flat-ended line in the current node's draw order, with a minimum width of one pixel.</summary>
    public void DrawLine(Vector2 start, Vector2 end, Color color, float thickness = 1f) =>
        AddPrimitive(new PrimitiveCommand(PrimitiveKind.Line, default, color, Thickness: thickness, A: start, B: end));

    /// <summary>Queues a mutable rectangle for fluent fills, strokes, shaders and shadows.</summary>
    public Shape DrawRect(Rect rect, float radius = 0f, Corner corners = Corner.All)
    {
        var shape = CreateRect(rect, radius, corners);
        AddDraw(shape);
        return shape;
    }

    /// <summary>Queues a mutable rectangle at the given position and size.</summary>
    public Shape DrawRect(Vector2 position, Vector2 size, float radius = 0f, Corner corners = Corner.All) =>
        DrawRect(new Rect(position.X, position.Y, size.X, size.Y), radius, corners);

    /// <summary>Queues a mutable circle for fluent fills, strokes, shaders and shadows.</summary>
    public Shape DrawCircle(Vector2 center, float radius)
    {
        var shape = Shape.Circle(radius, center);
        AddDraw(shape);
        return shape;
    }

    /// <summary>Queues a mutable triangle for fluent fills, strokes, shaders and shadows.</summary>
    public Shape DrawTriangle(Vector2 a, Vector2 b, Vector2 c)
    {
        var shape = Shape.Triangle(a, b, c);
        AddDraw(shape);
        return shape;
    }

    /// <summary>Queues a mutable white background behind the current node's other commands.</summary>
    public Shape DrawBackgroundRect(float radius = 0f, Corner corners = Corner.All)
    {
        var shape = CreateRect(CurrentNode.Rect, radius, corners).SolidColor(Color.White);
        AddDraw(shape, prepend: true);
        return shape;
    }

    static Shape CreateRect(Rect rect, float radius, Corner corners) =>
        ImMath.ApproximatelyEquals(radius, 0f)
            ? Shape.Rect(rect.X, rect.Y, rect.X + rect.W, rect.Y + rect.H)
            : Shape.RoundRect(rect.X, rect.Y, rect.X + rect.W, rect.Y + rect.H, radius, corners);

    /// <summary>Queues a translated, independently mutable copy of the supplied shape and its layers.</summary>
    public Shape DrawShape(Vector2 position, Shape shape)
    {
        var copy = shape.Copy();
        copy.Node = CurrentNode;
        foreach (var (_, layers) in copy.Layers)
            foreach (var (path, _) in layers)
                path.Transform(SKMatrix.CreateTranslation(position.X, position.Y));
        AddDraw(copy);
        return copy;
    }

    void AddPrimitive(in PrimitiveCommand command, bool prepend = false)
    {
        if (Pass != Pass.Pass2Render) return;
        CurrentNode.DrawList.Add(command, prepend);
    }

    /// <summary>
    /// Sets a clipping area for rendering content inside a specific layout node.
    /// </summary>
    /// <param name="node">The layout node to which the clipping area is applied.</param>
    /// <param name="clipShape">The shape defining the clipping area.</param>
    public void SetClipArea(LayoutNode node, Shape clipShape)
    {
        if (Pass != Pass.Pass2Render) return;

        // Queue the clip operation in the node's draw list
        node.DrawList.AddClip(clipShape, node.Rect.Center);
    }

    void AddDraw(IDrawable shape, bool prepend = false, LayoutNode? node = null)
    {
        if (Pass != Pass.Pass2Render) return;
        node ??= CurrentNode;
        if (prepend)
            node.DrawList.Prepend(shape);
        else
            node.DrawList.Add(shape);
    }

    void AddDraw(IDrawListEntry entry, LayoutNode? node = null, bool prepend = false)
    {
        if (Pass != Pass.Pass2Render) return;
        node ??= CurrentNode;
        if (prepend)
            node.DrawList.Prepend(entry);
        else
            node.DrawList.Add(entry);
    }
}
