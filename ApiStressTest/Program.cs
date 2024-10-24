using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using MealPlaner.Tests;
using Newtonsoft.Json;

class Program
{
    static async Task Main(string[] args)
    {
        // Load configuration
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath("C:\\Users\\marti\\source\\repos\\MealPlaner\\ApiStressTest")
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        string apiBaseUrl = configuration["ApiSettings:BaseUrl"];

        Console.WriteLine("API Stress Test Starting...");
        Console.WriteLine($"Testing API at: {apiBaseUrl}");
        Console.WriteLine("------------------------");

        var stressTest = new ApiStressTest(apiBaseUrl);

        try
        {
            // Test different scenarios
            //await RunTestScenario(stressTest, "Light Load Test - GET Users", HttpMethod.Get, "api/Recipe/GenerateMealPlan", 10, 10,true);
            //await RunTestScenario(stressTest, "Medium Load Test - GET Users", HttpMethod.Get, "api/users", 25, 20,false);

            // Example POST test
            var sampleUser = new
            {
                username = "testuser",
                email = "test@example.com",
                name = "Test User"
            };

            var jsonPayload = File.ReadAllText("C:\\Users\\marti\\source\\repos\\MealPlaner\\ApiStressTest\\payload.json");
            var payload = JsonConvert.DeserializeObject<object>(jsonPayload);
            await RunTestScenario(stressTest, "POST User Test", HttpMethod.Post, "api/Recipe/GenerateMealPlan", 5, 5,true, payload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError occurred: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine("\nTests completed. Press any key to exit...");
        Console.ReadKey();
    }
    private static async Task RunTestScenario(
        ApiStressTest stressTest,
        string scenarioName,
        HttpMethod method,
        string endpoint,
        int concurrentUsers,
        int requestsPerUser,
        bool requiresAuth,
        object payload = null)
    {
        Console.WriteLine($"\nRunning Scenario: {scenarioName}");
        Console.WriteLine($"Method: {method}, Endpoint: {endpoint}");
        Console.WriteLine($"Users: {concurrentUsers}, Requests per user: {requestsPerUser}");

        await stressTest.RunLoadTest(
            endpoint: endpoint,
            concurrentUsers: concurrentUsers,
            requestsPerUser: requestsPerUser,
            method: method,
            requiresAuth,
            payload: payload
        );
    }
}