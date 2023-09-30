# XNA Font Texture Generator

An utility that generates a bitmap-font texture (sprite sheet) that is compatible with the
[SpriteFont](https://learn.microsoft.com/en-us/previous-versions/windows/xna/bb464165(v=xnagamestudio.40)) and
[FontTextureProcessor](https://learn.microsoft.com/previous-versions/windows/xna/bb464071(v=xnagamestudio.40))
included in [MonoGame](https://github.com/MonoGame/MonoGame) and [FNA](https://github.com/FNA-XNA/FNA)
(successors to the Microsoft's XNA Framework).

<p align='center'>
  <img src='doc/Screenshot.png' width='90%' />
</p>

## Download and usage

Get the latest version in [Releases](https://github.com/yariker/xfontgen/releases), unzip it to a folder,
and run `xfontgen`.

The app can be run on the following platforms:
* Windows 10+ (x64)
* Linux (Debian 10+/Ubuntu 18.04+/Fedora 36+) (x64)
* macOS 10.15+ (x64)

Executing the app requires [.NET 7.0 Runtime](https://dotnet.microsoft.com/download/dotnet/7.0) to be installed.

### Adding bitmap-font texture to your project

When adding the generated PNG texture to your content pipeline, make sure to set the following content settings:
* Importer – Texture Importer
* Processor – Font Texture
* First Character – Should match "Min char"

<p align='center'>
  <img src='doc/Mgcb.png' width='250'/>
</p>

Such a sprite font can then be loaded into the `SpriteFont` object:

```csharp
_spriteFont = Content.Load<SpriteFont>("TestFont");
```

Which can then be drawn by regular means of `SpriteBatch.DrawString` as follows:

```csharp
_spriteBatch.Begin();

_spriteBatch.DrawString(_spriteFont, "The quick brown fox\r\njumps over the lazy dog.", 
    new Vector2(30, 30), Color.MediumSpringGreen);

_spriteBatch.End();
```

<p align='center'>
  <img src='doc/Demo.png' width='500' />
</p>

## License

Code licensed under the [MIT License](LICENSE.txt).
