using System;

namespace otavaSocket
{
    class MemoryNode
    {
        public MemoryNode prev;
        public byte[] data;
        public int References;
        public int length;
    }

    class MemoryList
    {
        private static int maxBufferSize = 1024;
        private MemoryNode memories = null;

        private static MemoryNode CreateMemory()
        {
            MemoryNode mem = new MemoryNode();
            mem.prev = null;
            mem.data = new byte[maxBufferSize];
            mem.References = 1;

            return mem;
        }

        public MemoryNode GetMemory()
        {
            if (memories == null)
            {
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

            Console.WriteLine("Releasing memory...");
        }

        public void AddReference(MemoryNode mem)
        {
            mem.References++;
        }
    }

}
