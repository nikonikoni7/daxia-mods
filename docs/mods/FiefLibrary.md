# 家园藏经阁 1.0.4

进入家园正房，交互左上墙中间书架打开藏经阁；两栏分别为背包秘籍与书架，选择数量并确认转移。只允许有效秘籍物品，排除正在修炼的实例，支持融合秘籍；取出检查背包空间。内容属于当前存档，共享家园不共享此仓库；转移后正常保存，读旧档回退背包和仓库。卸载保留内容和恢复日志，需在卸载期间使用的秘籍请先取出并保存。

入口：**家园正房书架交互**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行。安装器自动查找Steam游戏目录，无需固定盘符；如未识别或发现多个安装位置，请按[合集说明](../../README.md)手动传入`-GamePath`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Install -Mod FiefLibrary
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Uninstall -Mod FiefLibrary
```

配置：`BepInEx/config/local.daxia.fieflibrary.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/FiefLibrary](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/FiefLibrary)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。

保存恢复日志位于`BepInEx/config/FiefLibrary/recovery`。发现日志或仓库损坏时保留存档、原版备份与恢复目录，不手工拼接文件。公开包不含任何个人位置恢复记录。
