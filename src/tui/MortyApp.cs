// =============================================================================
// Morty TUI 应用
// =============================================================================
// 使用 Terminal.Gui 实现的终端用户界面
// 提供消息列表、输入框和状态栏
// =============================================================================

using Terminal.Gui;

namespace Morty.TUI;

/// <summary>
/// Morty TUI 应用
/// </summary>
public class MortyApp
{
    /// <summary>
    /// 主窗口
    /// </summary>
    private readonly Window _mainWindow;

    /// <summary>
    /// 消息列表视图
    /// </summary>
    private readonly ListView _messageList;

    /// <summary>
    /// 输入框
    /// </summary>
    private readonly TextField _inputField;

    /// <summary>
    /// 状态栏
    /// </summary>
    private readonly Label _statusLabel;

    /// <summary>
    /// 初始化 TUI 应用
    /// </summary>
    public MortyApp()
    {
        // 初始化 Terminal.Gui
        Application.Init();

        // 创建主窗口
        _mainWindow = new Window("morty - AI Coding Assistant")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1
        };

        // 创建消息列表
        _messageList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2
        };

        // 创建输入框
        _inputField = new TextField("")
        {
            X = 0,
            Y = Pos.Bottom(_messageList),
            Width = Dim.Fill(),
            Height = 1
        };
        _inputField.KeyPress += OnInputKeyPress;

        // 创建状态栏
        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = Pos.Bottom(_inputField),
            Width = Dim.Fill(),
            Height = 1
        };

        // 添加到窗口
        _mainWindow.Add(_messageList);
        _mainWindow.Add(_inputField);
        _mainWindow.Add(_statusLabel);

        // 添加到应用
        Application.Top.Add(_mainWindow);
    }

    /// <summary>
    /// 运行应用
    /// </summary>
    public void Run()
    {
        Application.Run();
        Application.Shutdown();
    }

    /// <summary>
    /// 输入框按键事件处理
    /// </summary>
    private void OnInputKeyPress(KeyEvent keyEvent)
    {
        // 按回车发送消息
        if (keyEvent.Key == Key.Enter)
        {
            var text = _inputField.Text.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                // 添加用户消息
                AddMessage("You", text);
                
                // 清空输入框
                _inputField.Text = "";
                
                // 触发提交事件
                OnUserSubmit?.Invoke(this, text);
            }
        }
    }

    /// <summary>
    /// 添加消息到列表
    /// </summary>
    /// <param name="role">角色 (You/morty)</param>
    /// <param name="content">内容</param>
    public void AddMessage(string role, string content)
    {
        _messageList.SetSource(new[] { $"[{role}] {content}" });
    }

    /// <summary>
    /// 用户提交消息事件
    /// </summary>
    public event EventHandler<string>? OnUserSubmit;
}
