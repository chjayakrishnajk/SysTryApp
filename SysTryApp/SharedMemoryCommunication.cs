using System;
using System.Text;
using System.Threading.Tasks;
using System.IO.MemoryMappedFiles;

namespace SysTryApp
{
    public class SharedMemoryCommunication : IDisposable
    {
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _accessor;
        private const int MemorySize = 1024 * 1000 * 5; // 5 MB
        private const int LengthFieldSize = sizeof(int);

        // Event to be triggered when a message is received
        public event EventHandler<string> OnMessageReceived;

        // Initialize the shared memory segment
        public void Initialize(string memoryName)
        {
            _mmf = MemoryMappedFile.CreateOrOpen(memoryName, MemorySize);
            _accessor = _mmf.CreateViewAccessor();
        }

        // Send a message to shared memory
        public void Send(bool isStr, string message)
        {
            message = "SERVER!" + message;

            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            // Write the length of the message as an int
            _accessor.Write(0, messageBytes.Length);

            // Write the message starting at offset 4 (after the length field)
            _accessor.WriteArray(LengthFieldSize, messageBytes, 0, messageBytes.Length);
        }

        // Receive a message from shared memory
        private string Receive()
        {
            // Read the length of the message
            int messageLength = _accessor.ReadInt32(0);
            if (messageLength == 0)
            {
                return null; // No new message
            }

            // Read the message content
            byte[] messageBytes = new byte[messageLength];
            _accessor.ReadArray(LengthFieldSize, messageBytes, 0, messageBytes.Length);

            // Clear the message length to indicate that the message has been read
            _accessor.Write(0, 0);

            return Encoding.UTF8.GetString(messageBytes);
        }

        // Continuously listen for incoming messages
        public async Task StartListeningAsync()
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    string message = Receive();
                    if (!string.IsNullOrEmpty(message))
                    {
                        OnMessageReceived?.Invoke(this, message);
                    }
                    Task.Delay(100).Wait(); // Small delay to avoid busy-waiting
                }
            });
        }

        // Dispose of resources
        public void Dispose()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
        }
    }
}
