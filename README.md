# 工程产线场景演示

这是一个 Unity 2022.3.62f3c1 第一视角工程场景游戏。玩家在控制台按 **E** 启动单件板材的上料、冲压结果切换、出料、机械臂搬运、四边折弯和 H 转台放件演示；按 **R** 重置。场景入口是 `Assets/Scenes/SampleScene.unity`。

## 先读这里

- [FACTORY_DEMO.md](FACTORY_DEMO.md)：游玩方式、设备编号和完整运动顺序。
- [PROJECT_PROGRESS.md](PROJECT_PROGRESS.md)：当前实现、验证记录、待验收项目。
- `Assets/Scripts/LoadingProcessController.cs`：流程状态机、模型绑定、夹爪与机械臂运动、折弯和 H 转台放件。
- `Assets/Editor/LoadingSceneSetup.cs`：场景首次配置与缺失折弯分件引用的修复。
- `Assets/Scripts/FirstPersonWalker.cs`、`FirstPersonDemoViewer.cs`：第一视角移动和观察。

## 当前流程

原料台取板 → 吸盘交给拾取夹爪 → 冲床夹爪沿模型/CAD 水平 Y 轴（Unity 世界 Z）送料 → 保护罩内等待 2 秒并切换为暗灰色 `加工零件 冲压完毕.fbx` → 冲床夹爪沿模型 Y 轴取回，再沿 Unity 世界 X 轴出料并返回 → 成品移到桌面左缘 → `r872` 基座沿 `r775` 长滑槽移动，机械臂吸取成品 → 机械臂翻转成品，在 `r1565/r1547` 上下模之间逐边折弯 → 机械臂将四条立边**朝下**放入 H 夹头间隙，使立边末端接触 `H转台__1__r726` 的最上层台面，中心板高于台面 → 吸盘释放，机械臂退到长滑槽右端等待。

四组 H 夹头间隙由 `r1563/r1564`、`r1560/r1553`、`r1556/r1562`、`r1559/r1561` 构成。立边只需放入间隙，不要求填满。折弯成品由 `加工零件 冲压完毕1–5.fbx` 五块分件组成；单独的第 1 分件只是中心板。运行时通过网格边界计算夹头间隙、台面位置和立边最低点。具体算法与设备编号见 `FACTORY_DEMO.md`。

## 打开项目

1. 用 Unity **2022.3.62f3c1** 打开本目录，再打开 `Assets/Scenes/SampleScene.unity`。
2. 确认 `Assets/Moudles/` 中有本机原始 FBX，尤其是总装配体、`加工零件.fbx`、`加工零件 冲压完毕.fbx` 和 `加工零件 冲压完毕1–5.fbx`。目录名 `Moudles` 是项目现有名称。
3. 等待 Unity 导入和脚本编译，进入 Play；用 WASD 移动，右键控制视角，靠近大控制台按 E。按 R 可随时重置。

**远程仓库不包含 `.fbx` 二进制文件**，只保留其 `.meta` 以维持 GUID。新电脑克隆后须将同名原始 FBX 放回 `Assets/Moudles/`，否则场景模型与工件引用无法完整恢复。`Library/`、`Temp/` 和本机生成的项目文件也不纳入版本控制。

## 修改场景时注意

- 用户已在 `SampleScene.unity` 手工对准两套冲床夹爪 `r1417/r1515`。普通打开场景、进入或退出 Play 不应重新摆位。**不要为了修复引用而执行 `Tools > Factory > Configure Loading Scene`**；这个显式命令会重新运行自动摆位。缺失的五个折弯分件引用由编辑器脚本单独补齐。
- `LoadingProcessController` 在 `Start()` 创建机械臂运行时关节层级。`r857`、`r863` 和四个吸盘必须作为同一腕部运动，成品在释放前保持附着。重置逻辑也需同步恢复这些节点。
- 原料和成品使用 `#3B424A`。按 R 后新原料板应水平回到原料台。
- 完成状态 `ReadyForPunching` 表示本轮演示结束、机械臂停在导轨右端；再次按 E 会重置并重播**单件**流程，当前没有多件累积或下游焊接流程。

## 当前验证边界

`dotnet msbuild Demo1.sln -nologo -verbosity:quiet -clp:Summary` 已通过，0 个编译错误；仍有 2 个 Unity 程序集版本冲突警告。代码差异检查 `git diff --check` 通过。**最近的 H 转台放件改动尚未完成 Unity Play 目视验收**：优先检查四条立边是否朝下接触最上层台面、中心板是否高出台面、立边是否位于四组夹头间隙，以及机械臂和夹头是否穿模。更多验收点见 `PROJECT_PROGRESS.md`。
