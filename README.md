# [OasisBot](https://silkroad-developer-community.github.io/OasisBot/)

Free, open source Silkroad Online bot for everyone to use!

Join our [Discord server](https://discord.gg/FEmNcz7QwP) for updates, announcements and discussions.

[![GitHub Issues](https://img.shields.io/github/issues/Silkroad-Developer-Community/OasisBot?label=Open%20Issues)](https://github.com/Silkroad-Developer-Community/OasisBot/issues)
[![downloads](https://img.shields.io/github/downloads/Silkroad-Developer-Community/OasisBot/total?label=Total%20Downloads)](https://github.com/Silkroad-Developer-Community/OasisBot/releases)
[![downloads-latest](https://img.shields.io/github/downloads/Silkroad-Developer-Community/OasisBot/latest/total?label=Latest%20release)](https://github.com/Silkroad-Developer-Community/OasisBot/releases/latest)

| Links                                                                                                                                                                                                                  |                                            |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| [![release-latest](https://img.shields.io/github/v/release/Silkroad-Developer-Community/OasisBot?label=Latest%20Stable&style=for-the-badge)](https://github.com/Silkroad-Developer-Community/OasisBot/releases/latest) | Latest stable release                      |
| [![release-all](https://img.shields.io/badge/Latest%20Release-Nightly-FF0000?style=for-the-badge)](https://github.com/Silkroad-Developer-Community/OasisBot/releases)                                                  | Nightly releases for most recent features  |
| [![docs](https://img.shields.io/badge/OasisBot-Docs-FF00FF?style=for-the-badge)](https://Silkroad-Developer-Community.github.io/OasisBot)                                                                              | Documentation, tips & tricks and tutorials |

## Building

You can run the following commands for setup:

```shell
winget install Microsoft.DotNet.DesktopRuntime.8 --architecture x86 --force --accept-package-agreements --accept-source-agreements
winget install Microsoft.DotNet.SDK.8
winget install Microsoft.VisualStudio.BuildTools --override "--wait --quiet --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --includeRecommended"
git clone --recursive https://github.com/Silkroad-Developer-Community/OasisBot.git
cd OasisBot
dotnet restore
powershell -ExecutionPolicy Bypass .\scripts\build.ps1
```

Alternatively, you can also install `.NET desktop development` and `Desktop development with C++` from the Visual Studio Installer.

You can take builds with <kbd>Ctrl+Shift+B</kbd> from Visual Studio or VSCode and its forks. You can also use the included [build script](scripts/build.ps1).

## Supported clients

| Region          | Version                       |
| :-------------- | :---------------------------- |
| Chinese         | ICCGame                       |
| Chinese Old     | cSRO/-R                       |
| Global          | iSRO (International Silkroad) |
| Japanese        | JSRO                          |
| Japanese Old    | JSRO_SL                       |
| Korean          | KSRO                          |
| Rigid           | iSRO 2015                     |
| Russia          | RuSro                         |
| Taiwan          | Digeam                        |
| Taiwan Old      | TSRO 110                      |
| Thailand        | Blackrogue 100                |
| Thailand        | Blackrogue 110                |
| Turkey          | TRSRO                         |
| Vietnam         | vSRO 188                      |
| Vietnam         | vSRO 193                      |
| Vietnam         | vSRO 274                      |
| Vietnam         | VTC Game                      |
| ~~Chinese Old~~ | ~~MHTC~~                      |
| ~~Japanese-R~~  |                               |

## Credits

OasisBot was originally developed by [**ngoedde**](https://github.com/ngoedde) (Niklas Gödde/torstmn/Wimbeam) and [**SDClowen**](https://github.com/myildirimofficial) (Mahmut YILDIRIM). This repository is a community-led fork maintained by the Silkroad Developer Community.
