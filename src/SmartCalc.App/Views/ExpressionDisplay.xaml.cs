using System.Windows;
using System.Windows.Controls;

namespace SmartCalc.App.Views;

public partial class ExpressionDisplay : UserControl
{
    public static readonly DependencyProperty MathNotationProperty =
        DependencyProperty.Register(nameof(MathNotation), typeof(bool), typeof(ExpressionDisplay), new PropertyMetadata(true));

    /// <summary>false ở chế độ Lập trình: dấu ^ là XOR, giữ cách hiển thị text thô.</summary>
    public bool MathNotation
    {
        get => (bool)GetValue(MathNotationProperty);
        set => SetValue(MathNotationProperty, value);
    }

    public static readonly DependencyProperty ResultHeightProperty =
        DependencyProperty.Register(nameof(ResultHeight), typeof(double), typeof(ExpressionDisplay), new PropertyMetadata(64.0));

    /// <summary>Chiều cao tối đa của dòng kết quả (chữ co lại nếu dài).</summary>
    public double ResultHeight
    {
        get => (double)GetValue(ResultHeightProperty);
        set => SetValue(ResultHeightProperty, value);
    }

    public ExpressionDisplay() => InitializeComponent();
}
