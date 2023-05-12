namespace  Autocomplete.EmulateSearchActivity;

using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            var host = ConfigureBuilder(args).Build();
            await host.RunAsync();
        }
        catch(Exception ex)
        {
            //cleanup
        }
    }

    private static IHostBuilder ConfigureBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddMassTransit(x =>
                {
                    x.SetKebabCaseEndpointNameFormatter();
                    
                    x.UsingRabbitMq((context,cfg) =>
                    {
                        cfg.Host("localhost", "/", h => {
                            h.Username("guest");
                            h.Password("guest");
                        });
                        // cfg.ConfigureEndpoints(context);
                    });
                });
                services.AddHostedService<Producer>();
            });
    }
}