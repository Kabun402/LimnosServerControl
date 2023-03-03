using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LimnosServerControl.RCon.Events;
using LimnosServerControl.RCon.Helper;

namespace LimnosServerControl.RCon
{
    enum RConPacketType
    {
        Login,
        Command,
        Acknowledge
    }

    public class RConClient
    {
        private Socket _socket;
        private DateTime _packetSent;
        private DateTime _packetReceived;
        private int _sequenceNumber;
        private int _currentPacket;
        private SortedDictionary<int, string[]> _packetQueue;
        private RConLoginCredentials _loginCredentials;

        public bool Connected { get { return _socket != null && _socket.Connected; } }

        public int QueueLength { get { return _packetQueue.Count; } }

        public RConClient(RConLoginCredentials loginCredentials)
        {
            _loginCredentials = loginCredentials;
        }

        public bool Connect()
        {
            _packetSent = DateTime.Now;
            _packetReceived = DateTime.Now;

            _sequenceNumber = 0;
            _currentPacket = -1;
            _packetQueue = new SortedDictionary<int, string[]>();

            var remoteEndpoint = new IPEndPoint(_loginCredentials.Host, _loginCredentials.Port);
            _socket = new Socket(_loginCredentials.Host.AddressFamily, SocketType.Dgram, ProtocolType.Udp)
            {
                ReceiveBufferSize = int.MaxValue,
                ReceiveTimeout = 5000
            };

            try
            {
                _socket.Connect(remoteEndpoint);

                if (!SendLoginPacket())
                    return false;

                var bytesReceived = new byte[4096];

                _socket.Receive(bytesReceived, bytesReceived.Length, 0);

                if (bytesReceived[7] == 0x00)
                {
                    if (bytesReceived[8] == 0x01)
                    {
                        _ = ReceiveAsync();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool SendLoginPacket()
        {
            try
            {
                if (!_socket.Connected)
                    return false;

                byte[] packet = ConstructPacket(RConPacketType.Login, 0, _loginCredentials.Password);
                _socket.Send(packet);

                _packetSent = DateTime.Now;
            }
            catch
            {
                return false;
            }

            return true;
        }

        private bool SendAcknowledgePacket(string command)
        {
            try
            {
                if (!_socket.Connected)
                    return false;

                byte[] packet = ConstructPacket(RConPacketType.Acknowledge, 0, command);
                _socket.Send(packet);

                _packetSent = DateTime.Now;
            }
            catch
            {
                return false;
            }

            return true;
        }

        public int SendCommand(string command, bool log = true)
        {
            return SendCommandPacket(command, log);
        }

        private int SendCommandPacket(string command, bool log = true)
        {
            int packetID = _sequenceNumber;
            _sequenceNumber = (_sequenceNumber == 255) ? 0 : _sequenceNumber + 1;

            try
            {
                if (_socket is null || !_socket.Connected)
                    return 256;

                var packet = ConstructPacket(RConPacketType.Command, packetID, command);

                _packetSent = DateTime.Now;

                if (log)
                {
                    _packetQueue.Add(packetID, new[] { command, _packetSent.ToString(CultureInfo.InvariantCulture) });
                }
                else
                {
                    _socket.Send(packet);
                }
            }
            catch
            {
                return 256;
            }

            return packetID;
        }

        private byte[] ConstructPacket(RConPacketType packetType, int sequenceNumber, string command)
        {
            string type;

            switch (packetType)
            {
                case RConPacketType.Login:
                    type = RConHelper.Hex2Ascii("FF00");
                    break;
                case RConPacketType.Command:
                    type = RConHelper.Hex2Ascii("FF01");
                    break;
                case RConPacketType.Acknowledge:
                    type = RConHelper.Hex2Ascii("FF02");
                    break;
                default:
                    return new byte[] { };
            }

            if (packetType != RConPacketType.Acknowledge)
            {
                if (command != null) command = Encoding.GetEncoding(1252).GetString(Encoding.UTF8.GetBytes(command));
            }

            var count = RConHelper.Bytes2String(new[] { (byte)sequenceNumber });

            var byteArray = new CRC32().ComputeHash(RConHelper.String2Bytes(type + ((packetType != RConPacketType.Command) ? "" : count) + command));

            var hash = new string(RConHelper.Hex2Ascii(BitConverter.ToString(byteArray).Replace("-", "")).ToCharArray().Reverse().ToArray());

            var packet = "BE" + hash + type + ((packetType != RConPacketType.Command) ? "" : count) + command;

            return RConHelper.String2Bytes(packet);
        }

        public void Disconnect()
        {
            if (_socket != null && _socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
        }

        private async Task ReceiveAsync()
        {
            var state = new StateObject { WorkSocket = _socket };

            _socket.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);

            while (_socket.Connected)
            {
                int timeoutClient = (int)(DateTime.Now - _packetSent).TotalSeconds;
                int timeoutServer = (int)(DateTime.Now - _packetReceived).TotalSeconds;

                if (timeoutClient >= 5)
                {
                    if (timeoutServer >= 20)
                    {
                        Disconnect();
                    }
                    else
                    {
                        if (_packetQueue.Count == 0)
                        {
                            SendCommandPacket(null, false);
                        }
                    }
                }

                if (_socket.Connected && _packetQueue.Count > 0 && _socket.Available == 0)
                {
                    try
                    {
                        int key = _packetQueue.First().Key;

                        if (_currentPacket == -1 || !_packetQueue.ContainsKey(_currentPacket))
                        {
                            _currentPacket = key;
                            string value = _packetQueue[key][0];
                            _socket.Send(ConstructPacket(RConPacketType.Command, key, value));
                        }
                    }
                    catch
                    { }
                }

                await Task.Delay(250);
            }

            if (!_socket.Connected)
            {
                OnDisconnect(_loginCredentials);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.WorkSocket;

                if (!client.Connected)
                {
                    return;
                }

                int bytesRead = client.EndReceive(ar);

                if (state.Buffer[7] == 0x02)
                {
                    SendAcknowledgePacket(RConHelper.Bytes2String(new[] { state.Buffer[8] }));
                    OnBattlEyeMessage(RConHelper.Bytes2String(state.Buffer, 9, bytesRead - 9), 256);
                }
                else if (state.Buffer[7] == 0x01)
                {
                    if (bytesRead > 9)
                    {
                        if (state.Buffer[7] == 0x01 && state.Buffer[9] == 0x00)
                        {
                            if (state.Buffer[11] == 0)
                            {
                                state.PacketsTodo = state.Buffer[10];
                            }

                            if (state.PacketsTodo > 0)
                            {
                                state.Message.Append(RConHelper.Bytes2String(state.Buffer, 12, bytesRead - 12));
                                state.PacketsTodo--;
                            }

                            if (state.PacketsTodo == 0)
                            {
                                OnBattlEyeMessage(state.Message.ToString(), state.Buffer[8]);
                                state.Message = new StringBuilder();
                                state.PacketsTodo = 0;
                            }
                        }
                        else
                        {
                            state.Message = new StringBuilder();
                            state.PacketsTodo = 0;

                            OnBattlEyeMessage(RConHelper.Bytes2String(state.Buffer, 9, bytesRead - 9), state.Buffer[8]);
                        }
                    }

                    if (_packetQueue.ContainsKey(state.Buffer[8]) && state.PacketsTodo == 0)
                    {
                        _packetQueue.Remove(state.Buffer[8]);
                    }
                }

                _packetReceived = DateTime.Now;

                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
            }
            catch
            { }
        }

        private void OnBattlEyeMessage(string message, int id)
        {
            RConMessageReceived?.Invoke(new RConMessageEventArgs(message, id));
        }

        private void OnDisconnect(RConLoginCredentials loginDetails)
        {
            RConDisconnected?.Invoke(new RConDisconnectEventArgs(loginDetails));
        }

        public event RConMessageEventHandler RConMessageReceived;
        public event RConDisconnectEventHandler RConDisconnected;
    }

    class StateObject
    {
        public Socket WorkSocket;
        public const int BufferSize = 2048;
        public byte[] Buffer = new byte[BufferSize];
        public StringBuilder Message = new StringBuilder();
        public int PacketsTodo;
    }
}
