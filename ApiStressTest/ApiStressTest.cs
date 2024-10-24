using System.Diagnostics;
using Newtonsoft.Json;

namespace MealPlaner.Tests
{
    public class ApiStressTest
    {
        private readonly string _baseUrl;
        private readonly HttpClient _client;
        private readonly List<TestResult> _results;
        private string _jwtToken;

        public ApiStressTest(string baseUrl)
        {
            _baseUrl = baseUrl;
            _client = new HttpClient();
            _results = new List<TestResult>();
        }

        public class TestResult
        {
            public int ConcurrentUsers { get; set; }
            public double AverageResponseTime { get; set; }
            public int SuccessfulRequests { get; set; }
            public int FailedRequests { get; set; }
            public double RequestsPerSecond { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public async Task RunLoadTest(
            string endpoint,
            int concurrentUsers,
            int requestsPerUser,
            HttpMethod method,
            bool requiresAuth=true,
            object payload = null)
        {
            Console.WriteLine($"Starting load test with {concurrentUsers} concurrent users...");
            var tasks = new List<Task>();
            var successCount = 0;
            var failCount = 0;
            var responseTimes = new List<double>();
            var sw = Stopwatch.StartNew();
            if (requiresAuth) 
            {
                SetAuthToken("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1lIjoic2tvcmMiLCJVc2VySWQiOiIyNyIsInN1YnNjcmlwdGlvbiI6InN0cmluZyIsImV4cCI6MTcyOTg1MDc0MywiaXNzIjoic29tZUd1eSIsImF1ZCI6Im15U3BlY2lhbHNpdGUifQ.0U5KR_XiA2qXOWfxztLkZJp_5yhdhejPPuQADceayyE");
            }

            for (int i = 0; i < concurrentUsers; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < requestsPerUser; j++)
                    {
                        try
                        {
                            var requestSw = Stopwatch.StartNew();
                            var response = await SendRequest(endpoint, method, payload);
                            requestSw.Stop();

                            if (response.IsSuccessStatusCode)
                            {
                                Interlocked.Increment(ref successCount);
                                lock (responseTimes)
                                {
                                    responseTimes.Add(requestSw.ElapsedMilliseconds);
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref failCount);
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failCount);
                            Console.WriteLine($"Request failed: {ex.Message}");
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            var result = new TestResult
            {
                ConcurrentUsers = concurrentUsers,
                AverageResponseTime = responseTimes.Count > 0 ? responseTimes.Average() : 0,
                SuccessfulRequests = successCount,
                FailedRequests = failCount,
                RequestsPerSecond = (successCount + failCount) / (sw.ElapsedMilliseconds / 1000.0),
                Timestamp = DateTime.UtcNow
            };

            _results.Add(result);
            PrintTestResult(result);
        }
        public void SetAuthToken(string token)
        {
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        private async Task<HttpResponseMessage> SendRequest(string endpoint, HttpMethod method, object payload)
        {
            var request = new HttpRequestMessage(method, $"{_baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}");

            if (payload != null && (method == HttpMethod.Post || method == HttpMethod.Put))
            {
                request.Content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    System.Text.Encoding.UTF8,
                    "application/json");
            }
            var response = await _client.SendAsync(request);
            Console.WriteLine(response);
            return response;
        }

        private void PrintTestResult(TestResult result)
        {
            Console.WriteLine("\nTest Results:");
            Console.WriteLine($"Timestamp: {result.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Concurrent Users: {result.ConcurrentUsers}");
            Console.WriteLine($"Average Response Time: {result.AverageResponseTime:F2}ms");
            Console.WriteLine($"Successful Requests: {result.SuccessfulRequests}");
            Console.WriteLine($"Failed Requests: {result.FailedRequests}");
            Console.WriteLine($"Requests Per Second: {result.RequestsPerSecond:F2}");
        }
    }
}
