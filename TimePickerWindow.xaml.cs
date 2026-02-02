using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCLockScreen
{
    public partial class TimePickerWindow : Window
    {
        public string SelectedTime { get; private set; }
        private bool isInitializing = true;

        public TimePickerWindow(string initialTime = "00:00")
        {
            InitializeComponent();
            
            // Parse initial time
            if (!string.IsNullOrEmpty(initialTime) && initialTime.Contains(":"))
            {
                var parts = initialTime.Split(':');
                if (parts.Length == 2)
                {
                    HoursTextBox.Text = parts[0].PadLeft(2, '0');
                    MinutesTextBox.Text = parts[1].PadLeft(2, '0');
                }
            }
            
            isInitializing = false;
            
            // Focus on hours by default
            Loaded += (s, e) => HoursTextBox.Focus();
        }

        private void HoursBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            HoursTextBox.Focus();
            e.Handled = true;
        }

        private void MinutesBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MinutesTextBox.Focus();
            e.Handled = true;
        }

        private void HoursTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            HoursTextBox.Dispatcher.BeginInvoke(new Action(() => 
            {
                HoursTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void MinutesTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            MinutesTextBox.Dispatcher.BeginInvoke(new Action(() => 
            {
                MinutesTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void HoursTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            if (!char.IsDigit(e.Text[0]))
            {
                e.Handled = true;
                return;
            }

            var textBox = sender as TextBox;
            string currentText = textBox.Text;
            int caretIndex = textBox.CaretIndex;
            int selectionLength = textBox.SelectionLength;
            
            // Build what the text would be
            string newText = currentText;
            if (selectionLength > 0)
            {
                newText = currentText.Remove(caretIndex, selectionLength);
            }
            newText = newText.Insert(caretIndex, e.Text);
            
            // Remove leading zeros for validation
            newText = newText.TrimStart('0');
            if (string.IsNullOrEmpty(newText))
                newText = "0";
            
            if (int.TryParse(newText, out int hours))
            {
                if (hours > 23)
                {
                    e.Handled = true;
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        private void MinutesTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            if (!char.IsDigit(e.Text[0]))
            {
                e.Handled = true;
                return;
            }

            var textBox = sender as TextBox;
            string currentText = textBox.Text;
            int caretIndex = textBox.CaretIndex;
            int selectionLength = textBox.SelectionLength;
            
            // Build what the text would be
            string newText = currentText;
            if (selectionLength > 0)
            {
                newText = currentText.Remove(caretIndex, selectionLength);
            }
            newText = newText.Insert(caretIndex, e.Text);
            
            // Remove leading zeros for validation
            newText = newText.TrimStart('0');
            if (string.IsNullOrEmpty(newText))
                newText = "0";
            
            if (int.TryParse(newText, out int minutes))
            {
                if (minutes > 59)
                {
                    e.Handled = true;
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        private void HoursTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Remove non-digits
            string text = new string(Array.FindAll(textBox.Text.ToCharArray(), char.IsDigit));
            
            if (string.IsNullOrEmpty(text))
            {
                textBox.TextChanged -= HoursTextBox_TextChanged;
                textBox.Text = "00";
                textBox.CaretIndex = 0;
                textBox.TextChanged += HoursTextBox_TextChanged;
                return;
            }

            // Parse and validate
            int hours = int.Parse(text);
            if (hours > 23)
            {
                hours = 23;
                text = hours.ToString();
            }

            // Only format if we have 2 digits or if focus is lost
            if (text.Length >= 2)
            {
                string formattedHours = hours.ToString("D2");
                
                if (textBox.Text != formattedHours)
                {
                    textBox.TextChanged -= HoursTextBox_TextChanged;
                    textBox.Text = formattedHours;
                    textBox.CaretIndex = formattedHours.Length;
                    textBox.TextChanged += HoursTextBox_TextChanged;
                }

                // Auto-advance to minutes when 2 digits entered
                MinutesTextBox.Focus();
            }
            else
            {
                // Just update text without padding
                if (textBox.Text != text)
                {
                    textBox.TextChanged -= HoursTextBox_TextChanged;
                    textBox.Text = text;
                    textBox.CaretIndex = text.Length;
                    textBox.TextChanged += HoursTextBox_TextChanged;
                }
            }
        }

        private void MinutesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Remove non-digits
            string text = new string(Array.FindAll(textBox.Text.ToCharArray(), char.IsDigit));
            
            if (string.IsNullOrEmpty(text))
            {
                textBox.TextChanged -= MinutesTextBox_TextChanged;
                textBox.Text = "00";
                textBox.CaretIndex = 0;
                textBox.TextChanged += MinutesTextBox_TextChanged;
                return;
            }

            // Parse and validate
            int minutes = int.Parse(text);
            if (minutes > 59)
            {
                minutes = 59;
                text = minutes.ToString();
            }

            // Only format if we have 2 digits
            if (text.Length >= 2)
            {
                string formattedMinutes = minutes.ToString("D2");
                
                if (textBox.Text != formattedMinutes)
                {
                    textBox.TextChanged -= MinutesTextBox_TextChanged;
                    textBox.Text = formattedMinutes;
                    textBox.CaretIndex = formattedMinutes.Length;
                    textBox.TextChanged += MinutesTextBox_TextChanged;
                }
            }
            else
            {
                // Just update text without padding
                if (textBox.Text != text)
                {
                    textBox.TextChanged -= MinutesTextBox_TextChanged;
                    textBox.Text = text;
                    textBox.CaretIndex = text.Length;
                    textBox.TextChanged += MinutesTextBox_TextChanged;
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Ensure both are properly formatted
            string hours = HoursTextBox.Text.PadLeft(2, '0');
            string minutes = MinutesTextBox.Text.PadLeft(2, '0');
            
            SelectedTime = $"{hours}:{minutes}";
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
