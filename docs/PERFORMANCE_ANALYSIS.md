# 日志性能瓶颈分析

## 🔴 主要性能瓶颈

### 1. **RichTextBox.Lines.Length 频繁访问（最严重）**
**位置**: `ShowContent()` 方法第577行、`IsAtBottom()` 方法第508行

**问题**:
- `Lines.Length` 是一个计算属性，每次访问都会：
  - 调用 Windows API `EM_GETLINECOUNT`
  - 触发 RichTextBox 重新解析整个文本内容
  - 对于大量文本（几万字符），耗时可能达到 **50-200ms**
- 每次追加日志都调用，累积延迟严重

**代码示例**:
```csharp
// 第577行：每次都要重新解析整个文本
if (logTextBox.Lines.Length >= MaxDisplayLines)
{
    // 第580行：又一次访问 Lines.Length
    int removeCount = Math.Min(TrimBlockSize, logTextBox.Lines.Length);
}
```

### 2. **循环遍历 Text 属性查找换行符**
**位置**: `ShowContent()` 方法第587-594行

**问题**:
- `logTextBox.Text[i]` 每次访问都会触发 RichTextBox 的字符串构建
- 循环可能遍历数万字符，导致 UI 线程阻塞
- 对于 3000 行日志，可能包含 10-20 万字符

**代码示例**:
```csharp
// 第587行：O(n) 复杂度，n 可能是数万
for (int i = 0; i < logTextBox.TextLength && lineIndex < removeCount; i++)
{
    if (logTextBox.Text[i] == '\n')  // 每次访问 Text[i] 都触发字符串构建
    {
        lineIndex++;
        startIndex = i + 1;
    }
}
```

### 3. **GetCharIndexFromPosition 频繁调用**
**位置**: `IsAtBottom()` 方法第526行、`ShowContent()` 方法第653行

**问题**:
- 需要计算文本布局和字符位置映射
- 对于大量文本，每次调用耗时 **10-50ms**
- `IsAtBottom()` 每次追加日志都调用（第569行）

**代码示例**:
```csharp
// 第526行：需要计算整个文本的布局
int bottomCharIndex = logTextBox.GetCharIndexFromPosition(bottomPoint);

// 第653行：又一次计算布局
int anchorCharIndex = logTextBox.GetCharIndexFromPosition(topAnchorPoint);
```

### 4. **TextLength 频繁访问**
**位置**: 多处（636、647、709、512、554、556、587、599行等）

**问题**:
- 虽然比 `Lines.Length` 快，但频繁访问仍有开销
- 每次访问都需要 RichTextBox 重新计算

### 5. **SelectedText 删除操作**
**位置**: `ShowContent()` 方法第607行

**问题**:
- 删除大段文本时，RichTextBox 需要重新渲染和布局
- 对于删除 100 行（可能几千字符），耗时 **20-100ms**

## 📊 性能影响估算

假设日志频率为 **50 条/秒**：
- 每次追加：`IsAtBottom()` (10-50ms) + `Lines.Length` (50-200ms) + 其他操作 ≈ **100-300ms**
- 每秒总延迟：50 × 100ms = **5-15 秒延迟** → UI 完全卡死
- 当需要删除旧日志时（第577行触发），延迟可能达到 **500ms-2秒**

## 💡 优化方案

### 方案A：维护本地行计数器（推荐，最简单有效）
- **优点**: 零成本获取行数，性能提升 100-1000 倍
- **缺点**: 需要手动维护计数器
- **实施难度**: ⭐ 简单

### 方案B：批量处理 + 节流更新
- **优点**: 减少 UI 更新频率
- **缺点**: 日志显示略有延迟（50-100ms）
- **实施难度**: ⭐⭐ 中等

### 方案C：使用 Windows API 高效操作
- **优点**: 直接操作底层控件，避免 .NET 包装开销
- **缺点**: 代码复杂，跨平台不兼容
- **实施难度**: ⭐⭐⭐ 复杂

### 方案D：混合优化（方案A + B）
- **优点**: 最佳性能，适合高频率日志场景
- **缺点**: 实施较复杂
- **实施难度**: ⭐⭐ 中等

## 🎯 推荐方案：方案A（维护本地计数器）

**核心思路**:
1. 维护 `private int _currentLineCount = 0;` 计数器
2. 每次追加日志时 `_currentLineCount++`
3. 删除日志时 `_currentLineCount -= removeCount`
4. 用计数器替代所有 `Lines.Length` 访问
5. 优化行查找：使用 `Lines` 数组（只在需要删除时访问一次）

**预期性能提升**:
- `Lines.Length` 访问从 **50-200ms** → **0ms**
- 行数检查从 **100-300ms** → **<1ms**
- 总体性能提升 **100-1000 倍**

