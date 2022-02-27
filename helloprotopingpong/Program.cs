// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using Proto;

Console.WriteLine("Hello, World!");
var system = new ActorSystem();
var numberOfPingers = 2;
var numberOfPings = 1000;

var monitorProps = Props.FromProducer(() => new MonitorActor());
var monitor = system.Root.SpawnNamed(monitorProps, "monitor");
system.Root.Send(monitor, new StartTest(numberOfPingers, numberOfPings));

Console.ReadLine();

public record StartTest(int NumberOfPingers, int NumberOfPings);
public record Start(int PingerNumber, int NumberOfPings);
public record PingerDone(int PingerNumber, long TotalMs);
public record Ping(int PingerId);
public record Pong(int MessageCount);

public class WorkerActor : IActor
{
    private int MessageCount = 0;
    private Random random;

    public WorkerActor()
    {
        random = new Random();
    }
    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Ping ping:
                Console.WriteLine($"Ping {MessageCount} from {ping.PingerId} ({context.Sender})");
                context.Respond(new Pong(MessageCount++));
                if(random.Next(0, 100) == 42)
                {
                    Console.WriteLine("==> I'm crashing here! <==");
                    throw new Exception("Oh no, you got 42!");
                }
                break;
            default: 
                Console.WriteLine($"==> Worker: Unknown message {context.Message}");
                break;
        }
        return Task.CompletedTask;
    }
}

public class PingerActor : IActor
{
    private int numberOfPings;
    private PID worker;
    private int PingerNumber;
    private int sentPings;
    private int receivedPongs;
    private Stopwatch stopWatch;
    private Props workerProps;

    public PingerActor(Props workerProps)
    {
        this.workerProps = workerProps;
    }

    public Task ReceiveAsync(IContext context)
    {
        var monitor = PID.FromAddress(context.System.Address, "monitor");
        switch (context.Message)
        {
            case Start start:
                worker = context.Spawn(workerProps);
                PingerNumber = start.PingerNumber;
                numberOfPings = start.NumberOfPings;
                Console.WriteLine($"Starting pinger: {start.PingerNumber}");
                context.Request(worker, new Ping(PingerNumber));
                sentPings++;
                stopWatch = new Stopwatch();
                stopWatch.Start();
                break;
            case Pong pong:
                Console.WriteLine($"Pong {receivedPongs}: {pong.MessageCount} (Pinger {PingerNumber})");
                receivedPongs++;
                if(sentPings < numberOfPings) 
                {
                    context.Request(worker, new Ping(PingerNumber));
                    sentPings++;
                }
                if(receivedPongs == numberOfPings)
                {
                    stopWatch.Stop();
                    Console.WriteLine($"Pinger {PingerNumber} done in {stopWatch.ElapsedMilliseconds} ms");
                    context.Send(monitor, new PingerDone(PingerNumber, stopWatch.ElapsedMilliseconds));
                }
                break;
            default:
                Console.WriteLine($"==> System message: {context.Message}, {worker}");
                break;
        }
        return Task.CompletedTask;
    }
}

public class MonitorActor : IActor
{
    private Dictionary<int, long> stats = new Dictionary<int, long>();
    private Props pingerProps;
    private int numberOfPingers;
    private int numberOfPings;
    private Stopwatch stopwatch;

    public MonitorActor()
    {
        pingerProps = 
            Props.FromProducer(() => new PingerActor(Props.FromProducer(() => new WorkerActor())))
                .WithChildSupervisorStrategy(new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1000, null));
    }

    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case StartTest startTest:
                numberOfPingers = startTest.NumberOfPingers;
                numberOfPings = startTest.NumberOfPings;
                stopwatch = new Stopwatch();
                stopwatch.Start();
                for(int i = 0; i < numberOfPingers; i++)
                {
                    var pinger = context.Spawn(pingerProps);
                    context.Send(pinger, new Start(i, startTest.NumberOfPings));
                }
                break;
            case PingerDone pingerDone:
                Console.WriteLine($"Pinger {pingerDone.PingerNumber} done in {pingerDone.TotalMs} ms ({stats.Count})");
                stats.Add(pingerDone.PingerNumber, pingerDone.TotalMs);
                if(stats.Count == numberOfPingers)
                {
                    stopwatch.Stop();
                    var total = stopwatch.ElapsedMilliseconds;
                    var numberOfMessages = numberOfPingers * numberOfPings * 2;
                    var messagesPerS = 1000 * numberOfMessages / total;
                    context.Stop(context.Self);
                    Console.WriteLine($"=========================");
                    Console.WriteLine($"Total: {total} ms");
                    Console.WriteLine($"Average: {total / numberOfPingers} ms");
                    Console.WriteLine($"Message/s: {messagesPerS}");
                }
                break;
            default:
                Console.WriteLine($"System message: {context.Message}");
                break;
        }
        return Task.CompletedTask;
    }
}
