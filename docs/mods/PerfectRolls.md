# 装备极品数值 1.1.1

成功获得装备后，把已经生成的浮动词条调整到原区间内最有利的值，正面效果取高、负面效果取轻。保留品质、词条种类和数量、需求及制作记录，覆盖锻造、竞宝大会打劫和珍宝阁重新领取。未知词条保留原值；优化随原版保存，卸载不会还原已经保存的数值。

入口：**自动**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行。安装器自动查找Steam游戏目录，无需固定盘符；如未识别或发现多个安装位置，请按[合集说明](../../README.md)手动传入`-GamePath`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Install -Mod PerfectRolls
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Manage-Mods.ps1 -Action Uninstall -Mod PerfectRolls
```

配置：`BepInEx/config/local.daxia.perfectrolls.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/PerfectRolls](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/PerfectRolls)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
