# 队友修炼记忆 1.0.1

记忆队友成功离队前的修炼秘籍，支持负数编号的融合秘籍。成功归队时仅尝试一次恢复：已有秘籍、被占用、丢失或条件不满足时不替换、不抢占。离队期间不修炼；记录随原版保存检查点隔离，卸载保留。

入口：**自动**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行；`-GamePath`填写实际游戏目录。

```powershell
& .\Manage-Mods.ps1 -Action Install -Mod CompanionTraining -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
& .\Manage-Mods.ps1 -Action Uninstall -Mod CompanionTraining -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
```

配置：`BepInEx/config/local.daxia.companiontraining.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/CompanionTraining](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/CompanionTraining)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
