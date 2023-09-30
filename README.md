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

<table>
    <tr style="border: 0px;">
        <td style="border: 0px;">
            <img src='doc/Mgcb.png' />
        </td>
        <td style="border: 0px; vertical-align: top;">
            When adding the generated PNG texture to your content pipeline, make sure to set the following properties:
            <p>
            <ul>
                <li>Importer – Texture Importer</li>
                <li>Processor – Font Texture</li>
                <li>First Character – Should match "Min char"</li>
            </ul>
            </p>
        </td>
    </tr>
</table>

The drawing can then be done by regular means of `SpriteBatch.DrawString`.

## License

Code licensed under the [MIT License](LICENSE).
