using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Concurrent;

namespace otavaSocket
{
    class MemoryList
    {
        private static int maxMessageSize = 1024;
        MemoryNode memories = null;
        private int status = 0;

        private static MemoryNode CreateMemory()
        {
            MemoryNode mem = new MemoryNode();
            mem.prev = null;
            mem.data = new byte[maxMessageSize];
            mem.References = 1;

            return mem;
        }

        public MemoryNode GetMemory()
        {
            if (memories == null)
            {
                status++;
                Console.WriteLine("Allocating memory...");
                return CreateMemory();
            }

            MemoryNode mem = memories;
            memories = mem.prev;

            return mem;
        }

        public void ReleaseMemory(MemoryNode mem)
        {
            if (mem == null)
                return;

            lock (mem)
            {
                if (mem.References > 2)
                {
                    mem.References--;
                    return;
                }
            }

            mem.prev = memories;
            mem.References = 1;
            memories = mem;

            status--;
            Console.WriteLine("Releasing memory...");
        }
    }

    class MemoryNode
    {
        public MemoryNode prev;
        public byte[] data;
        public int References;
        public int length;
    }

    class MessageData
    {
        public Client Sender { get; init; }
        public ArraySegment<byte> Message { get; init; }
    }

    class Client
    {
        public WebSocket socket { get; init; }
        public int ID { get; init; }
        public Task processTask { get; set; }
        public Task sendTask {get; set;}
        public ConcurrentQueue<MemoryNode> toSend { get; set; }
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
                } catch (AggregateException)
                {
                    Console.WriteLine($"Shutdown client {i}");
                }
                i++;
            }
        }

        public void AddClient(WebSocket client)
        {
            Client newClient = new Client { socket=client,
                ID = currentID,
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
                Console.Write(mem.data[i]);
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

                    // TODO: this probably needs a lock to prevent data races
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
                foreach (var recipient in clients)
                {
                    Console.WriteLine("Enqueing message to send");
                    //TODO: merge this into a method  on the client
                    mem.References++;
                    recipient.toSend.Enqueue(mem);
                }
            }

            //clients.Remove(client);
        }

        private async Task OnReceive(MessageData receivedMessage)
        {
            var sendTasks = new List<Task>();
            foreach (var client in clients)
            {
                //client.sendTask.Wait();;
                Task t = client.socket.SendAsync(receivedMessage.Message, WebSocketMessageType.Text, true, CancellationToken.None);
                sendTasks.Add(t);
            }
            await Task.WhenAll(sendTasks);

            //WebSocket sender = receivedMessage.Sender;
            //await sender.SendAsync(receivedMessage.Message, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
