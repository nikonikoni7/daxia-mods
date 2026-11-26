# 开发与构建

Windows、PowerShell、Python 3（`py -3`）。构建使用.NET SDK 8.0.425、引用包8.0.31与Roslyn，无需创建sln或csproj。

## 准备本机引用

先在合法本机游戏安装BepInEx 6 IL2CPP并运行一次，生成匹配Build 24794582的接口。SDK可使用仓库入口安装：

```powershell
py -3 .\scripts\get-sdk.py
$game = 'E:\SteamLibrary\steamapps\common\WulinSH'
$refs = '.\outputs\DaxiaMods\payload'
New-Item -ItemType Directory -Path "$refs\BepInEx" -Force
Copy-Item -LiteralPath "$game\dotnet" -Destination "$refs\dotnet" -Recurse
Copy-Item -LiteralPath "$game\BepInEx\core" -Destination "$refs\BepInEx\core" -Recurse
Copy-Item -LiteralPath "$game\BepInEx\interop" -Destination "$refs\BepInEx\interop" -Recurse
```

上述复制命令用于首次准备空引用目录。SDK及引用被Git忽略，不应提交游戏派生DLL。接口与BepInEx版本须与适配环境一致；当前没有经过新机器验收的一键环境恢复流程。测试固定引用包版本，不通过跳过BuildGuard适配其他Build。

## 编译与回归

```powershell
& .\scripts\Build.ps1 -RunTests
py -3 -m unittest discover -s tests -p 'test_public_*.py' -v
```

输出位于`outputs/DaxiaMods/plugins`，规则和接口模拟测试位于`tests/mods`。TowerRecovery的独立构建入口为`scripts/Build-TowerRecovery.ps1`，已纳入完整构建。原生探针与个人调查脚本未收录本公开版本；这些命令不启动游戏。

## 打包

```powershell
py -3 .\scripts\package-public.py
```

固定白名单为`mods.json`，默认配置维护源在`config/defaults`。发布入口先核对完整19个DLL，再使用新的临时目录复制允许文件，生成ZIP、逐文件SHA-256清单和ZIP校验文件。输出到`outputs/releases`；同名发行文件存在时拒绝覆盖。安装布局与收据保持原契约，TowerRecovery仍使用独立安装器。

构建会写本地work和outputs；本公开工程不安装到开发者的正式游戏目录，也不运行会启动游戏的Smoke。
