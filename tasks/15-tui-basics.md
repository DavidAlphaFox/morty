# 任务: 实现 TUI 界面基础

## 阶段
Phase 6: TUI 界面

## 描述
使用 Terminal.Gui 框架实现 TUI 界面基础架构，包括应用入口、主窗口、布局。

## 验收标准
- [ ] Terminal.Gui 正确初始化
- [ ] 主窗口显示
- [ ] 布局结构正确
- [ ] 程序可正常运行

## 实现步骤

### 15.1 创建 TUI 入口
创建 `src/tui/Program.cs`:
```csharp
using Terminal.Gui;

class Program
{
    static int Main(string[] args)
    {
        // 解析命令行参数
        var options = ParseArgs(args);
        
        // 加载配置
        var config = new ConfigLoader().Load();
        
        // 加载凭证
        var authManager = new AuthManager();
        var apiKey = authManager.GetApiKey(config.Provider?.Type ?? "zhipu");
        
        // 创建应用
        Application.Init();
        
        try
        {
            var top = Application.Top;
            
            // 创建主窗口
            var window = new MortyWindow(config, apiKey);
            top.Add(window);
            
            Application.Run();
            
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", ex.Message, "Ok");
            return 1;
        }
        finally
        {
            Application.Shutdown();
        }
    }
}
```

### 15.2 创建主窗口
创建 `src/tui/MortyWindow.cs`:
```csharp
public class MortyWindow : Window
{
    private readonly MenuBar _menuBar;
    private readonly MessageListView _messageList;
    private readonly InputView _inputView;
    private readonly StatusBarView _statusBar;
    
    public MortyWindow(MortyConfig config, string? apiKey)
    {
        Title = "morty - AI Coding Assistant";
        X = 0;
        Y = 1;
        Width = Dim.Fill();
        Height = Dim.Fill() - 1;
        
        // 菜单栏
        _menuBar = new MenuBar(new MenuBarItem[]
        {
            new("_File", new MenuItem[]
            {
                new("_New Session", "", () => NewSession()),
                new("_Resume Session", "", () => ResumeSession()),
                new("_Export", "", () => ExportSession()),
                new("_Quit", "", () => Application.RequestStop())
            }),
            new("_Model", new MenuItem[]
            {
                new("_Zhipu", "", () => SwitchModel("zhipu")),
                new("_MiniMax", "", () => SwitchModel("minimax")),
                new("_Qianwen", "", () => SwitchModel("qianwen"))
            }),
            new("_Tools", new MenuItem[]
            {
                new("_Settings", "", () => OpenSettings()),
                new("_MCP Servers", "", () => ManageMcp())
            })
        });
        
        // 消息列表
        _messageList = new MessageListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1
        };
        
        // 输入框
        _inputView = new InputView()
        {
            X = 0,
            Y = Pos.Bottom(_messageList),
            Width = Dim.Fill(),
            Height = 1
        };
        _inputView.OnSubmit += OnUserSubmit;
        
        // 状态栏
        _statusBar = new StatusBarView();
        
        Add(_menuBar);
        Add(_messageList);
        Add(_inputView);
        Add(_statusBar);
    }
    
    private void OnUserSubmit(object? sender, string text)
    {
        // 发送消息给 Agent
    }
}
```

### 15.3 设置布局
```csharp
// 使用 Terminal.Gui 的布局系统
_messageList.Width = Dim.Fill();
_messageList.Height = Dim.Fill() - 3;  // 留出输入框空间

_inputView.X = 0;
_inputView.Y = Pos.AnchorEnd() - 3;
_inputView.Width = Dim.Fill();
_inputView.Height = 3;

// 状态栏
_statusBar.X = 0;
_statusBar.Y = Pos.AnchorEnd();
_statusBar.Width = Dim.Fill();
_statusBar.Height = 1;
```

## 相关文件
- tasks/02-add-dependencies.md
- design/dotnet-coding-agent.md (5. TUI 设计)
