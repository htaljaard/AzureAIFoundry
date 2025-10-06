using System.Diagnostics;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var projectEndPoint = "https://ais-anabelle6.services.ai.azure.com/api/projects/SDKTest";
var deploymentName = "gpt-4o";

const string agentName = "IT Agent";
const string instruction = "You are an IT support agent. Answer the user's questions to the best of your ability.";

Environment.SetEnvironmentVariable("AZURE_TRACING_GEN_AI_CONTENT_RECORDING_ENABLED ", "true");

AIProjectClient projectClient = new(new Uri(projectEndPoint), new DefaultAzureCredential());

var connectionString = await projectClient.Telemetry.GetApplicationInsightsConnectionStringAsync();

var resourceAttributes = new Dictionary<string, object> {
    { "service.name", "ai-foundry-demo" },
    { "service.namespace", "asi.azure.foundry" },
    { "service.instance.id", "agent.demo" },
    { "gen_ai.provider.name","openai"},

};

var resourceBuilder = ResourceBuilder.CreateDefault().AddAttributes(resourceAttributes);

using var traceProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("ASI.AZURE.AI.AGENT.DEMO")
    .SetResourceBuilder(resourceBuilder)
    .AddConsoleExporter()
    .AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = connectionString;
    }).Build();

var tracer = traceProvider.GetTracer("ASI.AZURE.AI.AGENT.DEMO");

using var span = tracer.StartRootSpan("gen_ai.start_agent", SpanKind.Client,
    initialAttributes: new SpanAttributes(
        new Dictionary<string, object?>
        {
            ["gen_ai.model.deployment"] = deploymentName,
            ["gen_ai.model.provider"] = "azure",
            ["gen_ai.model.family"] = "gpt-4o",
            ["gen_ai.model.version"] = "1.0.0",
            ["gen_ai.tool.code_interpreter"] = true
        }
    ));
span.AddEvent("Creating Persistent Agent Client");

PersistentAgentsClient client = projectClient.GetPersistentAgentsClient(); // You could create a new persitent agent client for each agent if you want to use different agents with different instructions
PersistentAgent agent = client.Administration.CreateAgent(

    model: deploymentName,
    name: agentName,
    instructions: instruction,
    tools: [new CodeInterpreterToolDefinition()]
);

using var agentSpan = tracer.StartActiveSpan("gen_ai.create_agent", SpanKind.Client,
    initialAttributes: new SpanAttributes(
        new Dictionary<string, object?>
        {
            ["gen_ai.agent.id"] = agent.Id,
            ["gen_ai.agent.name"] = agent.Name,
            ["gen_ai.agent.instructions"] = agent.Instructions,
            ["gen_ai.agent.tools"] = string.Join(",", agent.Tools)
        }
    ));

PersistentAgentThread thread = await client.Threads.CreateThreadAsync();

client.Messages.CreateMessage(
    threadId: thread.Id,
    role: MessageRole.User,
    content: "Draw me a pie chart split 50 50"
);

ThreadRun run = client.Runs.CreateRun(thread.Id, agent.Id);

do
{
    Thread.Sleep(TimeSpan.FromMilliseconds(500));
    run = client.Runs.GetRun(thread.Id, run.Id);
}
while (run.Status == RunStatus.Queued
    || run.Status == RunStatus.InProgress
    || run.Status == RunStatus.RequiresAction);


var messages = client.Messages.GetMessages(
    threadId: thread.Id,
    order: ListSortOrder.Ascending
);

using var messagesSpan = tracer.StartActiveSpan("Process Messages");

foreach (PersistentThreadMessage threadMessage in messages)
{
    foreach (MessageContent content in threadMessage.ContentItems)
    {
        switch (content)
        {
            case MessageTextContent textItem:
                Console.WriteLine($"[{threadMessage.Role}]: {textItem.Text}");
                break;
            case MessageImageFileContent imageFileContent:
                Console.WriteLine($"[{threadMessage.Role}]: Image content file ID = {imageFileContent.FileId}");
                BinaryData imageContent = client.Files.GetFileContent(imageFileContent.FileId);
                string tempFilePath = Path.Combine(AppContext.BaseDirectory, $"{Guid.NewGuid()}.png");
                File.WriteAllBytes(tempFilePath, imageContent.ToArray());
                client.Files.DeleteFile(imageFileContent.FileId);

                ProcessStartInfo psi = new()
                {
                    FileName = tempFilePath,
                    UseShellExecute = true
                };
                Process.Start(psi);
                break;
        }
    }
}