using System.Windows;
using SmartCalc.App.ViewModels;
using SmartCalc.Core.History;
using SmartCalc.Core.Settings;

namespace SmartCalc.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettings.Load(AppSettings.DefaultPath);
        ApplyTheme(settings.Theme);

        var vm = new MainViewModel(settings, AppSettings.DefaultPath, new HistoryStore(HistoryStore.DefaultPath), ApplyTheme);
        var window = new MainWindow(vm)
        {
            Width = settings.WindowWidth,
            Height = settings.WindowHeight,
        };
        MainWindow = window;
        window.Show();
    }

    /// <summary>Thay bảng màu (MergedDictionaries[0]); mọi chỗ dùng DynamicResource tự cập nhật.</summary>
    public void ApplyTheme(AppTheme theme)
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{theme}.xaml", UriKind.Absolute),
        };
        Resources.MergedDictionaries[0] = dict;
        foreach (Window w in Windows) WindowInterop.SetDarkTitleBar(w, theme == AppTheme.Dark);
    }
}
