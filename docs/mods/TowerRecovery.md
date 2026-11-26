# 通天塔战后休整 1.0.0

通天塔正常胜利或战败结算后，主角和本场参战、结算后仍存活且在队的人类队友恢复满生命与内力。仅清除本场退场新增的伤痕累累和生命垂危；战前旧伤升级时保留原伤与剩余时间。仅作用于通天塔，不修改积分、层数或奖励。

入口：**自动**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行。安装器自动查找Steam游戏目录，无需固定盘符；如未识别或发现多个安装位置，请按[合集说明](../../README.md)手动传入`-GamePath`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-TowerRecovery.ps1 -Action Install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-TowerRecovery.ps1 -Action Uninstall
```

配置：`BepInEx/config/local.daxia.towerrecovery.cfg`。配置项`General.RestoreVitals`和`General.ClearNewInjuries`分别控制补满与清伤，默认均开启。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/TowerRecovery](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/TowerRecovery)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
