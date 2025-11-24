using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0110

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddServiceDefaults();


var projectEndPoint = builder.Configuration["PROJECT_ENDPOINT"] ?? throw new ArgumentNullException($"PROJECT_ENDPOINT");
var deploymentName = "gpt-4o";

var app = builder.Build();


app.MapPost("/api/chat", async (ChatRequest request) =>
{
    PersistentAgentsClient agentClient = new(projectEndPoint, new AzureCliCredential());

    const string agentName = "Politics Specialist Agent - Code First";
    const string instruction = "You are a politics expert. Answer the user's questions to the best of your ability.";

    PersistentAgent definition = await agentClient.Administration.CreateAgentAsync(
        model: deploymentName,
        name: agentName,
        description: instruction,
        instructions: instruction);

    AzureAIAgent agent = new(definition, agentClient);

    AzureAIAgentThread agentThread = new(agentClient);

    ChatMessageContent message = new(AuthorRole.User, request.Message);
    try
    {
        string responseText = "";

        await foreach (ChatMessageContent response in agent.InvokeAsync(message, agentThread))
        {
            responseText += response.Content;
        }

        return TypedResults.Ok(responseText);
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
        throw;
    }

    await agent.Client.Administration.DeleteAgentAsync(agent.Id);
}).WithName("Chat");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapDefaultEndpoints();


app.Run();


public record ChatRequest(string Message, string? ThreadId);