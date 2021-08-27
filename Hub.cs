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

    public class Hub
    {
        private List<WebSocket> clients;
        MemoryList memories;
        List<Task<bool>> receiveTasks;
        Task t;

        public Hub()
        {
            clients = new List<WebSocket>();
            memories = new MemoryList();
            receiveTasks = new List<Task<bool>>();
        }

        public void AddClient(WebSocket client)
        {
            clients.Add(client);
            Console.WriteLine($"WS client connected! Currently connected clients: {clients.Count}");
            receiveTasks.Add(ReceiveFromClient(client));
            if (t == null || t.IsCompleted)
            {
                // start processing connections when at least 1 client is connected
                // and dont start it again when its already running
                t = ProcessClients();
            }
        }

        public async Task ProcessClients()
        {
            while (receiveTasks.Count != 0)
            {
                //wait until one of the clients sends something
                var finishedTask = await Task.WhenAny(receiveTasks);
                int connectionIndex = receiveTasks.IndexOf(finishedTask);
                bool keepAlive = finishedTask.Result;
                if (keepAlive) {
                    Console.WriteLine("PC: Extending connection...");
                    receiveTasks[connectionIndex] = ReceiveFromClient(clients[connectionIndex]);
                }
                else {
                    Console.WriteLine("PC: Closing connection...");
                    //TODO: Maybe do something smart and reuse the positions idk
                    //      c# probably does something about it
                    receiveTasks.RemoveAt(connectionIndex);
                    // The client closed down the socket, so we can probably just throw it away
                    clients.RemoveAt(connectionIndex);
                }
            }
        }

        // returns false if the received message was a CLOSE, true otherwise
        private async Task<bool> ReceiveFromClient(WebSocket client)
        {
            MemoryNode mem = memories.GetMemory();
            ArraySegment<byte> buffer = new ArraySegment<byte>(mem.data);
            var receiveResult = await client.ReceiveAsync(buffer, CancellationToken.None);
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
                await client.SendAsync(echoBuf, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            memories.ReleaseMemory(mem);
            return !isClose;
        }

        private async Task Echo(ArraySegment<byte> buffer)
        {
            var sendTasks = new List<Task>();
            foreach (var client in clients)
            {
                sendTasks.Add(client.SendAsync(buffer, WebSocketMessageType.Text, false, CancellationToken.None));
            }
            Console.WriteLine(buffer);
            await Task.WhenAll(sendTasks);
        }

    }
}
