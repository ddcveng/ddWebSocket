using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Concurrent;

namespace otavaSocket
{
    class Client
    {
        public WebSocket socket { get; init; }
        public Session session { get; init; }
        public Task processTask { get; set; }
        public Task sendTask {get; set;}
        public ConcurrentQueue<MemoryNode> toSend { get; init; }
    }

    public class Hub
    {
        private List<Client> clients;
        MemoryList memories;
        CancellationTokenSource cts;
        ConcurrentQueue<MemoryNode> toSend;
        int currentID;

        public Hub()
        {
            clients = new List<Client>();
            memories = new MemoryList();
            cts = new CancellationTokenSource();
            toSend = new ConcurrentQueue<MemoryNode>();
            currentID = 1;
        }

        public void Stop()
        {
            cts.Cancel();

            int i = 0;
            foreach (var client in clients)
            {
                // WebSocket.SendAsync doesnt throw any exceptions
                // according to the docs
                // I dont really believe it so beware
                client.sendTask.Wait();
                try {
                    client.processTask.Wait();
                }
                catch (AggregateException)
                {
                    Console.WriteLine($"Shutdown client {i}");
                }
                i++;
            }
        }

        public void AddClient(WebSocket client, Session session)
        {
            Client newClient = new Client { socket=client,
                session = session,
                toSend = new ConcurrentQueue<MemoryNode>()
            };
            clients.Add(newClient);
            Console.WriteLine($"WS client connected! Currently connected clients: {clients.Count}");
            newClient.processTask = ReceiveFromClientAsync(newClient);
            newClient.sendTask    = SendToClientAsync(newClient);
            currentID++;//bad-data race, use GUID or something
        }

        private static void LogMessage(MemoryNode mem)
        {
            Console.WriteLine($"Received {mem.length} bytes:");
            for (int i = 0; i < mem.length; i++) {
                Console.Write((char)mem.data[i]);
            }
            Console.WriteLine();
            Console.WriteLine("--");
        }

        private async Task SendToClientAsync(Client client)
        {
            while (true)
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

        // Receive messages from client until the connection is closed
        // uses user defined OnReceive function to process data -- not yet implemented
        private async Task ReceiveFromClientAsync(Client client)
        {
            var socket = client.socket;

            while (true)
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
                // call OnReceive -- user defined processing of the received data
                LogMessage(mem);
                OnReceive(mem, client.session);
                // Without this lock a client could finish sending before we increment
                // the reference count and dispose of the memory prematurely
                lock (mem)
                {
                    foreach (var recipient in clients)
                    {
                        memories.AddReference(mem);
                        recipient.toSend.Enqueue(mem);
                    }
                }
            }

            //clients.Remove(client);
        }

        protected virtual void OnReceive(MemoryNode message, Session session)
        {
        }
    }
}
