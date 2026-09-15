# [OasisBot](https://silkroad-developer-community.github.io/OasisBot/)

Free, open source Silkroad Online bot for everyone to use!

Feel free to edit the code, create pull requests for any and all improvements, create issues and request features. [Supported clients](#supported-clients) that are listed below are a result of prolonged community work, do not hesitate to accompany us!

To join the conversation, get recent updates/announcements, join our [Discord server](https://discord.gg/FEmNcz7QwP).

[![GitHub Issues](https://img.shields.io/github/issues/Silkroad-Developer-Community/OasisBot?label=Open%20Issues)](https://github.com/Silkroad-Developer-Community/OasisBot/issues)
[![downloads](https://img.shields.io/github/downloads/Silkroad-Developer-Community/OasisBot/total?label=Total%20Downloads)](https://github.com/Silkroad-Developer-Community/OasisBot/releases)
[![downloads-latest](https://img.shields.io/github/downloads/Silkroad-Developer-Community/OasisBot/latest/total?label=Latest%20release)](https://github.com/Silkroad-Developer-Community/OasisBot/releases/latest)

| Links                                                                                                                                                                                                                  |                                            |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| [![release-latest](https://img.shields.io/github/v/release/Silkroad-Developer-Community/OasisBot?label=Latest%20Stable&style=for-the-badge)](https://github.com/Silkroad-Developer-Community/OasisBot/releases/latest) | Latest stable release                      |
| [![release-all](https://img.shields.io/badge/Latest%20Release-Nightly-FF0000?style=for-the-badge)](https://github.com/Silkroad-Developer-Community/OasisBot/releases)                                                  | Nightly releases for most recent features  |
| [![release-manager](https://img.shields.io/badge/Latest%20Release-Manager-00DD00?style=for-the-badge)](https://github.com/Silkroad-Developer-Community/OasisBot-Manager/releases/latest)                               | Manager for multiple bot profiles          |
| [![docs](https://img.shields.io/badge/OasisBot-Docs-FF00FF?style=for-the-badge)](https://Silkroad-Developer-Community.github.io/OasisBot)                                                                              | Documentation, tips & tricks and tutorials |

## Building the project

- Clone the repository with the command `git clone --recursive https://github.com/Silkroad-Developer-Community/OasisBot.git`)

### Visual Studio

- Open the project in [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) (Required workloads are `.NET desktop development` and `Desktop development with C++`)
- Build the project (<kbd>Ctrl+Shift+B</kbd>)
- Run the compiled executable from `Build\OasisBot.exe`

### Other

Run the commands below (You still need MSBuild tooling via Visual Studio):

- `dotnet restore`
- `powershell -ExecutionPolicy Bypass .\scripts\build.ps1`

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

| Copyright Owners                                                                                     | Year         | Contribution       |
| ---------------------------------------------------------------------------------------------------- | ------------ | ------------------ |
| [ngoedde](https://github.com/ngoedde)                                                                | 2017-2021    | Original Developer |
| RSBot Team ([ngoedde](https://github.com/ngoedde), [SDClowen](https://github.com/myildirimofficial)) | 2021-2026    | Continuation team  |
| [Silkroad Developer Community](https://silkroad-developer-community.github.io/#governance)           | 2026-current | Community fork     |
