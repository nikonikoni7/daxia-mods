# 五任务追踪 1.0.0

剧情与悬赏共用5个追踪名额，第6个拒绝并提示。保留原版手柄、鼠标、取消追踪、任务进度和保存流程；停用或卸载不删除已保存的额外标记，需要减少追踪时先在游戏中取消。

入口：**原版任务勾选**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行。安装器自动查找Steam游戏目录，无需固定盘符；如未识别或发现多个安装位置，请按[合集说明](../../README.md)手动传入`-GamePath`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Install -Mod QuestTracking
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Uninstall -Mod QuestTracking
```

配置：`BepInEx/config/local.daxia.questtracking.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/QuestTracking](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/QuestTracking)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
