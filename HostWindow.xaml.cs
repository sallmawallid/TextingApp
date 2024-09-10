using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Newtonsoft.Json;

namespace TextingApp
{
    public partial class HostWindow : Window
    {
        public TcpListener server = null;
        public TcpClient client;
        public NetworkStream stream;
        public string hostName;
        public UdpClient udpClient;
        public const int bufferSize = 1024;
        public byte[] Buffer = new byte[bufferSize];
        int PORT_NUM = 5000;
        bool IsConnected = false;
        private const int BroadcastInterval = 5000; 
        private bool shouldBroadcast = true; 


        public HostWindow()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            hostName = HostNameBox.Text;
            if (string.IsNullOrEmpty(hostName))
            {
                hostName = "Unknown";
                return;
            }
            WelcomeText.Text = $"Welcome, {hostName}";
            StartHosting();
            GetHostName.Visibility = Visibility.Hidden;
            StatusTextBlock.Visibility = Visibility.Visible;
        }

        private void StartHosting()
        {
            server = new TcpListener(IPAddress.Any, PORT_NUM);
            server.Start();
            server.BeginAcceptTcpClient(new AsyncCallback(OnClientConnected), null);

            StartBroadcasting();
        }

        private void StartBroadcasting()
        {
            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, 34534);
            string message = $"Host: {hostName}, Available at {GetLocalIPAddress()}";
            byte[] data = Encoding.UTF8.GetBytes(message);

            Task.Run(() =>
            {
                while (shouldBroadcast)
                {
                    udpClient.Send(data, data.Length, broadcastEndPoint);
                    Thread.Sleep(BroadcastInterval); // Wait for the interval before broadcasting again
                }
            });
        }

        private void OnClientConnected(IAsyncResult ar)
        {
            IsConnected = true;
            client = server.EndAcceptTcpClient(ar);
            stream = client.GetStream();

            // Stop broadcasting once a client is connected
            shouldBroadcast = false;

            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = "Client is connected";
                StatusTextBlock.Visibility = Visibility.Hidden;
                ChatPanel.Visibility = Visibility.Visible;
            });

            Task.Run(() => ReceiveMessages());
        }

        private string GetLocalIPAddress()
        {
            string hostIP = Dns.GetHostName();
            IPAddress[] addresses = Dns.GetHostAddresses(hostIP);

            foreach (IPAddress address in addresses)
            {
                if ((address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address)))
                {
                    return address.ToString();
                }
            }

            return "No valid IP address found.";
        }

        private void ReceiveMessages()
        {
            while (true)
            {
                byte[] buffer = new byte[client.ReceiveBufferSize];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Message message = JsonConvert.DeserializeObject<Message>(json);

                Dispatcher.Invoke(() =>
                {
                    ChatBox.AppendText($"{message.Sender} \n {message.Content} \n ({message.Timestamp})\n");
                });
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string Text = MessageBox.Text;
            if (string.IsNullOrEmpty(Text))
                return;

            Message message = new Message
            {
                Sender = hostName,
                Timestamp = DateTime.Now,
                Content = Text
            };

            Dispatcher.Invoke(() =>
            {
                ChatBox.AppendText($"{message.Sender} \n {message.Content} \n ({message.Timestamp})\n");
            });

            string json = JsonConvert.SerializeObject(message);
            byte[] data = Encoding.UTF8.GetBytes(json);

            stream.Write(data, 0, data.Length);
            MessageBox.Clear();
        }
    }
}
