# macOS 胶囊代码审阅

审阅文件：`src/DeepSeekBalanceWidget.Mac/MainWindow.axaml`、`MainWindow.axaml.cs`、`App.axaml.cs`。

## 结论

1. **PASS — 进度条 track + fill 与三色区间**
   - `MainWindow.axaml:110-113`：`<Grid ... Width="34" Height="5">` 内先放 `CapsuleTrackBrush` track，再放 `MiniOcFiveBarFill` fill；`116-119`、`122-125` 对 weekly/monthly 同样采用该结构。
   - `MainWindow.axaml.cs:497-505`：`Math.Clamp(..., 0, 100)`，并按 `value < 20` 红色、`value <= 70` 橙色、否则绿色设置 `SolidColorBrush`；文件中无 `ProgressBar` 控件。

2. **PASS — MiniChangeText 隐藏**
   - `MainWindow.axaml:84`：`<TextBlock x:Name="MiniChangeText" ... IsVisible="False" />`。
   - `MainWindow.axaml.cs:365-366` 虽更新文本，但没有重新设置 `IsVisible`，因此仍保持隐藏。

3. **PASS — macOS Pin 使用 NSWindowSetLevel P/Invoke**
   - `MainWindow.axaml.cs:30-34`：P/Invoke `libobjc.A.dylib` 的 `sel_registerName` 与 `objc_msgSend`。
   - `MainWindow.axaml.cs:316-327`：解析 `"setLevel:"` selector，并以 `NSFloatingWindowLevel`（3）/`NSNormalWindowLevel`（0）调用原生 NSWindow level；`305-314` 由置顶状态触发，`122-129` 在窗口打开后重施。

4. **PASS — OC block 无溢出且布局被包含**
   - `MainWindow.axaml:105-127`：`MiniOpenCodeBlock` 启用 `ClipToBounds="True"`，内部 Grid 列宽 `24,34,44,38`（合计 140），外层 Padding `7,4`、左 Margin `8`，位于固定 `MiniContentPanel` 第三列宽 170（`79`），静态尺寸可容纳。
   - 三个 bar 均由固定 34px track 包含 fill（`110-124`）；`MainWindow.axaml.cs:487-505` 将 fill 宽度 clamp 到 0–34px。

5. **PASS — GPT/OC block 间距合理**
   - `MainWindow.axaml:79-80`：GPT 位于宽 200 的第二列；`105`：OC 位于第三列宽 170，并设置 `Margin="8,0,0,0"`，形成明确的 8px 分隔。

6. **FAIL — 胶囊按钮存在拥挤/可读性风险**
   - `MainWindow.axaml:132-137`：按钮条固定宽度 206、`Spacing="6"`，按钮宽度为 `44+44+56+44`，加 3 个 6px 间距恰好等于 206，无剩余空间。
   - `MainWindow.axaml:14-18`：全局 Button Padding 为 `8,0`，使 44px 按钮的有效内容宽度仅约 28px；`贴边✓`、`置顶✓`（`133-134`）以及 3 字的 `最小化`（`135`）存在文字贴边或裁切风险。需扩大按钮或减少内边距/间距后才能稳妥满足“可读、不拥挤”。

## 额外检查：Activated handler

**PASS**：`App.axaml.cs:33-38` 获取 `IActivatableLifetime` 并订阅 `Activated`；`41-54` 对主窗口设置恢复保护、调用 `RestoreAndActivate()`，并在 `finally` 清除保护；`MainWindow.axaml.cs:652-657` 实际恢复并激活窗口。

本次仅新增本审阅报告，未修改任何业务代码。
