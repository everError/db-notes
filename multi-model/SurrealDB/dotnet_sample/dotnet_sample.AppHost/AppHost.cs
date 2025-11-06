var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.dotnet_sample>("dotnet-sample");

builder.Build().Run();
