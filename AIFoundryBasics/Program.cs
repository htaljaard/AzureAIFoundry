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

Environment.SetEnvironmentVariable("AZURE_TRACING_GEN_AI_CONTENT_RECORDING_ENABLED ", "true");

AIProjectClient projectClient = new(new Uri(projectEndPoint), new DefaultAzureCredential());
var connectionString = await projectClient.Telemetry.GetApplicationInsightsConnectionStringAsync();

PersistentAgentsClient client = projectClient.GetPersistentAgentsClient(); // You could create a new persitent agent client for each agent if you want to use different agents with different instructions
PersistentAgent agent = client.Administration.CreateAgent(

    model: deploymentName,
    name: agentName,
    instructions: instruction
// tools: [new CodeInterpreterToolDefinition()]
);

PersistentAgentThread thread = await client.Threads.CreateThreadAsync();


//OTEL
var activitySource = new ActivitySource("AIFoundryBasics");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("AIFoundryBasics"))
    .AddSource("AIFoundryBasics")
    .AddConsoleExporter()
    .AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = connectionString;
    })
    .Build();

Tracer tracer = tracerProvider.GetTracer("AIFoundryBasics");


using (var span = tracer.StartActiveSpan("SPAN - AGENT", SpanKind.Client))
{
    var userInput = "what is the capital of france?";

    var attributes = new KeyValuePair<string, object?>[]
    {
        new("operation_Id", span.Context.SpanId), // Unique trace/operation ID
        new("operation_ParentId", span.ParentSpanId), // Parent span ID
        new("gen_ai.system", agent.Name), // GenAI system/model
        new("gen_ai.agent.name", agent.Name),
        new("gen_ai.agent.id", agent.Id),
        new("gen_ai.provider.name", "openai"),
        new("gen_ai.request.model", deploymentName),
        new("gen_ai.system_instructions", "[{\"type\":\"text\",\"content\":\"You are a helpful assistant.\"}]"),
        new("gen_ai.input.messages", $"[{{\"role\":\"user\",\"parts\":[{{\"type\":\"text\",\"content\":\"{userInput}\"}}]}}]"),
        new("gen_ai.response.id", "response_ID"), // Response identifier
        new("gen_ai.thread.id", thread.Id), // Thread/conversation ID
        new("gen_ai.thread.run.id", "runId"), // Run/execution ID
        new("event.name", "Send User Message"), // Logical event name
        new("gen_ai.event.content", $"[{{\"role\":\"user\",\"parts\":[{{\"type\":\"text\",\"content\":\"{userInput}\"}}]}}]"), // Event content (prompt/completion)
        new("gen_ai.usage.input_tokens", 0), // Input tokens
        new("gen_ai.usage.output_tokens", 0), // Output tokens
        new("gen_ai.evaluator.name", "AIFoundryBasics"), // Evaluator name (if applicable)
        new("gen_ai.evaluation.score", 0), // Evaluation score (if applicable)
        new("gen_ai.evaluation.id", "AIFoundryBasics"), // Evaluation event ID (if applicable)
        new("gen_ai.choice", "user"), // GenAI role (user, assistant, system)
        new("timestamp", DateTime.UtcNow) // Event timestamp
    };

    span.AddEvent(
        name: "AddMessage",
        attributes: new SpanAttributes(attributes)
    );

    client.Messages.CreateMessage(
        threadId: thread.Id,
        role: MessageRole.User,
        content: userInput
    );

    span.AddEvent(
        name: "CreateRun",
        attributes: new SpanAttributes(attributes)
    );

    ThreadRun run = client.Runs.CreateRun(thread.Id, agent.Id);
    do
    {
        Thread.Sleep(TimeSpan.FromMilliseconds(500));
        run = client.Runs.GetRun(thread.Id, run?.Id);
    }
    while (run.Status == RunStatus.Queued
        || run.Status == RunStatus.InProgress
        || run.Status == RunStatus.RequiresAction);

    var messages = client.Messages.GetMessages(
        threadId: thread.Id,
        order: ListSortOrder.Ascending
    );

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

}