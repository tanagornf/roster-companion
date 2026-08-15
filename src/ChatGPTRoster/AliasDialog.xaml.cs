using System.Windows;

namespace ChatGPTRoster;

public partial class AliasDialog : Window
{
    public AliasDialog(string currentAlias)
    {
        InitializeComponent();
        AliasTextBox.Text = currentAlias;
        Loaded += (_, _) =>
        {
            AliasTextBox.Focus();
            AliasTextBox.SelectAll();
        };
    }

    public string Alias => AliasTextBox.Text;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
