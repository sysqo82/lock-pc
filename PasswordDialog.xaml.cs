using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PCLockScreen
{
    public partial class PasswordDialog : Window
    {
        private ConfigManager configManager;

        public PasswordDialog(ConfigManager configManager)
        {
            InitializeComponent();
            this.configManager = configManager;
            PasswordInput.Focus();
        }

        private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OK_Click(sender, e);
            }
        }

        private async void OK_Click(object sender, RoutedEventArgs e)
        {
            var password = PasswordInput.Password;

            if (string.IsNullOrEmpty(password))
            {
                ErrorMessage.Text = "Please enter a password.";
                return;
            }

            try
            {
                // Use the server account password for authentication. If no
                // runtime login exists yet, fall back to the persisted account
                // email in config.
                var config = configManager.LoadConfig();
                var email = config.AccountEmail;

                bool ok = false;
                try
                {
                    ok = await ServerSession.ValidateCurrentUserPasswordAsync(password, email);
                }
                catch
                {
                    ok = false;
                }

                if (!ok)
                {
                    try
                    {
                        if (configManager.TryGetAccountPassword(out var storedPwd) && !string.IsNullOrEmpty(storedPwd))
                        {
                            if (storedPwd == password)
                            {
                                ok = true;
                            }
                        }

                        if (!ok && configManager.ValidatePassword(password))
                        {
                            ok = true;
                        }
                    }
                    catch { ok = false; }
                }

                if (ok)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ErrorMessage.Text = "Incorrect password. Access denied.";
                    PasswordInput.Clear();
                    PasswordInput.Focus();
                }
            }
            catch (System.Exception ex)
            {
                ErrorMessage.Text = "Error contacting server: " + ex.Message;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
