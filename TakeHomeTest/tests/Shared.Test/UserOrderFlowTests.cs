using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Test
{
    public class UserOrderFlowTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly HttpClient _userClient = new()
        {
            BaseAddress = new Uri("http://localhost:5001"),
        };
        private readonly HttpClient _orderClient = new()
        {
            BaseAddress = new Uri("http://localhost:5002"),
        };

        public UserOrderFlowTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private async Task WaitForEndpointAsync(
            HttpClient client,
            string endpoint,
            int timeoutSeconds = 30
        )
        {
            var start = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);

            while (DateTime.UtcNow - start < timeout)
            {
                try
                {
                    var response = await client.GetAsync(endpoint);
                    if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                    // Service might not be ready yet
                }
                await Task.Delay(1000);
            }
            throw new TimeoutException(
                $"Endpoint {endpoint} did not become available within {timeoutSeconds} seconds"
            );
        }

        private async Task WaitForServicesAsync()
        {
            // Wait for both services to be ready by checking their API endpoints
            _output.WriteLine("Waiting for user service...");
            await WaitForEndpointAsync(_userClient, "/api/users", 60);
            _output.WriteLine("User service is ready");

            _output.WriteLine("Waiting for order service...");
            await WaitForEndpointAsync(_orderClient, "/api/orders", 60);
            _output.WriteLine("Order service is ready");
            {
                try
                {
                    // Try health endpoints first
                    var userHealth = await _userClient.GetAsync("/health");
                    var orderHealth = await _orderClient.GetAsync("/health");

                    if (userHealth.IsSuccessStatusCode && orderHealth.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (HttpRequestException ex)
                    when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Health endpoint not found, try API endpoints
                    try
                    {
                        var userApi = await _userClient.GetAsync("/api/users");
                        var orderApi = await _orderClient.GetAsync("/api/orders");

                        if (
                            userApi.StatusCode != System.Net.HttpStatusCode.NotFound
                            && orderApi.StatusCode != System.Net.HttpStatusCode.NotFound
                        )
                        {
                            return;
                        }
                    }
                    catch
                    {
                        // Ignore API endpoint check errors
                    }
                }
                catch
                {
                    // Ignore connection errors while waiting for services
                }
                await Task.Delay(1000);
            }

            throw new TimeoutException(
                "Services did not become available within the timeout period"
            );
        }

        [SkippableFact(DisplayName = "Integration: User-Order Flow Synchronization")]
        public async Task UserOrderFlow_Should_SynchronizeBetweenServices()
        {
            Skip.IfNot(
                string.Equals(
                    Environment.GetEnvironmentVariable("ENABLE_INTEGRATION_TESTS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase
                ),
                "Integration tests are disabled. Set ENABLE_INTEGRATION_TESTS=true to run."
            );
            await WaitForServicesAsync();

            // 1. Create User
            var user = new
            {
                id = "1",
                name = "Alice",
                email = "alice@example.com",
            };
            var userRes = await _userClient.PostAsJsonAsync("/api/users", user);
            userRes.EnsureSuccessStatusCode();

            // Wait a bit for Kafka propagation
            await Task.Delay(2000);

            // 2. Verify OrderService knows about the user
            var usersInOrderService = await _orderClient.GetFromJsonAsync<List<dynamic>>(
                "/api/users"
            );
            Assert.Contains(usersInOrderService, u => (string)u.name == "Alice");

            // 3. Create Order
            var order = new
            {
                id = "1001",
                userId = "1",
                productName = "MacBook Air",
                quantity = 1,
            };
            var orderRes = await _orderClient.PostAsJsonAsync("/api/orders", order);
            orderRes.EnsureSuccessStatusCode();

            // Wait again for Kafka sync
            await Task.Delay(2000);

            // 4. Verify UserService received order
            var ordersInUserService = await _userClient.GetFromJsonAsync<List<dynamic>>(
                "/api/orders"
            );
            Assert.Contains(ordersInUserService, o => (string)o.productName == "MacBook Air");
        }

        public void Dispose()
        {
            _userClient.Dispose();
            _orderClient.Dispose();
        }
    }
}
