namespace  Autocomplete.ProcessSearchStatistics;

using MassTransit;

internal class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            WebApplication app = ConfigureApp(args);

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            // cleanup
        }
    }

    private static WebApplication ConfigureApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // builder.Services.AddRazorPages();

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("localhost", "/", h => {
                    h.Username("guest");
                    h.Password("guest");
                });

                // cfg.ConfigureMessageTopology();

                cfg.ConfigureEndpoints(context);

                // cfg.ConfigureEndpoints(context);

            });
            x.AddConsumer<SearchSubmittedConsumer>();
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();


        //app.UseStaticFiles();

        //app.UseRouting();

        //app.UseAuthorization();

        //app.MapRazorPages();


        return app;
    }
}