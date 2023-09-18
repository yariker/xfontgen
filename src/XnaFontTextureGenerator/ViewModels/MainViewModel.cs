using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
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
    private const float DefaultFontSize = 30;
    private const uint DefaultForegroundColor = 0xFFFFFFFF;
    private const int DefaultShadowBlur = 3;
    private const int DefaultShadowOffsetX = 1;
    private const int DefaultShadowOffsetY = 1;
    private const uint DefaultShadowColor = 0xFF000000;
    private const float DefaultOutlineWidth = 1;
    private const uint DefaultOutlineColor = 0xFFFF0000;

    private readonly SynchronizationContext _synchronizationContext = SynchronizationContext.Current!;
    private readonly Subject<TextureMetadata> _renderQueue = new();

    private readonly IStorageProvider _storageProvider;
    private readonly IFontTextureRenderer _renderer;
    private readonly IMessageBox _messageBox;
    private readonly IDisposable _renderWorker;

    [ObservableProperty]
    private bool _lightMode;

    [ObservableProperty]
    private bool _rendering;

    [ObservableProperty]
    private string _fontName = null!;

    [ObservableProperty]
    private float _fontSize = DefaultFontSize;

    [ObservableProperty]
    private bool _antialiased = true;

    [ObservableProperty]
    private CombinedFontStyle _combinedFontStyle;

    [ObservableProperty]
    private string _minChar = "32";

    [ObservableProperty]
    private string _maxChar = "126";

    [ObservableProperty]
    private bool _shadowEnabled;

    [ObservableProperty]
    private float _shadowBlur = DefaultShadowBlur;
    
    [ObservableProperty]
    private float _shadowOffsetX = DefaultShadowOffsetX;

    [ObservableProperty]
    private float _shadowOffsetY = DefaultShadowOffsetY;

    [ObservableProperty]
    private Color _shadowColor = Color.FromUInt32(DefaultShadowColor);

    [ObservableProperty]
    private bool _outlineEnabled;

    [ObservableProperty]
    private float _outlineWidth = DefaultOutlineWidth;

    [ObservableProperty]
    private Color _outlineColor = Color.FromUInt32(DefaultOutlineColor);

    [ObservableProperty]
    private int _textureWidth = 512;

    [ObservableProperty]
    private Bitmap? _texture;

#if DEBUG
    public MainViewModel()
        : this(null!, null!, null!)
    {
    }
#endif

    public MainViewModel(IStorageProvider storageProvider, IFontTextureRenderer renderer, IMessageBox messageBox)
    {
        if (!Design.IsDesignMode)
        {
            ArgumentNullException.ThrowIfNull(storageProvider);
            ArgumentNullException.ThrowIfNull(renderer);
            ArgumentNullException.ThrowIfNull(messageBox);
        }

        _combinedFontStyle = new CombinedFontStyle("Regular", FontWeight.Normal, FontStretch.Normal, FontStyle.Normal);
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

        FontStyles = new[]
        {
            _combinedFontStyle,
            new CombinedFontStyle("Italic", FontWeight.Normal, FontStretch.Normal, FontStyle.Italic),
            new CombinedFontStyle("Bold", FontWeight.Bold, FontStretch.Normal, FontStyle.Normal),
            new CombinedFontStyle("Bold, Italic", FontWeight.Bold, FontStretch.Normal, FontStyle.Italic),
        };
    }

    public IReadOnlyList<string> FontNames { get; }

    public IReadOnlyList<CombinedFontStyle> FontStyles { get; }

    [RelayCommand]
    protected async Task ExportAsync()
    {
        var storageFile = await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
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

        var metadata = GetTextureMetadata();

        using (var bitmap = _renderer.Render(GenerateChars(), metadata))
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

    [RelayCommand]
    protected async Task ImportAsync()
    {
        var storageFiles = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Font Texture",
            FileTypeFilter = new[] { FilePickerFileTypes.ImagePng },
            AllowMultiple = false,
        });

        var storageFile = storageFiles.SingleOrDefault();
        if (storageFile == null)
        {
            return;
        }
        
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
        CombinedFontStyle = FontStyles.FirstOrDefault(x => x.IsMatch(metadata)) ?? FontStyles[0];
        Antialiased = metadata.Antialiased;
        MinChar = metadata.MinChar.ToString();
        MaxChar = metadata.MaxChar.ToString();
        TextureWidth = metadata.TextureWidth;

        ShadowEnabled = metadata.DropShadow != null;
        ShadowBlur = metadata.DropShadow?.Blur ?? DefaultShadowBlur;
        ShadowOffsetX = metadata.DropShadow?.OffsetX ?? DefaultShadowOffsetX;
        ShadowOffsetY = metadata.DropShadow?.OffsetY ?? DefaultShadowOffsetY;
        ShadowColor = Color.FromUInt32(metadata.DropShadow?.Color ?? DefaultShadowColor);

        OutlineEnabled = metadata.Outline != null;
        OutlineWidth = metadata.Outline?.Width ?? DefaultOutlineWidth;
        OutlineColor = Color.FromUInt32(metadata.Outline?.Color ?? DefaultOutlineColor);
    }

    private async Task RenderAsync(TextureMetadata metadata)
    {
        Rendering = true;
        var oldTexture = Texture;

        try
        {
            var chars = GenerateChars();
            Texture = await Task.Run(() => _renderer.Render(chars, metadata));
            oldTexture?.Dispose();
        }
        finally
        {
            Rendering = false;
        }
    }

    partial void OnMinCharChanging(string value)
    {
        if (ConvertChar(value) < char.MinValue)
        {
            throw new ArgumentOutOfRangeException($"Out of range value '{value}'.");
        }
    }

    partial void OnMaxCharChanging(string value)
    {
        if (ConvertChar(value) > char.MaxValue)
        {
            throw new ArgumentOutOfRangeException($"Out of range value '{value}'.");
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FontSize) && FontSize <= 0)
        {
            FontSize = 1;
            return;
        }

        base.OnPropertyChanged(e);

        if (e.PropertyName != nameof(LightMode) &&
            e.PropertyName != nameof(Texture) &&
            e.PropertyName != nameof(Rendering))
        {
            _renderQueue.OnNext(GetTextureMetadata());
        }
    }

    private string[] GenerateChars()
    {
        var minChar = ConvertChar(MinChar);
        var maxChar = ConvertChar(MaxChar);
        var count = maxChar - minChar + 1;

        return count > 0
            ? Enumerable.Range(minChar, count).Select(c => ((char)c).ToString()).ToArray()
            : Array.Empty<string>();
    }

    private static int ConvertChar(string value)
    {
        if (value.StartsWith("0x", StringComparison.Ordinal) &&
            int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var hex))
        {
            return hex;
        }

        if (int.TryParse(value, out var integer))
        {
            return integer;
        }

        if (value.Length == 1)
        {
            return value[0];
        }

        throw new ArgumentException($"Invalid character code '{value}'.");
    }

    private TextureMetadata GetTextureMetadata()
    {
        return new TextureMetadata
        {
            FontName = FontName,
            FontSize = FontSize,
            Antialiased = Antialiased,
            Foreground = DefaultForegroundColor,
            FontWeight = CombinedFontStyle.FontWeight,
            FontStretch = CombinedFontStyle.FontStretch,
            FontStyle = CombinedFontStyle.FontStyle,
            MinChar = ConvertChar(MinChar),
            MaxChar = ConvertChar(MaxChar),
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
