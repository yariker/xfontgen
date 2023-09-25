using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TagLib;
using TagLib.Png;
using XnaFontTextureGenerator.Model;
using XnaFontTextureGenerator.Services;

namespace XnaFontTextureGenerator.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SynchronizationContext _synchronizationContext = SynchronizationContext.Current!;
    private readonly Subject<TextureMetadata> _renderQueue = new();

    private readonly IClassicDesktopStyleApplicationLifetime _application;
    private readonly IStorageProvider _storageProvider;
    private readonly IFontTextureRenderer _renderer;
    private readonly IMessageBox _messageBox;
    private readonly IDisposable _renderWorker;

    private CancellationTokenSource? _cancellationTokenSource;
    private int _hoverIndex = -1;
    private string[]? _chars;
    private Glyph[]? _glyphs;

    [ObservableProperty]
    private bool _lightMode;

    [ObservableProperty]
    private bool _rendering;

    [ObservableProperty]
    private string _fontName = null!;

    [ObservableProperty]
    private float _fontSize = TextureMetadata.DefaultFontSize;

    [ObservableProperty]
    private Color _foreground = Color.FromUInt32(TextureMetadata.DefaultForegroundColor);

    [ObservableProperty]
    private bool _antialiased = true;

    [ObservableProperty]
    private CombinedFontStyle _combinedFontStyle;

    [ObservableProperty]
    private string _minChar = "32";

    [ObservableProperty]
    private string _maxChar = "126";

    [ObservableProperty]
    private int _kerning = TextureMetadata.AutomaticKerning;

    [ObservableProperty]
    private int _leading;

    [ObservableProperty]
    private bool _shadowEnabled;

    [ObservableProperty]
    private float _shadowBlur = DropShadowMetadata.DefaultShadowBlur;
    
    [ObservableProperty]
    private float _shadowOffsetX = DropShadowMetadata.DefaultShadowOffsetX;

    [ObservableProperty]
    private float _shadowOffsetY = DropShadowMetadata.DefaultShadowOffsetY;

    [ObservableProperty]
    private Color _shadowColor = Color.FromUInt32(DropShadowMetadata.DefaultShadowColor);

    [ObservableProperty]
    private bool _outlineEnabled;

    [ObservableProperty]
    private float _outlineWidth = StrokeMetadata.DefaultOutlineWidth;

    [ObservableProperty]
    private Color _outlineColor = Color.FromUInt32(StrokeMetadata.DefaultOutlineColor);

    [ObservableProperty]
    private int _textureWidth = 512;

    [ObservableProperty]
    private Bitmap? _texture;

    [ObservableProperty]
    private string? _tooltip;

#if DEBUG
    public MainViewModel()
        : this(null!, null!, null!, null!)
    {
    }
#endif

    public MainViewModel(
        IClassicDesktopStyleApplicationLifetime application,
        IStorageProvider storageProvider,
        IFontTextureRenderer renderer,
        IMessageBox messageBox)
    {
        if (!Design.IsDesignMode)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(storageProvider);
            ArgumentNullException.ThrowIfNull(renderer);
            ArgumentNullException.ThrowIfNull(messageBox);
        }

        _combinedFontStyle = CombinedFontStyle.Styles[0];

        _application = application;
        _storageProvider = storageProvider;
        _renderer = renderer;
        _messageBox = messageBox;

        _renderWorker = _renderQueue.Throttle(TimeSpan.FromSeconds(0.5))
                                    .ObserveOn(_synchronizationContext)
                                    .Select(x => Observable.FromAsync(() => RenderAsync(x)))
                                    .Concat()
                                    .Subscribe();

        FontNames = FontManager.Current.SystemFonts.Select(x => x.Name).ToArray();
        FontName = FontManager.Current.DefaultFontFamily.Name;
        FontStyles = CombinedFontStyle.Styles;
    }

    public IReadOnlyList<string> FontNames { get; }

    public IReadOnlyList<CombinedFontStyle> FontStyles { get; }

    [RelayCommand]
    protected async Task LoadAsync()
    {
        if (!Design.IsDesignMode && _application.Args is { Length: 1 } args)
        {
            var storageFile = await _storageProvider.TryGetFileFromPathAsync(args[0]);
            await ImportAsync(storageFile);
        }
    }

    [RelayCommand]
    protected async Task ExportAsync(IStorageFile? storageFile = null)
    {
        storageFile ??= await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Font Texture",
            FileTypeChoices = new[] { FilePickerFileTypes.ImagePng },
            DefaultExtension = ".png",
            ShowOverwritePrompt = true,
        });

        if (storageFile == null)
        {
            return;
        }

        try
        {
            var metadata = GetTextureMetadata();

            using (var bitmap = _renderer.Render(GenerateChars(), metadata, out _))
            {
                await using var stream = await storageFile.OpenWriteAsync();
                bitmap.Save(stream);
            }

            // Append PNG metadata.
            using var pngFile = TagLib.File.Create(storageFile.Path.LocalPath);
            var pngTag = (PngTag)pngFile.GetTag(TagTypes.Png, true);
            pngTag.Creator = TextureMetadata.Creator;
            pngTag.SetKeyword(TextureMetadata.Keyword, metadata.ToJson());
            pngFile.Save();
        }
        catch (Exception ex)
        {
            _messageBox.Show(ex.ToString());
        }
    }

    [RelayCommand]
    protected async Task ImportAsync(IStorageFile? storageFile = null)
    {
        storageFile ??= (await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Font Texture",
            FileTypeFilter = new[] { FilePickerFileTypes.ImagePng },
            AllowMultiple = false,
        })).SingleOrDefault();

        if (storageFile == null)
        {
            return;
        }

        try
        {
            // Parse PNG metadata.
            using var pngFile = TagLib.File.Create(storageFile.Path.LocalPath);

            var pngTag = (PngTag)pngFile.GetTag(TagTypes.Png, true);
            var metadata = TextureMetadata.FromJson(pngTag.GetKeyword(TextureMetadata.Keyword));
            if (metadata == null)
            {
                _messageBox.Show($"XNA Font Texture Generator metadata not found in file '{storageFile.Name}'.");
                return;
            }

            FontName = metadata.FontName;
            FontSize = metadata.FontSize;
            Foreground = Color.FromUInt32(metadata.Foreground);
            Kerning = metadata.Kerning;
            Leading = metadata.Leading;
            CombinedFontStyle = FontStyles.FirstOrDefault(x => x.IsMatch(metadata)) ?? FontStyles[0];
            Antialiased = metadata.Antialiased;
            MinChar = metadata.MinChar.ToString();
            MaxChar = metadata.MaxChar.ToString();
            TextureWidth = metadata.TextureWidth;

            ShadowEnabled = metadata.DropShadow != null;
            ShadowBlur = metadata.DropShadow?.Blur ?? DropShadowMetadata.DefaultShadowBlur;
            ShadowOffsetX = metadata.DropShadow?.OffsetX ?? DropShadowMetadata.DefaultShadowOffsetX;
            ShadowOffsetY = metadata.DropShadow?.OffsetY ?? DropShadowMetadata.DefaultShadowOffsetY;
            ShadowColor = Color.FromUInt32(metadata.DropShadow?.Color ?? DropShadowMetadata.DefaultShadowColor);

            OutlineEnabled = metadata.Outline != null;
            OutlineWidth = metadata.Outline?.Width ?? StrokeMetadata.DefaultOutlineWidth;
            OutlineColor = Color.FromUInt32(metadata.Outline?.Color ?? StrokeMetadata.DefaultOutlineColor);
        }
        catch (Exception ex)
        {
            _messageBox.Show(ex.ToString());
        }
    }

    [RelayCommand]
    protected void MouseMove(PixelPoint? pixelPoint)
    {
        if (_glyphs != null && _chars != null && pixelPoint is PixelPoint p)
        {
            var point = new Point(p.X, p.Y);
            var index = FindGlyph(point);

            if (index >= 0)
            {
                if (index != _hoverIndex)
                {
                    var chr = _chars[index];
                    Tooltip = $"'{chr}' 0x{CharHelper.ConvertToCode(chr):X} ({char.GetUnicodeCategory(chr, 0)})";
                    _hoverIndex = index;
                }
                
                return;
            }
        }

        Tooltip = null;
        _hoverIndex = -1;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName != nameof(LightMode) &&
            e.PropertyName != nameof(Texture) &&
            e.PropertyName != nameof(Rendering) &&
            e.PropertyName != nameof(Tooltip))
        {
            _cancellationTokenSource?.Cancel();
            _renderQueue.OnNext(GetTextureMetadata());
        }
    }

    partial void OnMinCharChanging(string value) => CharHelper.ConvertToChar(value);

    partial void OnMaxCharChanging(string value) => CharHelper.ConvertToChar(value);

    private async Task RenderAsync(TextureMetadata metadata)
    {
        Rendering = true;
        var oldTexture = Texture;
        var cancellationToken = GetCancellationToken();

        try
        {
            Texture = await Task.Run(
                () => _renderer.Render(_chars = GenerateChars(), metadata, out _glyphs, cancellationToken),
                cancellationToken);

            oldTexture?.Dispose();
        }
        catch (OperationCanceledException)
        {
            // Canceled.
        }
        catch (Exception ex)
        {
            _messageBox.Show(ex.ToString());
        }
        finally
        {
            Rendering = false;
        }
    }

    private CancellationToken GetCancellationToken()
    {
        var newTokenSource = new CancellationTokenSource();
        using var oldTokenSource = Interlocked.Exchange(ref _cancellationTokenSource, newTokenSource);
        return newTokenSource.Token;
    }

    private string[] GenerateChars()
    {
        var minChar = CharHelper.ConvertToChar(MinChar);
        var maxChar = CharHelper.ConvertToChar(MaxChar);
        return CharHelper.GetChars(minChar, maxChar);
    }

    private int FindGlyph(Point point)
    {
        var span = _glyphs.AsSpan();

        for (var i = 0; i < span.Length; i++)
        {
            if (span[i].Rect.Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    private TextureMetadata GetTextureMetadata()
    {
        return new TextureMetadata
        {
            FontName = FontName,
            FontSize = FontSize,
            Kerning = Kerning,
            Leading = Leading,
            Antialiased = Antialiased,
            Foreground = Foreground.ToUInt32(),
            FontWeight = CombinedFontStyle.FontWeight,
            FontStretch = CombinedFontStyle.FontStretch,
            FontStyle = CombinedFontStyle.FontStyle,
            MinChar = CharHelper.ConvertToChar(MinChar),
            MaxChar = CharHelper.ConvertToChar(MaxChar),
            TextureWidth = TextureWidth,
            DropShadow = GetShadowMetadata(),
            Outline = GetStrokeMetadata(),
        };
    }

    private DropShadowMetadata? GetShadowMetadata()
    {
        if (!ShadowEnabled)
        {
            return null;
        }

        return new DropShadowMetadata
        {
            Blur = ShadowBlur,
            OffsetX = ShadowOffsetX,
            OffsetY = ShadowOffsetY,
            Color = ShadowColor.ToUInt32(),
        };
    }

    private StrokeMetadata? GetStrokeMetadata()
    {
        if (!OutlineEnabled)
        {
            return null;
        }

        return new StrokeMetadata
        {
            Width = OutlineWidth,
            Color = OutlineColor.ToUInt32(),
        };
    }
}
