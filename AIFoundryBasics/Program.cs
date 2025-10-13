using System.Diagnostics;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
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

using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()    
    .SetResourceBuilder(resourceBuilder)
    .AddSource(telemetrySource)
    .AddOtlpExporter()
    .AddConsoleExporter()
    .AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = connectionString;
    })
    .Build();

ActivitySource source = new(telemetrySource);

using var activity = source.StartActivity("Create and Run AI Agent", ActivityKind.Client);

AIAgent agent = persistentAgentClient.CreateAIAgent(
            name: agentName,
            instructions: instruction,
            model: deploymentName
            );
            // .AsBuilder()
            // .UseOpenTelemetry(telemetrySource)
            // .Build();


using (var activity2 = source.StartActivity("Run AI Agent", ActivityKind.Client))
{

    var response = await agent.RunAsync("My computer is running slow. Can you help me fix it?");

    using (var activity3 = source.StartActivity("Run Complete", ActivityKind.Client))
    {
        activity3?.SetTag("agent.response", response.ToString());
    }

};

tracerProvider.Shutdown();