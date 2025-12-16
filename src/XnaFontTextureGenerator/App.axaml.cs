using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XnaFontTextureGenerator.ViewModels;
using XnaFontTextureGenerator.Views;

namespace XnaFontTextureGenerator;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime application)
        {
            var window = new MainWindow();
            application.MainWindow = window;

            var mainView = window.MainView;
            var mainViewModel = new MainViewModel(application, window.StorageProvider, mainView, mainView);
            mainView.DataContext = mainViewModel;

            ParseArgs(application, window, mainViewModel);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void ParseArgs(IClassicDesktopStyleApplicationLifetime application, MainWindow window, MainViewModel mainViewModel)
    {
        string[]? args = application.Args;
        if (args != null && args.Length > 0)
        {
            Task t = Task.Run(async () =>
            {
                var sp = window.StorageProvider;
                List<string> pngs = new();
                foreach (var arg in args)
                {
                    string a = arg;
                    a = a.Trim(' ', '"');
                    if (a.EndsWith(".png"))
                    {
                        pngs.Add(a);
                    }
                    else
                    {
                        pngs.AddRange(Directory.GetFiles(a, "*.png", SearchOption.AllDirectories));
                    }
                }
                foreach (var png in pngs)
                {
                    if (File.Exists(png))
                    {
                        var file = await sp.TryGetFileFromPathAsync(png);
                        await mainViewModel.ImportAsync(file);
                        await mainViewModel.ExportAsync(file);
                    }
                }

                application.Shutdown();
            });
        }
    }
}
