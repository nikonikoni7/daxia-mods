# 公告栏刷新 1.0.0

当前公告栏完整三个任务都为Open时免费同时重抽，不耗游戏时间，允许再次抽到相同任务。成功接取任一任务后锁定，交付、自动完成或放弃都不解锁，等待原版定时更新恢复完整三个Open；每块公告栏独立。

入口：**原版公告栏按钮**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行。安装器自动查找Steam游戏目录，无需固定盘符；如未识别或发现多个安装位置，请按[合集说明](../../README.md)手动传入`-GamePath`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Install -Mod BountyRefresh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Uninstall -Mod BountyRefresh
```

配置：`BepInEx/config/local.daxia.bountyrefresh.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/BountyRefresh](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/BountyRefresh)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
