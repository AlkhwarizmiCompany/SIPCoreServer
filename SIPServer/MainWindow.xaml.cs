using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using System.Net;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SIPServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Server server;

        public MainWindow()
        {

            InitializeComponent();

            server = new Server(AppendToLog);
        }
        private void AppendToLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                EventsInfoTextBox.AppendText(message + Environment.NewLine);
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            server.AnswerCall("thisis@xyz");

        }
    }
}