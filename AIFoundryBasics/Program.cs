using System.Diagnostics;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Agents.AI;

var projectEndPoint = "https://ais-anabelle6.services.ai.azure.com/api/projects/SDKTest";
var deploymentName = "gpt-4o";

const string agentName = "IT Agent";
const string instruction = "You are an IT support agent. Answer the user's questions to the best of your ability.";

PersistentAgentsClient client = new(projectEndPoint, new DefaultAzureCredential());

PersistentAgent agent = client.Administration.CreateAgent(

    model: deploymentName,
    name: agentName,
    instructions: instruction,
    tools: [new CodeInterpreterToolDefinition()]
);

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