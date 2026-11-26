# 答题提示 1.0.1

仅在隆中书院、太学馆、院试/乡试/会试、蔡攸之吟诗作对和儒圣馆祭酒解惑的匹配题组中，将正确选项底色标为浅金色。保留选项文字、顺序、鼠标与手柄操作，以及原版判分、奖励和保存。

入口：**原版答题选项**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行；`-GamePath`填写实际游戏目录。

```powershell
& .\Manage-Mods.ps1 -Action Install -Mod AnswerHints -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
& .\Manage-Mods.ps1 -Action Uninstall -Mod AnswerHints -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
```

配置：`BepInEx/config/local.daxia.answerhints.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/AnswerHints](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/AnswerHints)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
