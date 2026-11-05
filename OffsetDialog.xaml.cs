using System.Globalization;
using System.Windows;

namespace AxisLimitationOnView
{
    public partial class OffsetDialog : Window
    {
        public double OffsetMm { get; private set; }

        public OffsetDialog(double initialOffsetMm)
        {
            InitializeComponent();
            OffsetMm = initialOffsetMm;
            OffsetTextBox.Text = initialOffsetMm.ToString("0.###", CultureInfo.CurrentCulture);

            Loaded += (s, e) =>
            {
                OffsetTextBox.Focus();
                OffsetTextBox.SelectAll();
            };
        }

        private void TrimButton_Click(object sender, RoutedEventArgs e)
        {
            string text = (OffsetTextBox.Text ?? string.Empty).Trim();

            double value;
            bool ok = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                      double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

            if (!ok || value < 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                MessageBox.Show(this, "Введите неотрицательное число (например, 200).",
                    "Некорректное значение", MessageBoxButton.OK, MessageBoxImage.Warning);
                OffsetTextBox.Focus();
                OffsetTextBox.SelectAll();
                return;
            }

            OffsetMm = value;
            DialogResult = true;
            Close();
        }
    }
}