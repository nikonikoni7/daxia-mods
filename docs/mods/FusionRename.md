# 融合武功重新取名 1.0.1

自由探索时按F6，列出主角、当前队友学会的融合武功和背包持有的融合秘籍，同一武功合并展示。免费反复改名，同步当前档武功、秘籍和仍刻着同一武功的共享碑位；其他档已经拓印的版本保留旧名。只改名称，不发放秘籍、不改数值；可与FreeNaming组合，当前档由玩家正常保存。

入口：**F6 / Esc**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行；`-GamePath`填写实际游戏目录。

```powershell
& .\Manage-Mods.ps1 -Action Install -Mod FusionRename -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
& .\Manage-Mods.ps1 -Action Uninstall -Mod FusionRename -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
```

配置：`BepInEx/config/local.daxia.fusionrename.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/FusionRename](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/FusionRename)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
