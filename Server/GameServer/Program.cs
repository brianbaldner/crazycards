using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Server f = new Server();
            f.Run(7777);
        }
    }
    class Server
    {
        public GameInfo GameInfo = null;
        public List<PlayerInfo> lobbyManager = new List<PlayerInfo>();
        public List<TcpClient> users = new List<TcpClient>();
        public async void Run(int port)
        {
            string ip = "127.0.0.1";
            var server = new TcpListener(IPAddress.Any, port);

            server.Start();
            Console.WriteLine("Server has started on {0}:{1}, Waiting for a connection...", ip, port);
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("A client connected.");
                users.Add(client);
                lobbyManager.Add(new PlayerInfo() { name = "", index = users.Count - 1, leader = lobbyManager.Count == 0, turn = false });
                Task.Run(() => ClientConnect(client, users.Count - 1));
            }
        }

        // https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
        public async void ClientConnect(TcpClient client, int num)
        {
            NetworkStream stream = client.GetStream();

            // enter to an infinite cycle to be able to handle every change in stream
            while (true)
            {
                while (!stream.DataAvailable) ;
                while (client.Available < 3) ; // match against "get"

                byte[] bytes = new byte[client.Available];
                stream.Read(bytes, 0, client.Available);
                string s = Encoding.UTF8.GetString(bytes);

                if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
                {
                    Console.WriteLine("=====Handshaking from client=====\n{0}", s);

                    // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                    // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                    // 3. Compute SHA-1 and Base64 hash of the new value
                    // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                    string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                    string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                    string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                    // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                    byte[] response = Encoding.UTF8.GetBytes(
                        "HTTP/1.1 101 Switching Protocols\r\n" +
                        "Connection: Upgrade\r\n" +
                        "Upgrade: websocket\r\n" +
                        "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                    stream.Write(response, 0, response.Length);
                }
                else
                {
                    bool fin = (bytes[0] & 0b10000000) != 0,
                        mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

                    int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                        msglen = bytes[1] - 128, // & 0111 1111
                        offset = 2;

                    if (msglen == 126)
                    {
                        // was ToUInt16(bytes, offset) but the result is incorrect
                        msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                        offset = 4;
                    }
                    else if (msglen == 127)
                    {
                        Console.WriteLine("TODO: msglen == 127, needs qword to store msglen");
                        // i don't really know the byte order, please edit this
                        // msglen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
                        // offset = 10;
                    }

                    if (msglen == 0)
                        break;
                    else if (mask)
                    {
                        byte[] decoded = new byte[msglen];
                        byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                        offset += 4;

                        for (int i = 0; i < msglen; ++i)
                            decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                        string text = Encoding.UTF8.GetString(decoded);
                        if(text == "�" || text == "|quit|")
                        {
                            break;
                        }
                        Message(text, num);

                    }
                    else
                        Console.WriteLine("mask bit not set");

                    Console.WriteLine();
                }
            }
            Console.WriteLine($"Client {num} has disconnected");
            users[num] = null;
            var leader = lobbyManager.Find(p => p.index == num).leader;
            lobbyManager.RemoveAt(lobbyManager.FindIndex(p => p.index == num));
            if(leader && lobbyManager.Count > 0)
            {
                lobbyManager[0].leader = true;

            }
            SendToAll(JsonConvert.SerializeObject(new { name = "lobbyInfo", data = lobbyManager }));
        }

        public void Message(string message, int index)
        {
            Console.WriteLine($"From client {index}: {message}");
            dynamic data = JsonConvert.DeserializeObject(message);
            switch ((string)data.name)
            {
                case "placeCard":
                    PlaceCard(index, data.send);
                    break;
                case "pullCard":
                    PullCard(index, data.send);
                    break;
                case "setName":
                    SetName(index, data.send);
                    break;
                case "startGame":
                    StartGame(index);
                    break;
                default:
                    return;
            }
        }

        public void StartGame(int index)
        {
            if (!lobbyManager.Find(e => e.index == index).leader)
                return;
            GameInfo info = new GameInfo();

            info.players = lobbyManager;

            List<Card> deck = Card.ShuffleDeck();

            foreach (PlayerInfo ply in info.players)
            {
                ply.deck = deck.GetRange(0, 7);
                deck.RemoveRange(0, 7);
            }
            info.topCard = deck[0];
            deck.RemoveAt(0);
            info.pile = deck;

            Random rnd = new Random();
            info.players[rnd.Next(info.players.Count)].turn = true;
            info.playstack = new List<Card>();
            GameInfo = info;
            SendToAll(JsonConvert.SerializeObject(new { name = "startGame", data = info }));
        }



        public void PlaceCard(int index, dynamic send)
        {
            int infoindex = GameInfo.players.FindIndex(i => index == i.index);
            if (!GameInfo.players[infoindex].turn)
                return;
            if (send.card.color != GameInfo.topCard.color && send.card.type != GameInfo.topCard.type)
                return;

            Card sentcard = GameInfo.players[infoindex].deck.Find(e => e.type == (int)send.card.type && e.color == (string)send.card.color);
            GameInfo.topCard = sentcard;
            GameInfo.players[infoindex].deck.Remove(sentcard);
            GameInfo.playstack.Add(sentcard);

            if(sentcard.type == 11)//Reverse
            {
                GameInfo.direction *= -1;
            }

            if(sentcard.type == 12 || sentcard.type == 14) //Draw 4
            {
                GameInfo.players[infoindex + GameInfo.direction >= GameInfo.players.Count ? infoindex + GameInfo.direction - GameInfo.players.Count : (infoindex + GameInfo.direction <= 0 ? infoindex + GameInfo.direction + GameInfo.players.Count : infoindex + GameInfo.direction)].deck.AddRange(GameInfo.pile.GetRange(0, 4));
                GameInfo.pile.RemoveRange(0, 4);
            }
            GameInfo.players[infoindex].turn = false;
            if(sentcard.type == 10 || sentcard.type == 12 || sentcard.type == 14)
            {
                GameInfo.players[infoindex + (GameInfo.direction * 2) >= GameInfo.players.Count ? infoindex + (GameInfo.direction * 2) - GameInfo.players.Count : (infoindex + (GameInfo.direction * 2) <= 0 ? infoindex + (GameInfo.direction * 2) + GameInfo.players.Count : infoindex + (GameInfo.direction * 2))].turn = true;
            }
            else
            {
                GameInfo.players[infoindex + GameInfo.direction >= GameInfo.players.Count ? infoindex + GameInfo.direction - GameInfo.players.Count : (infoindex + GameInfo.direction <= 0 ? infoindex + GameInfo.direction + GameInfo.players.Count : infoindex + GameInfo.direction)].turn = true;
            }

            SendToAll(JsonConvert.SerializeObject(new { name = "gameUpdate", data = GameInfo }));
        }
        public void PullCard(int index, dynamic send)
        {
            int infoindex = GameInfo.players.FindIndex(i => index == i.index);
            if (!GameInfo.players[infoindex].turn)
                return;

            var newcard = GameInfo.pile[0];
            GameInfo.pile.RemoveAt(0);
            if(GameInfo.pile.Count == 0)
            {
                GameInfo.pile = Card.ShuffleDeck(GameInfo.playstack);
                GameInfo.playstack.Clear();
            }
            GameInfo.players[infoindex].deck.Add(newcard);
            if (newcard.color != GameInfo.topCard.color && newcard.type != GameInfo.topCard.type)
            {
                GameInfo.players[infoindex].turn = false;
                GameInfo.players[infoindex + 1 >= GameInfo.players.Count ? infoindex + 1 - GameInfo.players.Count : infoindex + 1].turn = true;
            }
            SendToAll(JsonConvert.SerializeObject(new { name = "gameUpdate", data = GameInfo }));
        }
        public void SetName(int index, dynamic send)
        {
            Console.WriteLine(send.username);
            lobbyManager.Find(p => p.index == index).name = send.username;
            SendToAll(JsonConvert.SerializeObject(new { name = "lobbyInfo", data = lobbyManager }));
        }
        public void SendToAll(string message)
        {
            byte[] response = Encoding.UTF8.GetBytes(message);
            foreach (var usr in users)
            {
                if (usr != null)
                {
                    Send(usr, response, 1, false, null);
                }
            }
        }
        public void SendToAllExcept(string message, int index)
        {
            byte[] response = Encoding.UTF8.GetBytes(message);
            var ad = users;
            ad.RemoveAt(index);
            foreach (var usr in ad)
            {
                if (usr != null)
                    Send(usr, response, 1, false, null);
            }
        }
        public void SendToOne(string message, int index)
        {
            byte[] response = Encoding.UTF8.GetBytes(message);
            Send(users[index], response, 1, false, null);
        }
        //Thanks Wildbook: https://gist.github.com/wildbook/b1f9adf03a47bedd0f23fafe1287c741
        static void Send(TcpClient client, byte[] payload, int opcode, bool masking, byte[] mask)
        {
            if (masking && mask == null) throw new ArgumentException(nameof(mask));

            using (var packet = new MemoryStream())
            {
                byte firstbyte = 0b0_0_0_0_0000; // fin | rsv1 | rsv2 | rsv3 | [ OPCODE | OPCODE | OPCODE | OPCODE ]

                firstbyte |= 0b1_0_0_0_0000; // fin
                                             //firstbyte |= 0b0_1_0_0_0000; // rsv1
                                             //firstbyte |= 0b0_0_1_0_0000; // rsv2
                                             //firstbyte |= 0b0_0_0_1_0000; // rsv3

                firstbyte += (byte)opcode; // Text
                packet.WriteByte(firstbyte);

                // Set bit: bytes[byteIndex] |= mask;

                byte secondbyte = 0b0_0000000; // mask | [SIZE | SIZE  | SIZE  | SIZE  | SIZE  | SIZE | SIZE]

                if (masking)
                    secondbyte |= 0b1_0000000; // mask

                if (payload.LongLength <= 0b0_1111101) // 125
                {
                    secondbyte |= (byte)payload.Length;
                    packet.WriteByte(secondbyte);
                }
                else if (payload.LongLength <= UInt16.MaxValue) // If length takes 2 bytes
                {
                    secondbyte |= 0b0_1111110; // 126
                    packet.WriteByte(secondbyte);

                    var len = BitConverter.GetBytes(payload.LongLength);
                    Array.Reverse(len, 0, 2);
                    packet.Write(len, 0, 2);
                }
                else // if (payload.LongLength <= Int64.MaxValue) // If length takes 8 bytes
                {
                    secondbyte |= 0b0_1111111; // 127
                    packet.WriteByte(secondbyte);

                    var len = BitConverter.GetBytes(payload.LongLength);
                    Array.Reverse(len, 0, 8);
                    packet.Write(len, 0, 8);
                }

                if (masking)
                {
                    packet.Write(mask, 0, 4);
                    payload = ApplyMask(payload, mask);
                }

                // Write all data to the packet
                packet.Write(payload, 0, payload.Length);

                // Get client's stream
                var stream = client.GetStream();

                var finalPacket = packet.ToArray();
                Console.WriteLine($@"SENT: {BitConverter.ToString(finalPacket)}");

                // Send the packet
                foreach (var b in finalPacket)
                    stream.WriteByte(b);
            }
        }
        static byte[] ApplyMask(IReadOnlyList<byte> msg, IReadOnlyList<byte> mask)
        {
            var decoded = new byte[msg.Count];
            for (var i = 0; i < msg.Count; i++)
                decoded[i] = (byte)(msg[i] ^ mask[i % 4]);
            return decoded;
        }
    }
    public class Card
    {
        public string color;
        public int type; //10 is skip (2), 11 is reverse (2), 12 is draw (2), 13 is wild (4), and 14 is wild draw 4 (4)

        public static Card[] cards = {
            new Card(){color = "red", type = 0},
            new Card(){color = "red", type = 1},
            new Card(){color = "red", type = 1},
            new Card(){color = "red", type = 2},
            new Card(){color = "red", type = 2},
            new Card(){color = "red", type = 3},
            new Card(){color = "red", type = 3},
            new Card(){color = "red", type = 4},
            new Card(){color = "red", type = 4},
            new Card(){color = "red", type = 5},
            new Card(){color = "red", type = 5},
            new Card(){color = "red", type = 6},
            new Card(){color = "red", type = 6},
            new Card(){color = "red", type = 7},
            new Card(){color = "red", type = 7},
            new Card(){color = "red", type = 8},
            new Card(){color = "red", type = 8},
            new Card(){color = "red", type = 9},
            new Card(){color = "red", type = 9},
            new Card(){color = "red", type = 10},
            new Card(){color = "red", type = 10},
            new Card(){color = "red", type = 11},
            new Card(){color = "red", type = 11},
            new Card(){color = "red", type = 12},
            new Card(){color = "red", type = 12},

            new Card(){color = "blue", type = 0},
            new Card(){color = "blue", type = 1},
            new Card(){color = "blue", type = 1},
            new Card(){color = "blue", type = 2},
            new Card(){color = "blue", type = 2},
            new Card(){color = "blue", type = 3},
            new Card(){color = "blue", type = 3},
            new Card(){color = "blue", type = 4},
            new Card(){color = "blue", type = 4},
            new Card(){color = "blue", type = 5},
            new Card(){color = "blue", type = 5},
            new Card(){color = "blue", type = 6},
            new Card(){color = "blue", type = 6},
            new Card(){color = "blue", type = 7},
            new Card(){color = "blue", type = 7},
            new Card(){color = "blue", type = 8},
            new Card(){color = "blue", type = 8},
            new Card(){color = "blue", type = 9},
            new Card(){color = "blue", type = 9},
            new Card(){color = "blue", type = 10},
            new Card(){color = "blue", type = 10},
            new Card(){color = "blue", type = 11},
            new Card(){color = "blue", type = 11},
            new Card(){color = "blue", type = 12},
            new Card(){color = "blue", type = 12},

            new Card(){color = "green", type = 0},
            new Card(){color = "green", type = 1},
            new Card(){color = "green", type = 1},
            new Card(){color = "green", type = 2},
            new Card(){color = "green", type = 2},
            new Card(){color = "green", type = 3},
            new Card(){color = "green", type = 3},
            new Card(){color = "green", type = 4},
            new Card(){color = "green", type = 4},
            new Card(){color = "green", type = 5},
            new Card(){color = "green", type = 5},
            new Card(){color = "green", type = 6},
            new Card(){color = "green", type = 6},
            new Card(){color = "green", type = 7},
            new Card(){color = "green", type = 7},
            new Card(){color = "green", type = 8},
            new Card(){color = "green", type = 8},
            new Card(){color = "green", type = 9},
            new Card(){color = "green", type = 9},
            new Card(){color = "green", type = 10},
            new Card(){color = "green", type = 10},
            new Card(){color = "green", type = 11},
            new Card(){color = "green", type = 11},
            new Card(){color = "green", type = 12},
            new Card(){color = "green", type = 12},

            new Card(){color = "yellow", type = 0},
            new Card(){color = "yellow", type = 1},
            new Card(){color = "yellow", type = 1},
            new Card(){color = "yellow", type = 2},
            new Card(){color = "yellow", type = 2},
            new Card(){color = "yellow", type = 3},
            new Card(){color = "yellow", type = 3},
            new Card(){color = "yellow", type = 4},
            new Card(){color = "yellow", type = 4},
            new Card(){color = "yellow", type = 5},
            new Card(){color = "yellow", type = 5},
            new Card(){color = "yellow", type = 6},
            new Card(){color = "yellow", type = 6},
            new Card(){color = "yellow", type = 7},
            new Card(){color = "yellow", type = 7},
            new Card(){color = "yellow", type = 8},
            new Card(){color = "yellow", type = 8},
            new Card(){color = "yellow", type = 9},
            new Card(){color = "yellow", type = 9},
            new Card(){color = "yellow", type = 10},
            new Card(){color = "yellow", type = 10},
            new Card(){color = "yellow", type = 11},
            new Card(){color = "yellow", type = 11},
            new Card(){color = "yellow", type = 12},
            new Card(){color = "yellow", type = 12},

            //Wild cards
            new Card(){color = "grey", type = 13},
            new Card(){color = "grey", type = 13},
            new Card(){color = "grey", type = 13},
            new Card(){color = "grey", type = 13},
            new Card(){color = "grey", type = 14},
            new Card(){color = "grey", type = 14},
            new Card(){color = "grey", type = 14},
            new Card(){color = "grey", type = 14},
        };

        public static List<Card> ShuffleDeck()
        {
            List<Card> deck = cards.ToList();
            var rnd = new Random();
            deck = deck.OrderBy(item => rnd.Next()).ToList();
            return deck;
        }
        public static List<Card> ShuffleDeck(List<Card> deck)
        {
            var rnd = new Random();
            deck = (List<Card>)deck.OrderBy(item => rnd.Next());
            return deck;
        }
    }

    public class GameInfo
    {
        public List<PlayerInfo> players;
        public Card topCard;
        public List<Card> pile;
        public List<Card> playstack;
        public int direction = 1;
    }
    public class PlayerInfo
    {
        public string name;
        public int index;
        public List<Card> deck;
        public bool turn;
        public bool leader;
    }


}
