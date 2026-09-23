# 锻体 · Sephiria BodyForge

赛兔（Sephiria）的锻体 Mod，使用游戏原生 AddOn 加载。当前版本 **2.8.37**。

消耗神器或石板，从三份配方中选择一份，获得本局永久属性；有色材料还可以给另一件神器增加附魔。配方参考当前羁绊，提供构筑倾向与通用奖励。

## 安装与使用

从 [Releases](https://github.com/Furxier/Sephiria-BodyForge/releases/latest) 下载 Mod ZIP，解压后将 `BodyForge` 文件夹放入游戏的 `AddOns` 目录。更新前退出游戏，覆盖同名文件。

打开背包，点击旁边的“锻体”，选择材料、附魔目标和配方，再确认。普通白色材料无需选择附魔目标。支持背包下方栏位的材料。

| 材料稀有度 | 附魔等级 | 重掷次数 |
| --- | ---: | ---: |
| 普通 | 0 | 0 |
| 罕见 | +1 | 1 |
| 稀有 | +2 | 2 |
| 传奇 | +3 | 3 |
| 永恒 | +5 | 5 |

每累计确认获得 **10 级附魔**，获得一次里程碑三选一：**背包 +1 格／无视防御 +3%／HP 偷取 +1%**。背包奖励本局最多 10 格；关闭选择窗口保留领取资格。HP 偷取 1% 对应原生 10 点。

收益页展示累计属性，支持分组、双列和滚动。入口可拖动，弹窗支持右下角缩放。游戏选择繁體中文时，Mod 自动跟随。

详细操作与配置见 [使用说明](Distribution/使用说明.md)。

## 心之重担

复用原版心之重担：放在目标神器下方，根据其羁绊切换属性。按永久附魔等级成长，15 级达到满级效果，石板加级不计入成长。联机使用重担装备功能时，房主与客机均需安装相同版本。

冰川满级提供冻结伤害 +60%、冰属性伤害 +20、冻伤系数 +30%、冻结所需层数 −1；减层数在 13 级解锁，冻结门槛最低 1 层。锻体池加入冻伤伤害，未加入减层数。

## 可选联动

- SP：命运、彗星、锻造、派对配方与重担适配；仅启用相应内容时生效。
- SilentInventory 1.10.0：可从操作面板按指定稀有度触发无需材料的锻体。该 Mod 需单独安装。

## 本地构建

需要 Windows、游戏本体，以及已解压的 BodyForge 发布包所提供的编译依赖。游戏程序集不在源码仓库内。

```powershell
& ./Source/build.ps1 -GameDirectory 'D:\steam\steamapps\common\Sephiria'
& ./Source/Tests/ForgeGameApi.Tests.ps1 -GameDirectory 'D:\steam\steamapps\common\Sephiria' -AssemblyPath ./artifacts/BodyForge.dll
& ./package.ps1 -GameDirectory 'D:\steam\steamapps\common\Sephiria'
```

也可设置 `SEPHIRIA_DIR`。输出位于 `artifacts/`；其余检查脚本位于 `Source/Tests/`，分别在独立 PowerShell 进程中执行。

基础点价集中在 `SharedStatCatalog.cs`；奖励稀有度和限制位于 `ForgeBalance.cs`，配方位于 `ForgeTemplates.cs`，重担成长曲线位于 `HeartProfiles.cs`。机制类词条保留显式估值例外。

## 致谢

特别感谢群友 **月光路灯** 提供的灵感。
