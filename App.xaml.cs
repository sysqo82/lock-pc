using System.Windows;

namespace PCLockScreen
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Ensure only one instance is running
            bool createdNew;
            var mutex = new System.Threading.Mutex(true, "PCLockScreen_SingleInstance", out createdNew);
            
            if (!createdNew)
            {
                MessageBox.Show("PC Lock Screen is already running.", "Already Running", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
        }
    }
}
