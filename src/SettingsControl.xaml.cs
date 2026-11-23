using UserControl = System.Windows.Controls.UserControl;

namespace TimerAlarmPlugin;

public partial class SettingsControl : UserControl
{
    public SettingsControl(Settings settings)
    {
        InitializeComponent();
        DataContext = settings;
    }
}