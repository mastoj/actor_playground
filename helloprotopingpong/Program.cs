// See https://aka.ms/new-console-template for more information
using Proto;

Console.WriteLine("Hello, World!");
var system = new ActorSystem();
var props = Props.FromProducer(() => new WorkerActor());
var worker = system.Root.Spawn(props);
var response = await system.Root.RequestAsync<Pong>(worker, new Ping(), CancellationToken.None);
Console.WriteLine($"Got response: {response}");


public record Ping;
public record Pong(int messageCount);

public class WorkerActor : IActor
{
    private int MessageCount = 0;
    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is Ping)
        {
            Console.WriteLine("Ping");
            context.Respond(new Pong(MessageCount++));
        }
        return Task.CompletedTask;
    }
}

