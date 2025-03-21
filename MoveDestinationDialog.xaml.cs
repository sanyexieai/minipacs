using System.Windows;

namespace minipacs
{
    public partial class MoveDestinationDialog : Window
    {
        public int Port { get; private set; }
        public string DestinationHost { get; private set; }
        public int DestinationPort { get; private set; }

        public MoveDestinationDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PortTextBox.Text, out int port))
            {
                MessageBox.Show("请输入有效的监听端口号", "错误");
                return;
            }

            if (string.IsNullOrWhiteSpace(HostTextBox.Text))
            {
                MessageBox.Show("请输入目标主机", "错误");
                return;
            }

            if (!int.TryParse(DestPortTextBox.Text, out int destPort))
            {
                MessageBox.Show("请输入有效的目标端口号", "错误");
                return;
            }

            Port = port;
            DestinationHost = HostTextBox.Text;
            DestinationPort = destPort;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 