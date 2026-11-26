# 伙伴游历追踪 1.0.1

华青青和洛千雪原版曾入队后，在伙伴的故事中显示游历进度，离队保留。按原版剧情标记移除完成地点，破庙仅显示开局实际救助的角色；与QuestTracking共用5个名额。卸载前取消这两条追踪并正常保存，避免原版保留不可见名额。

入口：**伙伴的故事中勾选**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行。安装器自动查找Steam游戏目录，无需固定盘符；如未识别或发现多个安装位置，请按[合集说明](../../README.md)手动传入`-GamePath`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Install -Mod CompanionTravel
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Uninstall -Mod CompanionTravel
```

配置：`BepInEx/config/local.daxia.companiontravel.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/CompanionTravel](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/CompanionTravel)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
