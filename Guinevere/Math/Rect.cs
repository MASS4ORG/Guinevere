namespace Guinevere;

/// <summary>
/// Represents a rectangle defined by its position (X, Y) and size (W, H).
/// Provides properties and methods for working with rectangles, such as
/// calculating various points, centers, and containment checks.
/// </summary>
public record struct Rect
{
    /// <summary>
    /// Represents a rectangle defined by its position (X, Y) and size (W, H).
    /// Provides properties and methods for working with rectangles, such as
    /// calculating various points, centers, and containment checks.
    /// </summary>
    /// <param name="X">The x-coordinate of the rectangle's top-left corner.</param>
    /// <param name="Y">The y-coordinate of the rectangle's top-left corner.</param>
    /// <param name="W">The width of the rectangle.</param>
    /// <param name="H">The height of the rectangle.</param>
    public Rect(float X = 0, float Y = 0, float W = 0, float H = 0)
    {
        this.X = X;
        this.Y = Y;
        this.W = W;
        this.H = H;
    }

    /// <summary>
    /// Gets the position of the rectangle, represented as the coordinates of its top-left corner (X, Y).
    /// </summary>
    public Vector2 Position => new(X, Y);

    /// <summary>
    /// Gets the size of the rectangle, represented as the width and height (W, H).
    /// </summary>
    public Vector2 Size => new(W, H);

    /// <summary>
    /// Gets the local center of the rectangle relative to its width and height.
    /// The local center is the midpoint defined by half the width and half the height of the rectangle.
    /// </summary>
    public Vector2 LocalCenter => new(W / 2, H / 2);

    /// <summary>
    /// Gets the absolute center of the rectangle by adding its local center to its position.
    /// The center is calculated as the midpoint of width (W) and height (H) relative to the rectangle's position (X, Y).
    /// </summary>
    public Vector2 Center => LocalCenter + Position;

    /// <summary>
    /// Gets the position of the rectangle's bottom-right corner.
    /// Defined as the point where the rectangle's X and Width
    /// values meet its Y and Height values.
    /// </summary>
    public Vector2 BottomRight => new(X + W, Y + H);

    /// <summary>
    /// Represents a rectangle with zero-position and size.
    /// A predefined rectangle instance with X, Y, W, and H set to 0.
    /// </summary>
    public static Rect Zero => new();

    /// <summary>
    /// Gets a rectangle instance where all the position and size values (X, Y, W, H) are initialized to 1.
    /// </summary>
    public static Rect One => new(1, 1, 1, 1);

    /// <summary>
    /// Gets the height of the rectangle, represented as the vertical dimension (H).
    /// </summary>
    public float Height
    {
        get => H;
        set => H = value;
    }

    /// <summary>
    /// Gets the width of the rectangle.
    /// </summary>
    public float Width
    {
        get => W;
        set => W = value;
    }

    /// <summary>The x-coordinate of the rectangle's top-left corner.</summary>
    public float X { get; set; }

    /// <summary>The y-coordinate of the rectangle's top-left corner.</summary>
    public float Y { get; set; }

    /// <summary>The width of the rectangle.</summary>
    public float W { get; private set; }

    /// <summary>The height of the rectangle.</summary>
    public float H { get; private set; }

    /// <summary>
    /// Checks whether the rectangle contains the specified point.
    /// </summary>
    /// <param name="point">The point to check.</param>
    /// <returns>True if the point is within the rectangle's bounds; otherwise false.</returns>
    [PublicAPI]
    public bool Contains(Vector2 point)
    {
        return point.X >= X &&
               point.X <= X + W &&
               point.Y >= Y &&
               point.Y <= Y + H;
    }

    /// <summary>
    /// Defines an implicit conversion from an instance of Rect to an instance of Skia's SKRect.
    /// Converts the Rect structure to an SKRect using the X, Y, Width, and Height properties.
    /// </summary>
    /// <param name="rect">The Rect instance to convert.</param>
    /// <returns>An SKRect instance created from the specified Rect.</returns>
    public static implicit operator SKRect(Rect rect) =>
        new(rect.X, rect.Y, rect.X + rect.W, rect.Y + rect.H);

    /// <summary>
    /// Defines an implicit conversion from an instance of Skia's SKRect to an instance of Rect.
    /// Converts the given SKRect to the Rect structure using the Left, Top, Width,
    /// and Height properties of the SKRect.
    /// </summary>
    /// <param name="skRect">The SKRect instance to convert.</param>
    /// <returns>A Rect instance constructed from the SKRect.</returns>
    public static implicit operator Rect(SKRect skRect) =>
        new(skRect.Left, skRect.Top, skRect.Width, skRect.Height);

    /// <summary>
    /// Returns a string representation of the rectangle, including its position and size, formatted to one decimal place.
    /// </summary>
    /// <returns>A string in the format "Rect(x:{X}, y:{Y}, w:{W}, h:{H})", where X, Y, W, and H are the rectangle's properties.</returns>
    public override string ToString()
    {
        return $"Rect(x:{X:F1}, y:{Y:F1}, w:{W:F1}, h:{H:F1})";
    }

    /// <summary>
    /// Converts a Rect instance explicitly to its string representation using the ToString method.
    /// </summary>
    /// <param name="rect">The Rect instance to convert to a string.</param>
    /// <returns>A string representation of the specified Rect instance.</returns>
    public static explicit operator string(Rect rect) => rect.ToString();


    /// <summary>
    /// Adds a scalar value to all components (X, Y, W, H) of the rectangle.
    /// </summary>
    /// <param name="rect">The rectangle to add to.</param>
    /// <param name="value">The value to add to each component.</param>
    /// <returns>A new rectangle with each component increased by the value.</returns>
    public static Rect operator +(Rect rect, float value) =>
        new(rect.X + value, rect.Y + value, rect.W + value, rect.H + value);

    /// <summary>
    /// Subtracts a scalar value from all components (X, Y, W, H) of the rectangle.
    /// </summary>
    /// <param name="rect">The rectangle to subtract from.</param>
    /// <param name="value">The value to subtract from each component.</param>
    /// <returns>A new rectangle with each component decreased by the value.</returns>
    public static Rect operator -(Rect rect, float value) =>
        new(rect.X - value, rect.Y - value, rect.W - value, rect.H - value);

    /// <summary>
    /// Multiplies all components (X, Y, W, H) of the rectangle by a scalar value.
    /// </summary>
    /// <param name="rect">The rectangle to multiply.</param>
    /// <param name="value">The value to multiply each component by.</param>
    /// <returns>A new rectangle with each component multiplied by the value.</returns>
    public static Rect operator *(Rect rect, float value) =>
        new(rect.X * value, rect.Y * value, rect.W * value, rect.H * value);

    /// <summary>
    /// Divides all components (X, Y, W, H) of the rectangle by a scalar value.
    /// </summary>
    /// <param name="rect">The rectangle to divide.</param>
    /// <param name="value">The value to divide each component by.</param>
    /// <returns>A new rectangle with each component divided by the value.</returns>
    /// <exception cref="DivideByZeroException">Thrown when value is zero.</exception>
    public static Rect operator /(Rect rect, float value)
    {
        if (value == 0)
            throw new DivideByZeroException("Cannot divide rectangle components by zero.");

        return new Rect(rect.X / value, rect.Y / value, rect.W / value, rect.H / value);
    }

    /// <summary>
    /// Adds a scalar value to all components (X, Y, W, H) of the rectangle.
    /// </summary>
    /// <param name="value">The value to add to each component.</param>
    /// <param name="rect">The rectangle to add to.</param>
    /// <returns>A new rectangle with each component increased by the value.</returns>
    public static Rect operator +(float value, Rect rect) => rect + value;

    /// <summary>
    /// Multiplies all components (X, Y, W, H) of the rectangle by a scalar value.
    /// </summary>
    /// <param name="value">The value to multiply each component by.</param>
    /// <param name="rect">The rectangle to multiply.</param>
    /// <returns>A new rectangle with each component multiplied by the value.</returns>
    public static Rect operator *(float value, Rect rect) => rect * value;

    /// <summary>
    /// Deconstructs the rectangle into its components: position (X, Y) and size (W, H).
    /// </summary>
    /// <param name="x">The x-coordinate of the rectangle's top-left corner.</param>
    /// <param name="y">The y-coordinate of the rectangle's top-left corner.</param>
    /// <param name="w">The width of the rectangle.</param>
    /// <param name="h">The height of the rectangle.</param>
    public void Deconstruct(out float x, out float y, out float w, out float h)
    {
        x = X;
        y = Y;
        w = W;
        h = H;
    }
}
