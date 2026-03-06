using Terminal.Gui;

namespace Morty.TUI;

public class MortyApp
{
    private readonly Window _mainWindow;
    private readonly ListView _messageList;
    private readonly TextField _inputField;
    private readonly Label _statusLabel;

    public MortyApp()
    {
        Application.Init();

        _mainWindow = new Window("morty - AI Coding Assistant")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1
        };

        _messageList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2
        };

        _inputField = new TextField("")
        {
            X = 0,
            Y = Pos.Bottom(_messageList),
            Width = Dim.Fill(),
            Height = 1
        };
        _inputField.KeyPress += OnInputKeyPress;

        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = Pos.Bottom(_inputField),
            Width = Dim.Fill(),
            Height = 1
        };

        _mainWindow.Add(_messageList);
        _mainWindow.Add(_inputField);
        _mainWindow.Add(_statusLabel);

        Application.Top.Add(_mainWindow);
    }

    public void Run()
    {
        Application.Run();
        Application.Shutdown();
    }

    private void OnInputKeyPress(KeyEvent keyEvent)
    {
        if (keyEvent.Key == Key.Enter)
        {
            var text = _inputField.Text.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                AddMessage("You", text);
                _inputField.Text = "";
                OnUserSubmit?.Invoke(this, text);
            }
        }
    }

    public void AddMessage(string role, string content)
    {
        _messageList.SetSource(new[] { $"[{role}] {content}" });
    }

    public event EventHandler<string>? OnUserSubmit;
}
