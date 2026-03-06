# 任务: 实现 TUI 视图组件

## 阶段
Phase 6: TUI 界面

## 描述
实现 TUI 视图组件：MessageListView、InputView、StatusBarView。

## 验收标准
- [ ] 消息列表正确显示
- [ ] 输入框可输入文本
- [ ] 状态栏显示信息
- [ ] 支持滚动

## 实现步骤

### 16.1 创建消息列表视图
创建 `src/tui/Views/MessageListView.cs`:
```csharp
public class MessageListView : View
{
    private readonly List<MessageItem> _messages = new();
    private int _scrollPosition;
    
    public MessageListView()
    {
        CanFocus = false;
    }
    
    public void AddMessage(MessageItem message)
    {
        _messages.Add(message);
        SetNeedsDisplay();
    }
    
    public void Clear()
    {
        _messages.Clear();
        _scrollPosition = 0;
        SetNeedsDisplay();
    }
    
    public override void Redraw(NormalizeCollection<View> bounds)
    {
        Driver.SetAttribute(ColorScheme.Normal);
        
        var y = 0;
        foreach (var msg in _messages.Skip(_scrollPosition))
        {
            if (y >= bounds.Height)
                break;
            
            DrawMessage(msg, y);
            y += GetMessageHeight(msg);
        }
        
        // 填充空白区域
        while (y < bounds.Height)
        {
            Move(0, y++);
            Driver.DrawString(new string(' ', Bounds.Width));
        }
    }
    
    private void DrawMessage(MessageItem msg, int y)
    {
        var color = msg.Role == "user" 
            ? ColorScheme.Focus 
            : ColorScheme.Normal;
        
        Driver.SetAttribute(color);
        
        // 绘制角色标签
        var label = msg.Role == "user" ? "[You]" : "[morty]";
        Move(0, y);
        Driver.DrawString(label);
        
        // 绘制内容
        var content = msg.Content.Split('\n').First();
        Move(3, y++);
        Driver.DrawString(content.Length > Bounds.Width - 3 
            ? content[..(Bounds.Width - 6)] + "..." 
            : content);
        
        // 多行内容
        foreach (var line in msg.Content.Split('\n').Skip(1))
        {
            Move(0, y++);
            Driver.DrawString(line.Length > Bounds.Width 
                ? line[..(Bounds.Width - 3)] + "..." 
                : line);
        }
    }
}

public class MessageItem
{
    public string Role { get; set; } = "";  // user, assistant, tool
    public string Content { get; set; } = "";
    public string? ToolName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
```

### 16.2 创建输入视图
创建 `src/tui/Views/InputView.cs`:
```csharp
public class InputView : View
{
    private readonly TextField _textField;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    
    public event EventHandler<string>? OnSubmit;
    public event EventHandler? OnCancel;
    
    public string Text => _textField.Text.ToString();
    
    public InputView()
    {
        _textField = new TextField("")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = true
        };
        
        _textField.KeyPress += OnKeyPress;
        
        Add(_textField);
        SetFocus(_textField);
    }
    
    private bool OnKeyPress(KeyEvent keyEvent)
    {
        if (keyEvent.Key == Key.Enter)
        {
            var text = _textField.Text.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                _history.Add(text);
                _historyIndex = _history.Count;
                OnSubmit?.Invoke(this, text);
                _textField.Text = "";
            }
            return true;
        }
        
        if (keyEvent.Key == Key.CtrlC)
        {
            OnCancel?.Invoke(this, EventArgs.Empty);
            return true;
        }
        
        // 历史导航
        if (keyEvent.Key == Key.CursorUp)
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                _textField.Text = _history[_historyIndex];
            }
            return true;
        }
        
        if (keyEvent.Key == Key.CursorDown)
        {
            if (_historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                _textField.Text = _history[_historyIndex];
            }
            return true;
        }
        
        return false;
    }
}
```

### 16.3 创建状态栏视图
创建 `src/tui/Views/StatusBarView.cs`:
```csharp
public class StatusBarView : View
{
    public string WorkingDirectory { get; set; } = "";
    public string SessionName { get; set; } = "";
    public string Model { get; set; } = "";
    public int TokenUsage { get; set; }
    public decimal Cost { get; set; }
    
    public StatusBarView()
    {
        Height = 1;
        CanFocus = false;
    }
    
    public override void Redraw(NormalizeCollection<View> bounds)
    {
        Driver.SetAttribute(ColorScheme.Normal);
        
        var text = $" {Model} | {TruncatePath(WorkingDirectory)} | " +
                   $"Session: {SessionName} | " +
                   $"Tokens: {TokenUsage} | ${Cost:F4} ";
        
        Move(0, 0);
        Driver.DrawString(text.Length > bounds.Width 
            ? text[..(bounds.Width - 3)] + "..." 
            : text);
        
        // 填充剩余空间
        var remaining = bounds.Width - text.Length;
        if (remaining > 0)
        {
            Move(text.Length, 0);
            Driver.DrawString(new string(' ', remaining));
        }
    }
    
    private string TruncatePath(string path)
    {
        if (path.Length <= 30)
            return path;
        
        var home = Environment.GetFolderPath(Environment.SpecialFolder.Home);
        if (path.StartsWith(home))
            return "~" + path[home.Length..];
        
        return "..." + path[(path.Length - 27)..];
    }
}
```

## 相关文件
- tasks/15-tui-basics.md
- design/dotnet-coding-agent.md (5.2 组件设计)
