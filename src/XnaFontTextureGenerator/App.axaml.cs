using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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

        TopLevel? topLevel;
        MainView? mainView;

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;
                mainView = mainWindow.MainView;
                topLevel = mainWindow;
                break;

            case ISingleViewApplicationLifetime mobile:
                mobile.MainView = mainView = new MainView();
                topLevel = TopLevel.GetTopLevel(mainView);
                break;

            default:
                return;
        }

        if (mainView != null && topLevel != null)
        {
            mainView.DataContext = new MainViewModel(topLevel.StorageProvider, mainView, mainView);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
