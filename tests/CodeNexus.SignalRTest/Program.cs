using Microsoft.AspNetCore.SignalR.Client;

Console.WriteLine("SignalR Learning Path Test Console");

var baseUrl = "https://localhost:7001"; // Adjust your API URL
var token = "your-jwt-token-here";      // Replace with actual token

// Create connections
var learningPathConnection = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/hubs/learningpath", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(token);
    })
    .WithAutomaticReconnect()
    .Build();

var chapterConnection = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/hubs/chapter", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(token);
    })
    .WithAutomaticReconnect()
    .build();

// Setup event handlers
learningPathConnection.On<object>("LearningPathGenerationStarted", () =>
{
    Console.WriteLine("🚀 Learning path generation started");
});

learningPathConnection.On<object>("LearningPathCreated", (data) =>
{
    Console.WriteLine($"✅ Learning path created: {data}");
});

learningPathConnection.On<object>("LearningPathGenerationCompleted", (data) =>
{
    Console.WriteLine($"🎉 Learning path generation completed: {data}");
});

learningPathConnection.On<object>("LearningPathGenerationError", (error) =>
{
    Console.WriteLine($"❌ Learning path generation error: {error}");
});

chapterConnection.On<object>("ChapterSkeletonLoading", (data) =>
{
    Console.WriteLine($"⏳ Chapter skeleton loading: {data}");
});

chapterConnection.On<object>("ReceiveChapterSkeleton", (data) =>
{
    Console.WriteLine($"✅ Chapter skeleton received: {data}");
});

try
{
    // Start connections
    Console.WriteLine("Connecting to SignalR hubs...");
    await learningPathConnection.StartAsync();
    await chapterConnection.StartAsync();
    Console.WriteLine("✅ Connected to all hubs");

    // Test learning path generation
    Console.WriteLine("\nTesting learning path generation...");
    await learningPathConnection.InvokeAsync("RequestLearningPathGeneration",
        Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), // Replace with real subject ID
        Guid.Parse("550e8400-e29b-41d4-a716-446655440001"), // Replace with real goal ID
        "Beginner",
        "VietNamese"
    );

    // Wait for completion
    Console.WriteLine("Waiting for results... (Press any key to exit)");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}
finally
{
    await learningPathConnection.DisposeAsync();
    await chapterConnection.DisposeAsync();
}