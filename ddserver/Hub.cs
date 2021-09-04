using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Text;

namespace otavaSocket
{
    /// Represents a WebSocket connection
    internal class Client
    {
        /// the underlying socket
        public WebSocket socket { get; init; }
        /// Session associated with this connection
        public Session session { get; init; }
        /// a string identifying the client
        public string id {get;init;}
        /// The Task that receives and processes data
        public Task processTask { get; set; }
        /// The Task that sends data
        public Task sendTask {get; set;}
        /// A Queue of messages that havent been sent yet
        public ConcurrentQueue<MemoryNode> toSend { get; init; }
    }

    /// Class for managing WebSocket connections to the server
    /**
     * To provide custom behavior when processing data,
     * derive this class and override the OnReceive method.
     * The base version does no processing and just echoes the
     * received message to all connected clients.
     */
    public class Hub
    {
        /// A collection of currently active WebSocket connections
        private List<Client> clients;
        /// Manager of memory blocks
        MemoryList memories;
        /// A CTS used to shutdown the Hub
        CancellationTokenSource cts;

        public Hub()
        {
            clients = new List<Client>();
            memories = new MemoryList();
            cts = new CancellationTokenSource();
        }

        /// Cancel all tasks wait until they finish
        public async Task Stop()
        {
            cts.Cancel();

            int i = 0;
            foreach (var client in clients)
            {
                // WebSocket.SendAsync doesnt throw any exceptions
                // according to the docs
                // I dont really believe it so beware
                await client.sendTask;
                try {
                    await client.processTask;
                }
                catch (AggregateException)
                {
                    Console.WriteLine($"Shutdown client {i}");
                }
                i++;
            }
        }

        /// Add a new connection to manage
        /**
         * Adds the newly created Client object to the list
         * of active connections and starts handling the data
         * transfer.
         *
         * @param client The WebSocket instance that needs to be managed
         * @param session session data corresponding to the socket connection
         */
        public void AddClient(WebSocket client, Session session)
        {
            string id = "unknown";
            session.SessionData.TryGetValue("Username", out id);

            Client newClient = new Client { socket=client,
                session = session,
                id = id,
                toSend = new ConcurrentQueue<MemoryNode>()
            };
            clients.Add(newClient);

            Console.WriteLine($"WS client connected! Currently connected clients: {clients.Count}");
            newClient.processTask = ReceiveFromClientAsync(newClient);
            newClient.sendTask    = SendToClientAsync(newClient);
        }

        /// Helper method for writing out the received message
        private static void LogMessage(MemoryNode mem)
        {
            Console.WriteLine($"Received {mem.length} bytes:");
            for (int i = 0; i < mem.length; i++) {
                Console.Write((char)mem.data[i]);
            }
            Console.WriteLine();
            Console.WriteLine("--");
        }

        /// Manages sending messages to the client
        /**
         * Sends pending messages to the client until cancelled
         * or the client shuts down.
         *
         * Has an internal timeout of 150ms to release pressure on
         * the thread when there is  nothing to send
         *
         * @param client The Client to send messages to
         */
        private async Task SendToClientAsync(Client client)
        {
            while (client.socket.State == WebSocketState.Open)
            {
                MemoryNode messageData;
                while (client.toSend.TryDequeue(out messageData))
                {
                    Console.WriteLine("dequeued message");
                    var buf = new ArraySegment<byte>(messageData.data, 0, messageData.length);

                    if (client.socket.State != WebSocketState.Open) {
                        Console.WriteLine("socket was closed");
                        break;
                    }

                    await client.socket.SendAsync(
                            buf,
                            WebSocketMessageType.Text,
                            endOfMessage: true,
                            cts.Token
                        );

                    memories.ReleaseMemory(messageData);
                }

                await Task.Delay(150);
                if (cts.IsCancellationRequested) {
                    break;
                }
            }
        }

        /// Manage receiving data from the client
        /**
         * Receives data until the socket closes, the Hub gets cancelled
         * or the received message was a CLOSE
         *
         * @param client The Client to receive from
         */
        private async Task ReceiveFromClientAsync(Client client)
        {
            var socket = client.socket;

            while (socket.State == WebSocketState.Open)
            {
                MemoryNode mem = memories.GetMemory();
                var buf = new ArraySegment<byte>(mem.data);
                var receiveContext = await socket.ReceiveAsync(buf, cts.Token);
                bool isMessageClose = receiveContext.MessageType == WebSocketMessageType.Close;

                mem.length = receiveContext.Count;

                if (isMessageClose) {
                    Console.WriteLine("Received CLOSE -- Shutting down");
                    break;
                }
                LogMessage(mem);

                // make the byte[] into a string for convenience
                string message = Encoding.ASCII.GetString(mem.data, 0, mem.length);
                string processedMessage = OnReceive(message, client.session, client.id);
                if (processedMessage.Length == 0)
                    continue;

                Console.WriteLine($"SENDING BACK: {processedMessage}");
                Encoding.ASCII.GetBytes(processedMessage, 0, processedMessage.Length, mem.data, 0);
                mem.length = processedMessage.Length;

                // This depends on the application to provide a value
                // for 'currentRoom' in the session which kinda sucks
                // and ruins the separation but I dont have the motivation
                // to make this better
                string senderGroup;
                client.session.SessionData.TryGetValue("currentRoom", out senderGroup);

                // Without this lock a client could finish sending before we increment
                // the reference count and dispose of the memory prematurely
                lock (mem)
                {
                    foreach (var recipient in clients)
                    {
                        string recipientGroup;
                        recipient.session.SessionData.TryGetValue("currentRoom", out recipientGroup);

                        // only send to clients in the same group
                        if (senderGroup == recipientGroup) {
                            memories.AddReference(mem);
                            recipient.toSend.Enqueue(mem);
                        }
                    }
                }
                if (cts.IsCancellationRequested) {
                    break;
                }
            }

            // send can probably hang in some cases so this
            // may not always work, maybe its ok to just remove it and forget it
            await client.sendTask;
            clients.Remove(client);
        }

        /// Process the received message
        /**
         * @param message The received message
         * @param session The session corresponding to the connection
         * @param senderID A string identifier of the sender
         */
        protected virtual string OnReceive(string message, Session session, string senderID)
        {
            return message;
        }
    }
}
