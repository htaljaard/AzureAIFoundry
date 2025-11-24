using System.Diagnostics;
using Azure.AI.Agents.Persistent;
using Azure.Identity;

var projectEndPoint = Environment.GetEnvironmentVariable("PROJECT_ENDPOINT") ?? throw new ArgumentNullException("PROJECT_ENDPOINT");
var bingConnectionId = System.Environment.GetEnvironmentVariable("BING_CONNECTION_ID") ?? throw new ArgumentNullException("BING_CONNECTION_ID");
var deploymentName = "gpt-4o";

const string agentName = "Politics Specialist Agent - Code First";
const string instruction = "You are a politics expert. Answer the user's questions to the best of your ability.";


PersistentAgentsClient client = new(projectEndPoint, new DefaultAzureCredential());

BingGroundingToolDefinition bingGroundingTool = new(
    new BingGroundingSearchToolParameters(
        [new BingGroundingSearchConfiguration(bingConnectionId)]
    )
);

PersistentAgent agent = client.Administration.CreateAgent(

    model: deploymentName,
    name: agentName,
    instructions: instruction,
    tools: [bingGroundingTool]
);

PersistentAgentThread thread = await client.Threads.CreateThreadAsync();

client.Messages.CreateMessage(
    threadId: thread.Id,
    role: MessageRole.User,
    content: "Who is the president of the United States?"
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

if (run.Status == RunStatus.Failed)
{
    Console.WriteLine($"Run failed: {run.LastError?.Message}");
    return;
}

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
