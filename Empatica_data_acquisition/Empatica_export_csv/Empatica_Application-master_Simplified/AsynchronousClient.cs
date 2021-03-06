using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using CsvHelper;
using System.Collections.Generic;
using System.Timers;
using System.Globalization;
using System.Diagnostics;

namespace EmpaticaBLEClient
{
    public static class AsynchronousClient
    {
        // Global variable of filename.
        private static string time_filename = DateTime.Now.ToString("_yyyy-MM-dd_THH_mm_sszzz").Replace(':', '_');
        private static string filename = time_filename + ".csv";    // Just initial filename because it need to be declare. But it will change later anyway.


        // The port number for the remote device.
        private const string ServerAddress = "127.0.0.1";
        private const int ServerPort = 27555;

        // ManualResetEvent instances signal completion.
        private static readonly ManualResetEvent ConnectDone = new ManualResetEvent(false);
        private static readonly ManualResetEvent SendDone = new ManualResetEvent(false);
        private static readonly ManualResetEvent ReceiveDone = new ManualResetEvent(false);

        // The response from the remote device.
        private static String _response = String.Empty;
        private static StreamWriter tw_bvp = new StreamWriter("Bvp" + filename, append: true, Encoding.UTF8, 65535);
        private static string response_for_write_bvp = "";

        public static void StartClient()
        {
            Console.WriteLine(DateTime.Now.ToString("_yyyy-MM-dd_THH_mm_sszzz").Replace(':', '_'));
            Console.WriteLine(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
            WritingFileHeader();

            // Create the command string for taking sensor input.
            string[] subscribe_str = { "acc", "bvp", "gsr", "ibi", "tmp", "bat", "tag" };
            // Connect to a remote device.
            try
            {
                // Establish the remote endpoint for the socket.
                var ipHostInfo = new IPHostEntry { AddressList = new[] { IPAddress.Parse(ServerAddress) } };
                var ipAddress = ipHostInfo.AddressList[0];
                var remoteEp = new IPEndPoint(ipAddress, ServerPort);

                // Create a TSCP/IP socket.
                var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.
                client.BeginConnect(remoteEp, (ConnectCallback), client);
                ConnectDone.WaitOne();

                //Auto Connect to the E4 device
                Console.Write("Auto_Connection...");
                var auto_connect_msg = "device_connect 0C2C64";
                Send(client, auto_connect_msg + Environment.NewLine);
                SendDone.WaitOne();
                Receive(client);
                ReceiveDone.WaitOne();
                Console.WriteLine("Done!");

                // Infinite loop to take user's commands.
                while (true)
                {
                    //Console.Write("Enter your command : ");
                    var msg = Console.ReadLine();
                    if (msg == "subscribe_all")
                    {
                        for(int i=0; i<subscribe_str.Length; i++)
                        {
                            var subscribe_all_msg = "device_subscribe " + subscribe_str[i] + " ON";

                            Console.WriteLine(subscribe_all_msg);
                            Send(client, subscribe_all_msg + Environment.NewLine);  //Environment.NewLine ==> " /r/n" is newline and move cursor to the beginning of the line.
                            SendDone.WaitOne();
                            Receive(client);
                            ReceiveDone.WaitOne();
                            Thread.Sleep(300);  //Delay 100ms to protect the response message use as command message.
                            msg = "";
                        }
                    }
                    else if (msg == "unsubscribe_all")
                    {
                        for (int i = 0; i < subscribe_str.Length; i++)
                        {
                            var unsubscribe_all_msg = "device_subscribe " + subscribe_str[i] + " OFF";
                            Console.WriteLine(unsubscribe_all_msg);
                            Send(client, unsubscribe_all_msg + Environment.NewLine);  //Environment.NewLine ==> " /r/n" is newline and move cursor to the beginning of the line.
                            SendDone.WaitOne();
                            Receive(client);
                            ReceiveDone.WaitOne();
                            Thread.Sleep(300);  //Delay 100ms to protect the response message use as command message.
                        }
                    }
                    else if (msg == "device_stop")
                    {
                        Send(client, "device_disconnect" + Environment.NewLine);
                        Console.WriteLine("Write and End...");
                        
                        //Stop and writing all file
                        Console.WriteLine("Write Acc...");

                        Console.WriteLine("Write Bvp...");

                        Console.WriteLine("Write Gsr...");

                        Console.WriteLine("Write Hr...");

                        Console.WriteLine("Write Ibi...");

                        Console.WriteLine("Write Tag...");

                        Console.WriteLine("Write Temp...");

                        Console.WriteLine("Write Battery...");


                        Console.WriteLine("Close all files");
                        Console.WriteLine("Exiting...");
                        Console.ReadKey();
                        System.Environment.Exit(1);
                    }
                    else
                    {
                        msg = Console.ReadLine();
                        Send(client, msg + Environment.NewLine);  //Environment.NewLine ==> "/r/n" is newline and move cursor to the beginning of the line.
                        SendDone.WaitOne();
                        Receive(client);
                        ReceiveDone.WaitOne();
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                var client = (Socket)ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint);

                // Signal that the connection has been made.
                ConnectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Receive(Socket client)
        {
            try
            {
                // Create the state object.
                var state = new StateObject { WorkSocket = client };

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                var state = (StateObject)ar.AsyncState;
                var client = state.WorkSocket;

                // Read data from the remote device.
                var bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    state.Sb.Append(Encoding.ASCII.GetString(state.Buffer, 0, bytesRead));
                    _response = state.Sb.ToString();

                    HandleResponseFromEmpaticaBLEServer(_response);

                    state.Sb.Clear();

                    ReceiveDone.Set();

                    // Get the rest of the data.
                    client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
                }
                else
                {
                    // All the data has arrived; put it in response.
                    if (state.Sb.Length > 1)
                    {
                        _response = state.Sb.ToString();
                    }
                    // Signal that all bytes have been received.
                    ReceiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        private static void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.
            client.BeginSend(byteData, 0, byteData.Length, 0, SendCallback, client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                var client = (Socket)ar.AsyncState;
                // Complete sending the data to the remote device.
                client.EndSend(ar);
                // Signal that all bytes have been sent.
                SendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Called everytime that have response from EmpaticaBLEServer   
        private static void HandleResponseFromEmpaticaBLEServer(string response)
        {

            Console.Write(response);
        }
    }
}

