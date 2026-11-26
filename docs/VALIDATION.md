# 公开合集v1.0.1验证

验证日期：2026-11-26。适配Windows Steam Build 24794582。

| 验证 | 实际结果 |
| --- | --- |
| `scripts/Build.ps1 -RunTests` | 19个插件编译成功；回归日志563条PASS输出，命令退出码0 |
| 生产DLL一致性 | 19个DLL的SHA-256均与整理前发行DLL一致 |
| 游戏逻辑源码一致性 | 46个C#源码文件与原维护工程逐字节一致 |
| Python发布工具回归 | 11项安装器保护、7项游戏目录发现、2项打包测试通过（共20项） |
| `tests/Test-Release.ps1` | 候选发行包82项检查通过，覆盖19个Mod的安装、DLL哈希、卸载与配置保留 |
| 受保护文件 | 集成测试隔离目录中的Wulin.exe、GameAssembly.dll及4个前置文件内容和修改时间保持 |

安装器保护测试覆盖缺少加载器、缺少interop、版本不匹配、载荷篡改、路径越界、游戏进程存在、原配置、同名DLL回退、旧收据保留及仅插件包拒绝运行时载荷。游戏进程测试使用重命名的Windows工具，不启动游戏。

打包测试验证19个指定DLL、安装清单哈希和ZIP校验文件，不收录输入目录中的无关DLL、旧输出文件或游戏接口。

人工游戏验收由维护者明确跳过，未执行。本次未启动游戏，也未向正式游戏目录安装、卸载或修改玩家数据。

## 复核命令

```powershell
& .\scripts\Build.ps1 -RunTests
py -3 -m unittest discover -s tests -p 'test_public_*.py' -v
& .\tests\Test-Release.ps1 -Archive '.\outputs\releases\DaxiaMods-v1.0.1-Build24794582.zip' -GameAssembly '<本机游戏目录>\GameAssembly.dll'
```

最后一项只读取提供的GameAssembly并复制到隔离目录。测试不会启动该目录中的Wulin.exe，不需要访问玩家存档。

## v1.0.1安装可移植性修复

两个安装器改为从Steam注册表及libraryfolders.vdf发现实际安装位置，不再预设盘符。额外Steam库、中文和空格目录、自定义安装文件夹、显式路径优先、多安装位置拒绝、未安装拒绝及重复库去重均通过隔离测试。

20项发布工具测试在PowerShell 7和Windows PowerShell 5.1中验证通过；候选包82项安装与卸载检查也在两种宿主中验证通过。自动发现函数在本机只读定位到实际游戏目录，没有安装到正式游戏。
