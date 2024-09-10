using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TextingApp
{
    public partial class ClientWindow : Window
    {
        private TcpClient client;
        private NetworkStream stream;
        private UdpClient udpClient;
        private const int BroadcastPort = 34534;
        private string clientName;


        public ClientWindow()
        {
            InitializeComponent();
            StartListeningForBroadcasts();
        }

        private void StartListeningForBroadcasts()
        {
            try
            {
                udpClient = new UdpClient(BroadcastPort);
                Task.Run(() =>
                {
                    while (true)
                    {
                        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, BroadcastPort);
                        byte[] data = udpClient.Receive(ref remoteEndPoint);
                        string message = Encoding.UTF8.GetString(data);

                        // Check if the received message does not already exist in the HostListBox
                        bool messageExists = false;

                        // Using Dispatcher to access the UI element from a non-UI thread
                        Dispatcher.Invoke(() =>
                        {
                            foreach (var item in HostListBox.Items)
                            {
                                if (item.ToString() == message)
                                {
                                    messageExists = true;
                                    break;
                                }
                            }

                            // If the message doesn't exist, add it to the HostListBox
                            if (!messageExists)
                            {
                                HostListBox.Items.Add(message);
                            }
                        });
                    }
                });
            }
            catch
            {

            }
        }

        private void HostListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DisplayHosts.Visibility = Visibility.Hidden;
            GetClientName.Visibility = Visibility.Visible;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            clientName = ClientNameBox.Text;
            if (string.IsNullOrEmpty(clientName))
                return;

            string selectedHost = HostListBox.SelectedItem.ToString();
            string hostAddress = selectedHost.Split(new string[] { "Available at " }, StringSplitOptions.None)[1].Trim();

            client = new TcpClient();
            client.Connect(hostAddress, 5000);
            stream = client.GetStream();

            WelcomeText.Text = $"Welcome, {clientName}";
            GetClientName.Visibility = Visibility.Hidden;
            ChatPanel.Visibility = Visibility.Visible;

            Task.Run(() => ReceiveMessages());
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
                Sender = clientName,
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
