using System.Reflection;
using Avalonia.Controls;

namespace XnaFontTextureGenerator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title += $" {Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)}";
    }
}
