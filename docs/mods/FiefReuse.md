# 家园反复继承 1.0.0

开局选择跨周目共享家园后，可在任意周目的多个存档之间分批存取珍宝阁装备、拓印悟道碑秘籍。私人家园保持原版限制；容量、实物转移、满级刻印和槽位数不变，不复制物品。

入口：**原版家园界面**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行；`-GamePath`填写实际游戏目录。

```powershell
& .\Manage-Mods.ps1 -Action Install -Mod FiefReuse -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
& .\Manage-Mods.ps1 -Action Uninstall -Mod FiefReuse -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
```

配置：`BepInEx/config/local.daxia.fiefreuse.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/FiefReuse](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/FiefReuse)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
