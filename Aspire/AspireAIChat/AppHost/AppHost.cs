var builder = DistributedApplication.CreateBuilder(args);

var projectEndPoint = builder.AddParameter("AI-Foundry-Project-EndPoint");

var api = builder.AddProject<Projects.ChatAPI>("api").WithEnvironment("PROJECT_ENDPOINT", projectEndPoint);

builder.Build().Run();
