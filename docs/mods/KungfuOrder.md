# 武功拖动换位 1.0.1

在主角或队友原版武功页的普通武功上按住左键0.15秒，拖到另一项松开交换，中间项不动。短按保留原版操作，阈值前移动超过8像素保留滚动；绝招区域不参与拖动。顺序随原版保存，战斗快捷键按左列从上到下、再右列从上到下，绝招在前；战斗中不可拖动。

入口：**武功页长按左键**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行。安装器自动查找Steam游戏目录，无需固定盘符；如未识别或发现多个安装位置，请按[合集说明](../../README.md)手动传入`-GamePath`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Install -Mod KungfuOrder
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Uninstall -Mod KungfuOrder
```

配置：`BepInEx/config/local.daxia.kungfuorder.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/KungfuOrder](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/KungfuOrder)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
