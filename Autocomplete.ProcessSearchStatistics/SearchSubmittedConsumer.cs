namespace Autocomplete.ProcessSearchStatistics;

using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;

using Autocomplete.Common.Messages;

public class SearchSubmittedConsumer: IConsumer<SearchSubmitted>
{
    readonly ILogger<SearchSubmittedConsumer> _logger;
    public SearchSubmittedConsumer(ILogger<SearchSubmittedConsumer> logger)
    {
        _logger = logger;
    }
    public Task Consume(ConsumeContext<SearchSubmitted> context)
    {
        _logger.LogInformation("Received Text: {Text}", context.Message.SearchText);
        Console.WriteLine(context.Message.SearchText);
        return Task.CompletedTask;
    }
}