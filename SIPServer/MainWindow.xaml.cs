using Google.Cloud.Speech.V1;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json; 
using NAudio.Wave;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using System.Configuration;
using System.IO;
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


            var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();
            server = new Server(AppendToLog, configuration);

            // access it through this line 
            var someValue = configuration["GoogleAppCredentials"];


        }
        private void AppendToLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                EventsInfoTextBox.AppendText(message + Environment.NewLine);
            });
        }



        private void End_Call(object sender, RoutedEventArgs e)
        {
            server.EndCall("");

        }

        private void Answer_Call(object sender, RoutedEventArgs e)
        {
            server.AnswerCall("thisis@xyz");

        }

    }
}

