using System.Windows;
using System.Windows.Controls;

namespace SmartCalc.App.Views;

public partial class ExpressionDisplay : UserControl
{
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
