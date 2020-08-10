using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;

public class Client : MonoBehaviour
{
    public static Client instance;
    public static int dataBufferSize = 4096;

    public string ip = "127.0.0.1";
    public int port = 23399;
    public int clientID = 0;

    public TCP tcp;
    public UDP udp;

    private delegate void PacketHandler(Packet packet);
    private static Dictionary<int, PacketHandler> packetHandlers;

    private bool isConnected = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if(instance != this)
        {
            Debug.Log("Instance already exists! Destroying object!");
            Destroy(this);
        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void ConnectToServer()
    {
        tcp = new TCP();
        udp = new UDP();

        InitializeClientData();

        isConnected = true;
        tcp.Connect();
    }

    public class TCP
    {
        public TcpClient socket;

        private NetworkStream stream;
        private Packet receivedDataPacket;
        private byte[] receiveBuffer;

        public void Connect()
        {
            socket = new TcpClient
            {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };

            receiveBuffer = new byte[dataBufferSize];
            socket.BeginConnect(instance.ip, instance.port, ConnectCallback, socket);
        }

        private void ConnectCallback(IAsyncResult result)
        {
            socket.EndConnect(result);

            if(!socket.Connected)
            {
                return;
            }

            stream = socket.GetStream();

            receivedDataPacket = new Packet();

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveDataCallback, null);
        }

        public void SendData(Packet packet)
        {
            try
            {
                if(socket != null)
                {
                    stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
                }
            }
            catch(Exception e)
            {
                Debug.Log($"Error sending data to server via TCP: {e}");
            }
        }

        private void ReceiveDataCallback(IAsyncResult result)
        {
            try
            {
                int receivedDataByteLength = stream.EndRead(result);
                //If no data received, this means lost connection, so disconnect.
                if (receivedDataByteLength == 0)
                {
                    //Disconnect.
                    instance.Disconnect();
                    return;
                }

                byte[] receivedData = new byte[receivedDataByteLength];
                Array.Copy(receiveBuffer, receivedData, receivedDataByteLength);

                //Handle received data.
                receivedDataPacket.Reset(HandleData(receivedData));


                //Keep reading more data from network stream.
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveDataCallback, null);

            }
            catch (Exception e)
            {
                Debug.Log($"Error recieving TCP data from network stream: {e}");
                //Disconnect.
                Disconnect();
            }
        }

        private bool HandleData(byte[] data)
        {
            int packetLength = 0;

            receivedDataPacket.SetBytes(data);

            if(receivedDataPacket.UnreadLength() >= 4)
            {
                packetLength = receivedDataPacket.ReadInt();
                if(packetLength <= 0)
                {
                    return true;
                }
            }

            while(packetLength > 0 && packetLength <= receivedDataPacket.UnreadLength())
            {
                byte[] packetBytes = receivedDataPacket.ReadBytes(packetLength);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet packet = new Packet(packetBytes))
                    {
                        int packetId = packet.ReadInt();
                        packetHandlers[packetId](packet);
                    }
                });

                packetLength = 0;
                if (receivedDataPacket.UnreadLength() >= 4)
                {
                    packetLength = receivedDataPacket.ReadInt();
                    if (packetLength <= 0)
                    {
                        return true;
                    }
                }
            }

            if(packetLength <= 1)
            {
                return true;
            }

            return false;
        }

        private void Disconnect()
        {
            instance.Disconnect();
            stream = null;
            receivedDataPacket = null;
            receiveBuffer = null;
            socket = null;
        }
    }

    public class UDP
    {
        public UdpClient socket;
        public IPEndPoint endPoint;

        public UDP()
        {
            endPoint = new IPEndPoint(IPAddress.Parse(instance.ip), instance.port);
        }

        public void Connect(int localPort)
        {
            socket = new UdpClient(localPort);
            socket.Connect(endPoint);
            socket.BeginReceive(ReceiveCallback, null);

            using (Packet packet = new Packet())
            {
                SendData(packet);
            }
        }

        public void SendData(Packet packet)
        {
            try
            {
                packet.InsertInt(instance.clientID);
                if(socket != null)
                {
                    socket.BeginSend(packet.ToArray(), packet.Length(), null, null);
                }
            }
            catch (Exception e)
            {
                Debug.Log($"There was an issue sending data to server using UDP: {e}");
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                byte[] data = socket.EndReceive(result, ref endPoint);
                socket.BeginReceive(ReceiveCallback, null);

                if(data.Length < 4)
                {
                    //Disconnect.
                    instance.Disconnect();
                    return;
                }

                HandleData(data);
            }
            catch
            {
                //Disconnect.
                Disconnect();
            }
        }

        private void HandleData(byte[] data)
        {
            using (Packet packet = new Packet(data))
            {
                int packetLength = packet.ReadInt();
                data = packet.ReadBytes(packetLength);
            }

            ThreadManager.ExecuteOnMainThread(() =>
            {
                using (Packet packet = new Packet(data))
                {
                    int packetId = packet.ReadInt();
                    packetHandlers[packetId](packet);
                }
            });
        }

        private void Disconnect()
        {
            instance.Disconnect();

            endPoint = null;
            socket = null;
        }
    }

    private void InitializeClientData()
    {
        packetHandlers = new Dictionary<int, PacketHandler>()
        {
            {(int)ServerPackets.welcome, ClientHandle.Welcome },
            { (int)ServerPackets.udpTest, ClientHandle.UDPTest },
            { (int)ServerPackets.spawnPlayer, ClientHandle.SpawnPlayer},
            { (int)ServerPackets.playerPosition, ClientHandle.PlayerPosition},
            { (int)ServerPackets.playerRotation, ClientHandle.PlayerRotation}
        };
        Debug.Log("Initializing packet handlers...");
    }

    private void Disconnect()
    {
        if(isConnected)
        {
            isConnected = false;
            tcp.socket.Close();
            udp.socket.Close();

            Debug.Log("Disconnected from server.");
        }
    }
}
