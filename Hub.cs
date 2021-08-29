using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System;
using System.Threading;

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
            mem.prev = memories;
            memories = mem;

            status--;
            Console.WriteLine("Releasing memory...");
        }
    }

    class MemoryNode
    {
        public MemoryNode prev;
        public byte[] data;
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
        public Task sendTask { get; set; }
        public Task processTask { get; set; }

        public void LogMessage(ArraySegment<byte> data)
        {
            Console.WriteLine($"Client {ID} received {data.Count} bytes:");
            for (int i = 0; i < data.Count; i++) {
                Console.Write(data[i]);
            }
            Console.WriteLine();
            Console.WriteLine("--");
        }

        //PROBLEMS: jasny data race s IDckom ale to je asi jedno
        //          dajako treba vymazat disconnectnutych clientov z arrayu
        //          a to moze byt cancer lebo zase sa to deje paralelne
        //          mozno ich len oznacim ako dead alebo co alebo budem checkovat
        //          ci je state open.
        //
        public async Task ProcessMessages(MemoryList memories, IReadOnlyList<Client> clients)
        {
            WebSocketMessageType messageType;
            var mem = memories.GetMemory();
            var buffer = new ArraySegment<byte>(mem.data);
            do {
                Console.WriteLine($"Client {ID} receiving...");
                var receiveResult = await socket.ReceiveAsync(buffer, CancellationToken.None);
                messageType = receiveResult.MessageType;

                var receivedBuffer = new ArraySegment<byte>(mem.data, 0, receiveResult.Count);
                LogMessage(receivedBuffer);
                
                var sendTasks = new List<Task>();
                foreach (var client in clients)
                {
                    await client.sendTask;
                    Task t = client.socket.SendAsync(
                                receivedBuffer,
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None
                            );
                    sendTasks.Add(t);
                    client.sendTask = t;
                }

                await Task.WhenAll(sendTasks);

            } while (messageType != WebSocketMessageType.Close);

            Console.WriteLine($"Client {ID} shutting down...");
            memories.ReleaseMemory(mem);
        }
    }

    public class Hub
    {
        private List<Client> clients;
        MemoryList memories;
        List<Task<bool>> receiveTasks;
        Task t;
        CancellationTokenSource cts;
        int currentID;

        public Hub()
        {
            clients = new List<Client>();
            memories = new MemoryList();
            receiveTasks = new List<Task<bool>>();
            cts = new CancellationTokenSource();
            currentID = 1;
        }

        public void AddClient(WebSocket client)
        {
            Console.WriteLine($"WS client connected! Currently connected clients: {clients.Count}");
            Client newClient = new Client { socket=client, sendTask = Task.CompletedTask, ID = currentID};
            clients.Add(newClient);
            newClient.processTask = newClient.ProcessMessages(memories, clients);
            currentID++;//bad-data race, use GUID or something
        }

        public async Task ProcessClientsAsync(CancellationToken cancellationToken)
        {
            while (receiveTasks.Count != 0)
            {
                //wait until one of the clients sends something
                // When any doesnt notice new elements in list!!!!
                // Solution: on every AddClient restart this method
                //      by cancelling the task returned by WhenAny
                //      and catching the resulting exception, breaking the loop
                var waitTask = Task.WhenAny(receiveTasks);
                Task<bool> finishedTask;
                try {
                    waitTask.Wait(cancellationToken);//wait for the whenany cancellable
                    finishedTask = waitTask.Result;
                }
                catch (OperationCanceledException) {
                    //got new clients -> restart this method
                    Console.WriteLine("PC: Got cancellation request, exiting..");
                    break;
                }
                //var finishedTask = await Task.WhenAny(receiveTasks);
                int connectionIndex = receiveTasks.IndexOf(finishedTask);
                bool keepAlive = finishedTask.Result;
                if (keepAlive) {
                    Console.WriteLine("PC: Extending connection...");
                    receiveTasks[connectionIndex] = ReceiveFromClient(clients[connectionIndex]);
                }
                else {
                    Console.WriteLine("PC: Closing connection...");
                    receiveTasks.RemoveAt(connectionIndex);
                    // The client closed down the socket, so we can probably forget about it
                    clients.RemoveAt(connectionIndex);
                }
            }
        }

        // returns false if the received message was a CLOSE, true otherwise
        private async Task<bool> ReceiveFromClient(Client client)
        {
            MemoryNode mem = memories.GetMemory();
            ArraySegment<byte> buffer = new ArraySegment<byte>(mem.data);
            var socket = client.socket;
            var receiveResult = await socket.ReceiveAsync(buffer, CancellationToken.None);
            Console.WriteLine($"received {receiveResult.Count} bytes with status {receiveResult.MessageType}");
            
            for (int i = 0; i < receiveResult.Count; i++)
            {
                Console.Write((char)buffer[i]);
            }
            Console.WriteLine();

            bool isClose = receiveResult.MessageType == WebSocketMessageType.Close;

            if (!isClose) {
                //echo the buffer back
                var echoBuf = new ArraySegment<byte>(mem.data, 0, receiveResult.Count);
                var msgData = new MessageData{ Sender=client, Message=echoBuf};
                await OnReceive(msgData);
            }

            memories.ReleaseMemory(mem);
            return !isClose;
        }

        private async Task OnReceive(MessageData receivedMessage)
        {
            var sendTasks = new List<Task>();
            foreach (var client in clients)
            {
                //client.sendTask.Wait();;
                Task t = client.socket.SendAsync(receivedMessage.Message, WebSocketMessageType.Text, true, CancellationToken.None);
                sendTasks.Add(t);
                client.sendTask = t;
            }
            await Task.WhenAll(sendTasks);

            //WebSocket sender = receivedMessage.Sender;
            //await sender.SendAsync(receivedMessage.Message, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        //private async Task Echo(ArraySegment<byte> buffer)
        //{
        //    var sendTasks = new List<Task>();
        //    foreach (var client in clients)
        //    {
        //        sendTasks.Add(client.SendAsync(buffer, WebSocketMessageType.Text, false, CancellationToken.None));
        //    }
        //    Console.WriteLine(buffer);
        //    await Task.WhenAll(sendTasks);
        //}

    }
}
