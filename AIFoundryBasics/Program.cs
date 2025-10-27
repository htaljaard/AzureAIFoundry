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
    .AddConsoleExporter()
    .AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = connectionString;
    })
    .Build();


var tracer = tracerProvider.GetTracer(telemetrySource);


using var span = tracer.StartActiveSpan("Create and Run AI Agent", SpanKind.Client);

span.AddEvent("Starting to create and run AI Agent");
AIAgent agent = persistentAgentClient.CreateAIAgent(
            name: agentName,
            instructions: instruction,
            model: deploymentName
            ) .AsBuilder()
            .UseOpenTelemetry(sourceName: telemetrySource)
            .Build();


span.AddEvent("AI Agent created successfully");
span.AddEvent("Running AI Agent");
var response = await agent.RunAsync("My computer is running slow. Can you help me fix it?");

span.AddEvent("AI Agent run completed");