namespace Geofarmland.Server.Infrastructure.ExternalApiServices
{
    public static class SignalHub
    {
        public static void SendMessage(string message)
        {
            // Implementation for sending a message
            Console.WriteLine($"Sending message: {message}");
        }

        public static string ReceiveMessage()
        {
            // Implementation for receiving a message
            var receivedMessage = "Received message";
            Console.WriteLine(receivedMessage);
            return receivedMessage;
        }
    }
}
