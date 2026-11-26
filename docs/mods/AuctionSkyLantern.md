# 拍卖点天灯 1.0.1

每件拍品单独点击点天灯，锁定本件手动操作并按原版放弃后的倍率加速。第一次提示后的开放报价窗口自动调用原版加一成策略，每件至多一次；已经领先不加价，资金不足保持原版成交结果。玩家寄售和已放弃拍品不可点灯，下一件恢复正常。

入口：**原版竞价区按钮**。适配Windows Steam Build **24794582**，需要BepInEx 6 IL2CPP。安装前请阅读[合集说明](../../README.md)。

## 安装与卸载

退出游戏，在解压后的发行包根目录执行；`-GamePath`填写实际游戏目录。

```powershell
& .\Manage-Mods.ps1 -Action Install -Mod AuctionSkyLantern -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
& .\Manage-Mods.ps1 -Action Uninstall -Mod AuctionSkyLantern -GamePath 'E:\SteamLibrary\steamapps\common\WulinSH'
```

配置：`BepInEx/config/local.daxia.auctionskylantern.cfg`。已有配置优先保留；修改配置后重新启动游戏。

升级时先卸载旧版，再安装新版。卸载保留修改过的配置及插件记录，不会自动撤销已经由原版保存的游戏状态。

## 源码与验证

维护源码：[src/mods/AuctionSkyLantern](https://github.com/nikonikoni7/daxia-mods/tree/main/src/mods/AuctionSkyLantern)。构建和回归入口见[开发说明](../DEVELOPMENT.md)。公开合集的实际验证记录见[验证记录](../VALIDATION.md)。人工游戏验收由维护者明确跳过，未执行。
