# 锻体 · Sephiria BodyForge

赛兔（Sephiria）的独立锻体 Mod，使用游戏原生 AddOn 加载，当前版本 **2.6.1**。

消耗神器或石板，从三份配方中选择一份，获得本局永久属性；有色材料还可以给另一件神器增加附魔。配方会参考当前羁绊，提供构筑倾向与通用奖励。

## 下载安装

在 [Releases](https://github.com/Furxier/Sephiria-BodyForge/releases/latest) 下载 `BodyForge-2.6.1.zip`，解压后将 `BodyForge` 文件夹放入游戏的 `AddOns` 目录，然后启动游戏。

不需要 SPMod、SilentInventory 或 BepInEx。发布包自带的依赖 DLL 请保留。GitHub 自动生成的 Source code 压缩包仅供开发，不能当作 Mod 安装。

## 使用

打开背包，点击旁边的“锻体”，选择材料、附魔目标和配方，再确认。普通白色材料无需选择附魔目标。

| 稀有度 | 附魔 | 重掷次数 |
| --- | ---: | ---: |
| 普通 | 0 | 0 |
| 罕见 | +1 | 1 |
| 稀有 | +2 | 2 |
| 传奇 | +3 | 3 |
| 永恒 | +5 | 5 |

- 实际确认的锻体附魔每累计 10 级，扩容 1 格，本局最多 10 格。
- 收益页可查看累计属性，支持分组、滚轮滚动与双列布局。
- 背包按钮可拖动；弹窗可从右下角调整大小。
- 默认开启，总开关与专属奖励开关详见 [使用说明](Distribution/使用说明.md)。

## 本地构建

需要 Windows、游戏本体，以及已解压的 BodyForge 发布包（用于提供 Harmony 等依赖）。游戏程序集不包含在源码仓库中。

在仓库根目录执行，把游戏路径替换为你自己的安装目录：

```powershell
& ./Source/build.ps1 -GameDirectory 'D:\steam\steamapps\common\Sephiria'
& ./Source/Tests/ForgeGameApi.Tests.ps1 -GameDirectory 'D:\steam\steamapps\common\Sephiria' -AssemblyPath ./artifacts/BodyForge.dll
& ./package.ps1 -GameDirectory 'D:\steam\steamapps\common\Sephiria'
```

也可设置环境变量 `SEPHIRIA_DIR`。编译与打包结果保存在 `artifacts/`；默认不会覆盖正在使用的 Mod。游戏根目录下 `ModDevelopment/BodyForge` 的布局仍可自动找到游戏路径。

`Source/Tests` 中的其他检查脚本可分别执行。源码、测试和构建脚本公开；本地讨论、设计、数值分析和 Review 文档不随仓库发布。发布包按文件白名单生成，不含源码、个人配置和操作日志。

## 已知限制

通过游戏原生请求同步奖励，包含客机的确认流程，但未完成所有联机场景的实测。超时不自动重复发放；可能发生部分完成，超时后到账的属性也可能未计入收益显示。统计仅保留当前连接的本次冒险。

14 组自动检查通过不等于长时间内存与全部分辨率的实机验收。原生永久属性对象和本地操作日志会随使用次数增长。反馈问题时请提供版本、单机／房主／客机、材料稀有度和所选配方。
