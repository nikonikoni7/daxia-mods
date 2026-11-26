# 宝箱备忘录 1.0.6

仅记录缺钥匙导致开启失败的宝箱，原版确认开启成功才移除。按地图、钥匙或不分类查看；持有当前场景地图并打开原版地图时显示纯红点。记录按账号、槽位路径和存档内容隔离，卸载保留；当前还包含主菜单对话立绘预热，可在Performance配置中关闭。

入口：**L**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行；`-GamePath`填写实际游戏目录。

```powershell
& .\Manage-Mods.ps1 -Action Install -Mod ChestMemo -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
& .\Manage-Mods.ps1 -Action Uninstall -Mod ChestMemo -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
```

配置：`BepInEx/config/local.daxia.chestmemo.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/ChestMemo](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/ChestMemo)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
