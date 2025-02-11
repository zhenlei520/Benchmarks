﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
#if DATABASE
using Npgsql;
#endif

namespace PlatformBenchmarks
{
    public class Program
    {
        public static string[] Args;

        public static async Task Main(string[] args)
        {
            Args = args;

            Console.WriteLine(BenchmarkApplication.ApplicationName);
#if !DATABASE
            Console.WriteLine(BenchmarkApplication.Paths.Plaintext);
            Console.WriteLine(BenchmarkApplication.Paths.Json);
#else
            Console.WriteLine(BenchmarkApplication.Paths.FortunesRaw);
            Console.WriteLine(BenchmarkApplication.Paths.FortunesDapper);
            Console.WriteLine(BenchmarkApplication.Paths.FortunesEf);
            Console.WriteLine(BenchmarkApplication.Paths.SingleQuery);
            Console.WriteLine(BenchmarkApplication.Paths.Updates);
            Console.WriteLine(BenchmarkApplication.Paths.MultipleQueries);
#endif
            DateHeader.SyncDateTimer();

            var host = BuildWebHost(args);
            var config = (IConfiguration)host.Services.GetService(typeof(IConfiguration));
            BatchUpdateString.DatabaseServer = config.Get<AppSettings>().Database;
#if DATABASE
            await BenchmarkApplication.RawDb.PopulateCache();
#endif
            await host.RunAsync();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            Console.WriteLine($"BuildWebHost()");
            Console.WriteLine($"Args: {string.Join(' ', args)}");

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .AddEnvironmentVariables(prefix: "ASPNETCORE_")
                .AddCommandLine(args)
                .Build();

            var appSettings = config.Get<AppSettings>();
#if DATABASE
            Console.WriteLine($"Database: {appSettings.Database}");
            Console.WriteLine($"ConnectionString: {appSettings.ConnectionString}");

            if (appSettings.Database == DatabaseServer.PostgreSql)
            {
                BenchmarkApplication.RawDb = new RawDb(new ConcurrentRandom(), appSettings);
                BenchmarkApplication.DapperDb = new DapperDb(appSettings);
                BenchmarkApplication.EfDb = new EfDb(appSettings);
            }
            else
            {
                throw new NotSupportedException($"Database '{appSettings.Database}' is not supported, check your app settings.");
            }
#endif

            var hostBuilder = new WebHostBuilder()
                .UseBenchmarksConfiguration(config)
                .UseKestrel((context, options) =>
                {
                    var endPoint = context.Configuration.CreateIPEndPoint();

                    options.Listen(endPoint, builder =>
                    {
                        builder.UseHttpApplication<BenchmarkApplication>();
                    });
                })
                .UseStartup<Startup>();

#if NET5_0 || NET6_0_OR_GREATER
            hostBuilder.UseSockets(options =>
            {
                options.WaitForDataBeforeAllocatingBuffer = false;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    options.UnsafePreferInlineScheduling = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS") == "1";
                }
            });
#endif

            var host = hostBuilder.Build();

            return host;
        }
    }
}
