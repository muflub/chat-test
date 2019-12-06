using Common;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrleansBasics
{
    public class Program
    {
        private static IStreamProvider _streamProvider;

        static int Main(string[] args)
        {
            return RunMainAsync().Result;
        }

        private static async Task<int> RunMainAsync()
        {
            try
            {
                using (var client = await ConnectClient())
                {
                    await DoClientWork(client);
                    Console.ReadKey();
                }

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"\nException while trying to run client: {e.Message}");
                Console.WriteLine("Make sure the silo the client is trying to connect to is running.");
                Console.WriteLine("\nPress any key to exit.");
                Console.ReadKey();
                return 1;
            }
        }

        private static async Task<IClusterClient> ConnectClient()
        {
            IClusterClient client;
            client = new ClientBuilder()
                .AddSimpleMessageStreamProvider("SMSProvider")
                .UseLocalhostClustering()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "dev";
                    options.ServiceId = "OrleansBasics";
                })
                .ConfigureLogging(logging => logging.AddConsole())
                .Build();

            await client.Connect();

            _streamProvider = client.GetStreamProvider("SMSProvider");

            Console.WriteLine("Client successfully connected to silo host \n");
            return client;
        }

        private static async Task RunChatTest(IClusterClient client)
        {
            var channelName = "Test Channel";

            // Get the channel proxy object
            var channel = client.GetGrain<IChatChannel>(channelName);

            // Join the channel
            var memberId = await channel.JoinChannel(Guid.NewGuid().ToString());
            Console.WriteLine("\n\nMemberId: {0}\n\n", memberId);

            // Listen for channel events
            var events = _streamProvider.GetStream<ChatChannelEvent>(new Guid(memberId), channelName);
            await events.SubscribeAsync(async (data, token) => HandleChatEvent(memberId, data));

            // Send a message to the channel
            await channel.SendMessage(memberId, "Good morning, HelloGrain!");
        }

        private static async Task DoClientWork(IClusterClient client)
        {
            var tasks = new List<Task>();

            for (int x=0; x<10; ++x)
            {
                tasks.Add(RunChatTest(client));
            }

            await Task.WhenAll(tasks);
        }

        private static void HandleChatEvent(String id, ChatChannelEvent data)
        {
            switch (data.EventType)
            {
                case Event.MemberJoined:
                    Console.WriteLine($"{id} OnMemberJoined: {data.EventData} [{data.MemberId}]");
                    break;
                case Event.MemberLeft:
                    Console.WriteLine($"{id} OnMemberLeft: {data.EventData} [{data.MemberId}]");
                    break;
                case Event.Message:
                    Console.WriteLine($"{id} Message: {data.EventData} [{data.MemberId}]");
                    break;
                case Event.Whisper:
                    Console.WriteLine($"{id} Whisper: {data.EventData} [{data.MemberId}]");
                    break;
            }
        }
    }
}