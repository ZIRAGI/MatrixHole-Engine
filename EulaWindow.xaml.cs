using System.Windows;
using System.Windows.Input;

namespace MatrixHole
{
    public partial class EulaWindow : Window
    {
        public bool Accepted { get; private set; }

        public EulaWindow()
        {
            InitializeComponent();
            EulaTextBlock.Text = Core.EulaManager.GetEulaText();
            AcceptCheckBox.Checked += (s, e) => AcceptBtn.IsEnabled = true;
            AcceptCheckBox.Unchecked += (s, e) => AcceptBtn.IsEnabled = false;
        }

        private void AcceptBtn_Click(object sender, RoutedEventArgs e)
        {
            Accepted = true;
            Close();
        }

        private void DeclineBtn_Click(object sender, RoutedEventArgs e)
        {
            Accepted = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;
            DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Accepted = false;
            Close();
        }
    }
}
