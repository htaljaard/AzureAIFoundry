using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var projectEndPoint = "https://ais-anabelle6.services.ai.azure.com/api/projects/demo_project_1";
var deploymentName = "gpt-4o";

const string agentName = "IT Agent";
const string instruction = "You are an IT support agent. Answer the user's questions to the best of your ability.";

const string telemetrySource = "Agent-Framework-Tracing";

AIProjectClient projectClient = new(new Uri(projectEndPoint), new DefaultAzureCredential());
PersistentAgentsClient persistentAgentClient = projectClient.GetPersistentAgentsClient();

string connectionString = await projectClient.Telemetry.GetApplicationInsightsConnectionStringAsync();

ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(telemetrySource);

AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(telemetrySource)
    .AddSource("Microsoft.SemanticKernel*")
    .AddConsoleExporter()
    .AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = connectionString;
    })
    .Build();

using var logger = LoggerFactory.Create(builder =>
{
    builder
    .AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(resourceBuilder)
        .AddAzureMonitorLogExporter(o => o.ConnectionString = connectionString);
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    })
        .SetMinimumLevel(LogLevel.Debug);
});


var kernelBuilder = Kernel.CreateBuilder()

    .AddAzureOpenAIChatCompletion(
        deploymentName: deploymentName,
        endpoint: "https://ais-anabelle6.openai.azure.com/",
        apiKey: "77bd0808c8ac4ffeba4e833786044dd2"
    );
    
kernelBuilder.Services.AddSingleton(logger);

var kernel = kernelBuilder.Build();

Console.WriteLine(await kernel.InvokePromptAsync("Hello World!"));