using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace PCLockScreen
{
    /// <summary>
    /// Singleton that manages application language and exposes localized strings
    /// and FlowDirection to XAML via data binding.
    /// Usage in XAML:
    ///   Text="{Binding Source={x:Static local:Loc.Instance}, Path=Strings.Main_Title}"
    ///   FlowDirection="{Binding Source={x:Static local:Loc.Instance}, Path=FlowDirection}"
    /// </summary>
    public class Loc : INotifyPropertyChanged
    {
        public static readonly Loc Instance = new Loc();

        private Strings _strings = new Strings();
        private FlowDirection _flowDirection = FlowDirection.LeftToRight;

        private Loc() { }

        public Strings Strings
        {
            get => _strings;
            private set { _strings = value; OnPropertyChanged(nameof(Strings)); }
        }

        public FlowDirection FlowDirection
        {
            get => _flowDirection;
            private set { _flowDirection = value; OnPropertyChanged(nameof(FlowDirection)); }
        }

        /// <summary>
        /// Switch the application language at runtime. Supported: "en", "he".
        /// </summary>
        public void SetLanguage(string cultureCode)
        {
            var culture = new CultureInfo(cultureCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            Strings.Culture = culture;
            // Trigger re-read of all bound string properties
            Strings = new Strings { Culture = culture };

            FlowDirection = cultureCode.StartsWith("he", StringComparison.OrdinalIgnoreCase)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
