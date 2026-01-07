var builder = DistributedApplication.CreateBuilder(args);

// Spin up a Redis container named 'cache'
var cache = builder.AddRedis("cache");

// Spin up the AOT API and give it the Redis reference
var api = builder.AddProject<Projects.VikingAir_Api>("api")
       .WithReference(cache)
       .WithExternalHttpEndpoints();

// Add the React/Vite frontend as an npm app
var web = builder.AddNpmApp("web", "../VikingAir.Web")
    .WithReference(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
