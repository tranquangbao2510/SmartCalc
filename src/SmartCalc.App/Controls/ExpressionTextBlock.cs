using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SmartCalc.Core.Input;

namespace SmartCalc.App.Controls;

/// <summary>Dựng Inline từ bố cục Core; không thay đổi text nguồn dùng để tính/lưu/sao chép.</summary>
public sealed class ExpressionTextBlock : TextBlock
{
    public static readonly DependencyProperty ExpressionSourceProperty = DependencyProperty.Register(
        nameof(ExpressionSource), typeof(string), typeof(ExpressionTextBlock), new PropertyMetadata("", RebuildOnChange));
    public static readonly DependencyProperty ErrorPositionProperty = DependencyProperty.Register(
        nameof(ErrorPosition), typeof(int), typeof(ExpressionTextBlock), new PropertyMetadata(-1, RebuildOnChange));
    public static readonly DependencyProperty MathNotationProperty = DependencyProperty.Register(
        nameof(MathNotation), typeof(bool), typeof(ExpressionTextBlock), new PropertyMetadata(true, RebuildOnChange));
    public static readonly DependencyProperty ShowEqualsProperty = DependencyProperty.Register(
        nameof(ShowEquals), typeof(bool), typeof(ExpressionTextBlock), new PropertyMetadata(false, RebuildOnChange));

    public string ExpressionSource
    {
        get => (string)GetValue(ExpressionSourceProperty);
        set => SetValue(ExpressionSourceProperty, value);
    }
    public int ErrorPosition
    {
        get => (int)GetValue(ErrorPositionProperty);
        set => SetValue(ErrorPositionProperty, value);
    }
    public bool MathNotation
    {
        get => (bool)GetValue(MathNotationProperty);
        set => SetValue(MathNotationProperty, value);
    }
    public bool ShowEquals
    {
        get => (bool)GetValue(ShowEqualsProperty);
        set => SetValue(ShowEqualsProperty, value);
    }

    private static void RebuildOnChange(DependencyObject owner, DependencyPropertyChangedEventArgs _) =>
        ((ExpressionTextBlock)owner).Rebuild();

    static ExpressionTextBlock()
    {
        foreach (var property in new[] { FontSizeProperty, FontFamilyProperty, FontWeightProperty, FontStyleProperty, ForegroundProperty })
            property.OverrideMetadata(typeof(ExpressionTextBlock),
                new FrameworkPropertyMetadata(property.GetMetadata(typeof(TextBlock)).DefaultValue, RebuildOnChange));
    }

    private void Rebuild()
    {
        var elements = ExpressionLayout.Create(ExpressionSource ?? "", MathNotation, ErrorPosition).Elements;
        Inlines.Clear();
        int index = 0;
        AppendLevel(this, elements, ref index, 0);
        if (ShowEquals) Inlines.Add(new Run(" ="));
    }

    private void AppendLevel(TextBlock block, IReadOnlyList<ExpressionElement> elements, ref int index, int level)
    {
        while (index < elements.Count && elements[index].SuperscriptLevel >= level)
        {
            var element = elements[index];
            if (element.SuperscriptLevel > level)
            {
                var exponent = new TextBlock
                {
                    FontFamily = FontFamily,
                    FontWeight = FontWeight,
                    FontStyle = FontStyle,
                    FontSize = FontSize * ExpressionElement.ScaleAt(level + 1),
                    Foreground = Foreground,
                };
                // TextBlock lồng nhau tạo baseline nâng thực ở từng tầng; cỡ chữ tối thiểu 55%.
                AppendLevel(exponent, elements, ref index, level + 1);
                block.Inlines.Add(new InlineUIContainer(exponent) { BaselineAlignment = BaselineAlignment.Superscript });
                continue;
            }

            if (element.Kind == ExpressionElementKind.Placeholder)
            {
                var placeholder = new Border
                {
                    Width = Math.Max(7, block.FontSize * 0.45), Height = block.FontSize * 0.8,
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(1),
                    BorderBrush = Foreground, Opacity = element.IsError ? 1 : 0.3,
                    Margin = new Thickness(1, 0, 1, 0),
                };
                if (element.IsError)
                {
                    placeholder.SetResourceReference(Border.BorderBrushProperty, "ErrorBrush");
                    placeholder.SetResourceReference(Border.BackgroundProperty, "ErrorSoftBg");
                }
                block.Inlines.Add(new InlineUIContainer(placeholder));
            }
            else
            {
                var run = new Run(element.Text);
                if (element.IsError)
                {
                    run.SetResourceReference(TextElement.ForegroundProperty, "ErrorBrush");
                    run.SetResourceReference(TextElement.BackgroundProperty, "ErrorSoftBg");
                    run.TextDecorations = System.Windows.TextDecorations.Underline;
                }
                block.Inlines.Add(run);
            }
            index++;
        }
    }
}
