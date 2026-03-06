# 任务 4.2: TUI 改进

## 阶段
Phase 4 — 体验优化

## 目标
改进终端交互体验：Markdown 渲染、工具执行进度、多行输入。

## 设计方案

### 1. Markdown 渲染

```csharp
// src/cli/MarkdownRenderer.cs

public static class MarkdownRenderer
{
    /// <summary>渲染 Markdown 到终端 (ANSI 颜色)</summary>
    public static void Render(string markdown)
    {
        // # 标题 → 粗体 + 颜色
        // ```code``` → 灰色背景
        // `inline` → 黄色
        // **bold** → 粗体
        // - list → 缩进 + 符号
    }
}
```

### 2. 工具执行进度

```csharp
// 工具执行时显示 spinner
case AgentEvent.ToolExecutionStartEvent tool:
    StartSpinner($"[{tool.ToolName}] ...");
    break;
case AgentEvent.ToolExecutionEndEvent:
    StopSpinner();
    break;
```

### 3. 多行输入

```csharp
// 支持 \ 续行和 """ 多行块
if (input.EndsWith("\\"))
{
    multiLineBuffer.AppendLine(input[..^1]);
    continue;  // 等待更多输入
}
```

### 4. 历史记录

```csharp
// 使用 ReadLine 库或自实现
// 上/下箭头翻阅历史
// Ctrl+R 反向搜索
```

## 实现步骤

1. [ ] 创建 `src/cli/MarkdownRenderer.cs` — 基础 Markdown 渲染
2. [ ] `Program.cs` — 用 MarkdownRenderer 替换直接输出
3. [ ] 实现 spinner 动画
4. [ ] 多行输入支持
5. [ ] 输入历史记录 (持久化到 ~/.morty/history)

## 验收标准
- [ ] 代码块有语法高亮 (至少颜色区分)
- [ ] 工具执行时有 spinner
- [ ] 支持多行输入
- [ ] 上下箭头翻阅历史

## 相关文件
- `src/cli/MarkdownRenderer.cs` — 新建
- `src/cli/Program.cs` — 改造输出
