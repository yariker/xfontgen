using System;
using Avalonia;
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

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime application)
        {
            throw new PlatformNotSupportedException();
        }

        var window = new MainWindow();
        application.MainWindow = window;

        var mainView = window.MainView;
        mainView.DataContext = new MainViewModel(application, window.StorageProvider, mainView, mainView);

        base.OnFrameworkInitializationCompleted();
    }
}
