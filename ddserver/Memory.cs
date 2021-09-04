using System;

namespace otavaSocket
{

    /// Represents a block of memory
    /**
     * Functions as a singly linked list of memory blocks.
     * Used to buffer data received from WebSockets
     *
     * I made this to minimize the number of allocations, but
     * now I'm not sure if its even an optimization.
     */
    class MemoryNode
    {
        public MemoryNode prev;
        public byte[] data;
        public int References;
        public int length;
    }

    /// A wrapper class around the MemoryNode linked list
    class MemoryList
    {
        private static int maxBufferSize = 1024;
        /// The head of the linked list
        private MemoryNode memories = null;

        /// Allocate more memory
        private static MemoryNode CreateMemory()
        {
            MemoryNode mem = new MemoryNode();
            mem.prev = null;
            mem.data = new byte[maxBufferSize];
            mem.References = 1;

            return mem;
        }

        /// Get a block of memory or create one
        /// if there are none
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

        /// Put a MemoryNode back into the linked list
        /// if noone needs it anymore, otherwise just
        /// decrement the reference counter
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

        /// Increment the reference counter for a MemoryNode
        public void AddReference(MemoryNode mem)
        {
            mem.References++;
        }
    }

}
