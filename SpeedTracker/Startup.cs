using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using SpeedTest.Net.Helpers;
using SpeedTracker.Data;

namespace SpeedTracker
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            using (var db = new StatisticsContext())
                db.Database.EnsureCreated();

            services.AddOptions<Settings>()
                    .Bind(Configuration);

            services.AddHostedService<StatisticsCollector>();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/stats.json", async context =>
                {
                    using (var db = new StatisticsContext())
                    {
                        var stats = await db.Statistics.Where(x => x.Date > DateTime.UtcNow.AddDays(-2))
                                   .ToListAsync();

                        var window = TimeSpan.FromMinutes(5).Ticks;
                        var data = stats
                        .GroupBy(x => new DateTime(window * (x.Date.Ticks / window), DateTimeKind.Utc))
                        .Select(x => new
                        {
                            t = x.Key,
                            Latancies = x.Where(x => x.Type == DataType.Latancy),
                            DownloadSpeeds = x.Where(x => x.Type == DataType.DownloadSpeed),
                            UploadSpeeds = x.Where(x => x.Type == DataType.UploadSpeed),
                        })
                        .Select(x => new
                        {
                            t = x.t,
                            Latancy = x.Latancies.Any() ? x.Latancies.Average(x => x.Value) : (double?)null,
                            DownloadSpeed = x.DownloadSpeeds.Any() ? x.DownloadSpeeds.Average(x => x.Value) : (double?)null,
                            UploadSpeed = x.UploadSpeeds.Any() ? x.UploadSpeeds.Average(x => x.Value) : (double?)null
                        });

                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(data));
                    }
                });

                endpoints.MapGet("/stats.csv", async context =>
                {
                    var sb = new StringBuilder();
                    using (var db = new StatisticsContext())
                    {
                        var stats = await db.Statistics.OrderByDescending(x => x.Date)
                                .Take(10000)
                                .ToListAsync();

                        sb.AppendLine("Date,Latancy(ms),Download(Mbps),Upload(Mbps)");

                        foreach (var g in stats.GroupBy(x => x.Date).OrderByDescending(x => x.Key))
                        {
                            sb.Append(g.Key);
                            sb.Append(',');
                            var l = g.Where(x => x.Type == DataType.Latancy).Average(x => x.Value);
                            sb.Append(l);
                            sb.Append(',');

                            var d = g.Where(x => x.Type == DataType.DownloadSpeed).Average(x => x.Value).FromBytesPerSecondTo(SpeedTest.Net.Enums.SpeedTestUnit.MegaBitsPerSecond);
                            sb.Append(d);
                            sb.Append(',');

                            var u = g.Where(x => x.Type == DataType.UploadSpeed).Average(x => x.Value).FromBytesPerSecondTo(SpeedTest.Net.Enums.SpeedTestUnit.MegaBitsPerSecond);
                            sb.Append(u);
                            sb.AppendLine();
                        }
                    }

                    context.Response.Headers.Add("Content-Disposition", new StringValues("attachment; filename=stats.csv"));
                    context.Response.ContentType = "text/csv";
                    await context.Response.WriteAsync(sb.ToString());
                });

                endpoints.MapGet("/stats.db", async context =>
                {
                    await context.Response.SendFileAsync(StatisticsContext.Path);
                });
            });
        }
    }

    public class StatisticsCollector : BackgroundService
    {
        private readonly IOptions<Settings> options;

        public StatisticsCollector(IOptions<Settings> options)
        {
            this.options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            SpeedTest.Net.Models.Server? server = null;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    server ??= await SpeedTest.Net.SpeedTestClient.GetServer();

                    using (var db = new StatisticsContext())
                    {
                        var dbServer = await db.FindAsync<Server>(server.Id);
                        var now = DateTime.UtcNow;
                        if (dbServer == null)
                        {
                            dbServer = new Server
                            {
                                Id = server.Id,
                                Host = server.Host,
                                Latitude = server.Latitude,
                                Longitude = server.Longitude
                            };

                            db.Add(dbServer);
                        }

                        if (!stoppingToken.IsCancellationRequested)
                        {
                            var speed = await SpeedTest.Net.SpeedTestClient.GetDownloadSpeed(server);
                            db.Add(new Statistic
                            {
                                Date = now,
                                Type = DataType.DownloadSpeed,
                                Value = speed.Speed
                            });
                            await db.SaveChangesAsync();
                        }

                        if (!stoppingToken.IsCancellationRequested)
                        {
                            var speed = await SpeedTest.Net.SpeedTestClient.GetUploadSpeed(server);
                            db.Add(new Statistic
                            {
                                Date = now,
                                Type = DataType.UploadSpeed,
                                Value = speed.Speed
                            });
                            await db.SaveChangesAsync();
                        }

                        if (!stoppingToken.IsCancellationRequested)
                        {
                            var latancy = await SpeedTest.Net.SpeedTestClient.GetLatancy(server);
                            db.Add(new Statistic
                            {
                                Date = now,
                                Type = DataType.Latancy,
                                Value = latancy
                            });
                            await db.SaveChangesAsync();
                        }
                    }

                }
                catch (Exception ex)
                {
                    server = null;

                    using (var db = new StatisticsContext())
                    {
                        db.Events.Add(new Event
                        {
                            Date = DateTime.UtcNow,
                            Title = ex.Message,
                            Details = ex.ToString()
                        });
                        await db.SaveChangesAsync();
                    }
                }
                // should be able to change this setting!!!
                await Task.Delay(options.Value.Frequancy);
            }
        }
    }
}
