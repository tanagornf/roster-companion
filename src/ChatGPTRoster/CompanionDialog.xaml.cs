using System.Windows;

namespace ChatGPTRoster;

public partial class CompanionDialog : Window
{
    private CompanionDialog(string heading, string message, string primaryText, bool showCancel, bool destructive)
    {
        InitializeComponent();
        HeadingText.Text = heading;
        MessageText.Text = message;
        ConfirmButton.Content = primaryText;
        ConfirmButton.Style = (Style)FindResource(destructive ? "DangerButtonStyle" : "AccentButtonStyle");
        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
    }

    public static bool Confirm(
        Window owner,
        string heading,
        string message,
        string primaryText = "Continue",
        bool destructive = false) =>
        new CompanionDialog(heading, message, primaryText, showCancel: true, destructive) { Owner = owner }.ShowDialog() == true;

    public static void Inform(Window owner, string heading, string message) =>
        new CompanionDialog(heading, message, "OK", showCancel: false, destructive: false) { Owner = owner }.ShowDialog();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
