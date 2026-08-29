# ForkPlus-Next — WPF → Avalonia 迁移

> 本仓库是 [ForkPlus](https://github.com/hebin123456/ForkPlus)（WPF 版）向 Avalonia 12 的迁移目标。
> 基线由 [WpfToAvalonia](https://github.com/hebin123456/WpfToAvalonia)（`wpf2ava`）自动转换生成。

## 基线信息

- 源版本：ForkPlus `v3.12.3`（commit `498b4ca`）
- 转换工具：wpf2ava @ `82c462c`（Avalonia 12.1.1 / net10.0）
- 转换报告：`docs-conv-report.md`（INFO 7348 / WARN 413 / TODO 844）

## 当前状态（持续更新）

| 阶段 | 状态 | 说明 |
|---|---|---|
| 0 基线导入 | 完成 | 全量转换产物入库 |
| 1 C# 编译清零 | 进行中 | 起始 186 错误 / 46 文件，见下表 |
| 2 XAML (AVLN) 清零 | 未开始 | 被 C# 错误阻断，量化后更新 |
| 3 附属工程收尾 | 未开始 | AskPass / RI / 测试工程 |

## 阶段 1 错误分组与策略

| 分组 | 错误数 | 策略 |
|---|---:|---|
| OxyPlot 图表库 | 48 | 换库或先降级统计页 |
| 路由事件委托族（RoutedEventHandler 等） | 30 | 签名批量替换 |
| Frame 导航（RequestNavigateEventArgs） | 24 | 改超链接/Process.Start |
| 长尾（SpellingError、AutomationPeer 等） | 24 | 逐处处理 |
| Adorner 体系 | 20 | 改 Canvas overlay |
| WinRT / Windows.* | 20 | 平台条件化 |
| 拖放 / 粘贴事件参数 | 14 | Avalonia DragDrop 签名 |
| 弱事件（IWeakEventListener） | 6 | 删除或普通订阅 |
| WindowsAPICodePack | 4 | StorageProvider 替换 |

## 迁移守则

- 小步提交：每修完一组错误即 commit + push，不攒大招
- 转换工具可同步增强（在 WpfToAvalonia 仓提交），修一处回填一处映射
- 模板触发器被注释化处（XAML-TEMPLATE-TRIGGER-ORPHAN ×28 等）在编译清零后按页面恢复
