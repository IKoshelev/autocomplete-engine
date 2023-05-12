namespace Autocomplete.EmulateSearchActivity;

using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Hosting;

using Autocomplete.Common.Messages;

public class Producer : BackgroundService
{
    readonly IBus _bus;
    public Producer(IBus bus)
    {
        _bus = bus;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _bus.Publish(new SearchSubmitted("aaa"), stoppingToken);
            Console.WriteLine($"{DateTime.Now}: published");
            await Task.Delay(1000, stoppingToken);
        }
    }
}