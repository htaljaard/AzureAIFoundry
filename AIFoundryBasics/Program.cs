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
    .AddService(telemetrySource)
    .AddTelemetrySdk();


TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(telemetrySource)
    .SetResourceBuilder(resourceBuilder)
    .AddConsoleExporter() 
    .AddJaegerExporter(o =>
    {
        o.AgentHost = "localhost";
        o.AgentPort = 6831;
    })
    .AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = connectionString;
    })
    .Build();

var tracer = tracerProvider.GetTracer(telemetrySource);

using var span = tracer.StartActiveSpan("Create and Run AI Agent");

span.AddEvent("Creating AI Agent");

AIAgent agent = persistentAgentClient.CreateAIAgent(
            name: agentName,
            instructions: instruction,
            model: deploymentName
            ).AsBuilder()
            .UseOpenTelemetry(telemetrySource)
            .Build();

span.AddEvent("Running AI Agent");


var response = await agent.RunAsync("My computer is running slow. Can you help me fix it?");

Console.WriteLine(response);

span.AddEvent("AI Agent Run Complete");

tracerProvider.Dispose();



