# 大侠立志传Mod合集

19个独立插件，减少重复SL、等待、重复设置与查找成本。各插件独立启停和安装，源码采用MIT许可。

适配《大侠立志传》Windows Steam **Build 24794582**。安装器核对Build和GameAssembly SHA-256；其他版本拒绝安装。

## 下载与安装

从[Releases](https://github.com/nikonikoni7/daxia-mods/releases)下载ZIP及对应SHA-256文件；GitHub的Source code压缩包不含DLL。

先在下载文件所在目录核对ZIP的SHA-256，与下载的`.sha256`文件比较：

```powershell
Get-FileHash .\DaxiaMods-v1.0.1-Build24794582.zip -Algorithm SHA256
```

1. 安装BepInEx 6 IL2CPP x64，先启动一次游戏生成本机interop接口，再退出游戏。开发和原生验证使用过6.0.0-be.697；本包不附带加载器、运行时或游戏接口DLL。
2. 解压ZIP。用PowerShell在解压目录运行安装命令。安装器自动读取Steam注册表和各磁盘游戏库，定位包含Wulin.exe的目录。
3. 按需安装单个Mod，然后正常启动游戏。配置在游戏的`BepInEx/config`下。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Install -Mod ChestMemo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Uninstall -Mod ChestMemo
```

`Both`仅选择FastFinish和ExploreCamera。TowerRecovery沿用独立安装器和收据：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-TowerRecovery.ps1 -Action Install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-TowerRecovery.ps1 -Action Uninstall
```

安装器不要求E盘或特定Steam库位置。找不到游戏或发现多个安装目录时，会停止并提示手动指定，不会猜测目标。可以在Steam库中右键游戏，选择“管理 → 浏览本地文件”，复制打开的文件夹路径：

```powershell
$game = Read-Host '粘贴包含Wulin.exe的游戏文件夹路径'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Install -Mod ChestMemo -GamePath $game
# 通天塔插件同样支持：
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-TowerRecovery.ps1 -Action Install -GamePath $game
```

升级先卸载旧版，再安装新版。保留`.daxia-mods-store`和TowerRecovery插件目录内的收据，以便核验及回退；安装器保留已有配置和其他插件，不管理、替换或卸载前置运行环境。

## 插件清单

| Mod / 说明 | 版本 | 默认入口 | 功能 |
| --- | --- | --- | --- |
| [FastFinish](docs/mods/FastFinish.md) | 1.0.0 | 自动 | 快速结算 |
| [ExploreCamera](docs/mods/ExploreCamera.md) | 1.0.4 | Y / Esc | 探索自由镜头 |
| [ChestMemo](docs/mods/ChestMemo.md) | 1.0.6 | L | 宝箱备忘录 |
| [CompanionTraining](docs/mods/CompanionTraining.md) | 1.0.1 | 自动 | 队友修炼记忆 |
| [PerfectRolls](docs/mods/PerfectRolls.md) | 1.1.1 | 自动 | 装备极品数值 |
| [FreeNaming](docs/mods/FreeNaming.md) | 1.0.0 | 原版命名窗口 | 自由取名 |
| [FiefReuse](docs/mods/FiefReuse.md) | 1.0.0 | 原版家园界面 | 家园反复继承 |
| [FiefPositionFix](docs/mods/FiefPositionFix.md) | 1.0.2 | 自动 | 家园返回位置修复 |
| [ItemSpawner](docs/mods/ItemSpawner.md) | 1.0.2 | K | 中文物品添加器 |
| [AuctionRefresh](docs/mods/AuctionRefresh.md) | 1.0.0 | 拍卖入场预览按钮 | 拍卖刷新与倒计时 |
| [AuctionSkyLantern](docs/mods/AuctionSkyLantern.md) | 1.0.1 | 原版竞价区按钮 | 拍卖点天灯 |
| [BountyRefresh](docs/mods/BountyRefresh.md) | 1.0.0 | 原版公告栏按钮 | 公告栏刷新 |
| [QuestTracking](docs/mods/QuestTracking.md) | 1.0.0 | 原版任务勾选 | 五任务追踪 |
| [CompanionTravel](docs/mods/CompanionTravel.md) | 1.0.1 | 伙伴的故事中勾选 | 伙伴游历追踪 |
| [AnswerHints](docs/mods/AnswerHints.md) | 1.0.1 | 原版答题选项 | 答题提示 |
| [FusionRename](docs/mods/FusionRename.md) | 1.0.1 | F6 / Esc | 融合武功重新取名 |
| [KungfuOrder](docs/mods/KungfuOrder.md) | 1.0.1 | 武功页长按左键 | 武功拖动换位 |
| [FiefLibrary](docs/mods/FiefLibrary.md) | 1.0.4 | 家园正房书架交互 | 家园藏经阁 |
| [TowerRecovery](docs/mods/TowerRecovery.md) | 1.0.0 | 自动 | 通天塔战后休整 |

## 组合使用与保存

- CompanionTravel与QuestTracking共用5个追踪名额；前者保留先于后者执行的补丁顺序。
- AuctionRefresh负责入场预览，AuctionSkyLantern负责逐件竞价，可同时使用。
- FiefReuse、PerfectRolls与FusionRename分别控制继承次数、装备已有数值和融合名称，可组合使用；珍宝阁仍执行原版物品转移。
- FreeNaming可与FusionRename组合；自定义窗口保留现有输入和暂停机制。
- 涉及物品、属性、顺序或名称的变更按各插件说明由原版正常保存。卸载不自动还原已保存的效果；操作前自行保留可用存档备份。

本仓库只收录通用功能，不包含个人维修工具、个人存档、初始化记录或历史调查资料。自动验证范围见[验证记录](docs/VALIDATION.md)，人工游戏验收已明确跳过，未执行。

## 开发与许可

[开发说明](docs/DEVELOPMENT.md) · [MIT许可证](LICENSE) · [第三方与游戏材料归属](THIRD_PARTY_NOTICES.md)

源码位于`src/mods/<Mod>`；共享源码仍按原路径分别编入独立DLL。克隆仓库不含SDK、游戏派生引用或编译产物，需先准备本机依赖。
