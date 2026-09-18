using System.Numerics;
using Guinevere;

namespace Example_74_PanGui_Heart;

public abstract class Program
{
    private static Shape _shape = null!;
    private static string _text = null!;
    private static Shape _b = null!;
    private static Shape _a = null!;
    private static Shape _tri = null!;
    private static Shape _heart = null!;
    private static Gui _gui = null!;
    private static readonly float Speed = .1f * .99f;

    public static void Main()
    {
        _gui = new Gui();
        var win = new GuiWindow(_gui);
        List<Action> slides =
        [
            Slide01,
            Slide02,
            Slide03,
            Slide04,
            Slide05,
            Slide06,
            Slide07,
            Slide08,
            Slide09,
            Slide10,
            Slide11,
            Slide12,
            Slide13,
            Slide14,
            Slide15,
            Slide16,
            Slide16
        ];
        Color colorShape;
        Color innerShadow;
        Color innerShadow2;
        win.RunGui(() =>
        {
            _gui.SetTextColor(Color.White);
            using (_gui.Node().Expand().AlignContent(0.5f).Enter())
            {
                var slideIndex = (int)(_gui.Time.Elapsed / Speed / slides.Count);
                slideIndex %= slides.Count;
                slides[slideIndex]();

                var aPos = new Vector2(-60, 0) + _gui.CurrentNode.Rect.Center;
                _gui.DrawCircleBorder(aPos, 100, 0x333333ff);
                var bPos = new Vector2(+60, 0) + _gui.CurrentNode.Rect.Center;
                _gui.DrawCircleBorder(bPos, 100, 0x333333ff);

                var interactable = _gui.Node(250, 250).GetInteractable();
                if (interactable.OnHover())
                    _shape = _shape.Scale(1.2f);

                if (slideIndex >= 14)
                {
                    colorShape = Color.Red;
                    innerShadow = Color.DeepPink;
                    innerShadow2 = Color.DarkRed;
                    innerShadow = Color.FromArgb(128, Color.DeepPink);
                    innerShadow2 = Color.FromArgb(128, Color.DarkRed);
                }
                else
                {
                    colorShape = Color.Blue;
                    innerShadow = Color.FromArgb(128, Color.LightBlue);
                    innerShadow2 = Color.FromArgb(128, Color.DarkBlue);
                }

                _shape.SolidColor(colorShape)
                    .InnerShadow(innerShadow, Vector2.One * 10, 5, 10)
                    .InnerShadow(innerShadow2, Vector2.One * -10, 5, 10)
                    ;

                _gui.SetClipArea(_gui.CurrentNode, _shape);

                _gui.DrawShape(_gui.CurrentNode.Rect.Center, _shape);
                _gui.Node(0, 75); // spacer
                _gui.DrawText(_text, 20);
            }
        });
    }

    private static void Slide01()
    {
        _a = Shape.Circle(100);
        _shape = _a.Copy();
        _text = "a = Shape.Circle(100)";
    }

    private static void Slide02()
    {
        _a = Shape.Circle(100).MoveX(-60);
        _shape = _a.Copy();
        _text = "a = Shape.Circle(100).MoveX(-60)";
    }

    private static void Slide03()
    {
        _b = Shape.Circle(100).MoveX(+60);
        _shape = _b.Copy();
        _text = "b = Shape.Circle(100).MoveX(+60)";
    }

    private static void Slide04()
    {
        var shape = _a + _b;
        _shape = shape.Copy();
        _text = "a + b";
    }

    private static void Slide05()
    {
        var shape = _a * _b;
        _shape = shape.Copy();
        _text = "a * b";
    }

    private static void Slide06()
    {
        var shape = (_a + _b).Onion(10);
        _shape = shape.Copy();
        _text = "(a + b).Onion(10)";
    }

    private static void Slide07()
    {
        var shape = (_a + _b) - (_a * _b);
        _shape = shape.Copy();
        _text = "(a + b) - (a * b)";
    }

    private static void Slide08()
    {
        var shape = _a.MoveX(-85).Union(_b.MoveX(85), 100);
        _shape = shape.Copy();
        _text = "a.MoveX(-85).Union(b.MoveX(85), 100)";
    }

    private static void Slide09()
    {
        var shape = _a.Union(_b, 100);
        _shape = shape.Copy();
        _text = "a.Union(b, 100)";
    }

    private static void Slide10()
    {
        var shape = _a.Union(_b, 100) - (_a - 20);
        _shape = shape.Copy();
        _text = "a.Union(b, 100) - (a - 20)";
    }

    private static void Slide11()
    {
        var shape = _a.Union(_b, 100) - (_b - 20);
        _shape = shape.Copy();
        _text = "a.Union(b, 100) - (9 - 20)";
    }

    private static void Slide12()
    {
        _tri = Shape.Triangle(-100, 0, 100, 0, 0, 200);
        _shape = _tri.Copy();
        _text = "tri = Shape.Triangle(-100, 0, 100, 0, 0, 200)";
    }

    private static void Slide13()
    {
        var shape = _tri + _a + _b;
        _shape = shape.Copy();
        _text = "tri + a + b";
    }

    private static void Slide14()
    {
        _heart = _tri.Union(_a + _b, 100);
        _shape = _heart.Copy();
        _text = "_tri.Union(_a + _b, 100)";
    }

    private static void Slide15()
    {
        var t = _gui.Time.Elapsed;
        var pi = MathF.PI;
        var sin = MathF.Sin;
        var shape = _heart.Scale(1 + sin(t + pi) * 0.2f);
        _shape = shape.Copy();
        _text = "heart.Scale(1 + Sin(t + PI) * 0.2f)";
    }

    private static void Slide16()
    {
        var shape = _heart.Mix(_b.Rotate(_gui.Time.Elapsed * 4), 0.1f);
        _shape = shape.Copy();
        _text = "heart.Mix(b.Rotate(gui.Time.Elapsed * 4), 0.1f)";
    }
}
