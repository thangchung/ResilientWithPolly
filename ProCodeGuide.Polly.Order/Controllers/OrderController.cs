using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Bulkhead;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Retry;
using Polly.Timeout;
using ProCodeGuide.Polly.Order.ViewModels;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ProCodeGuide.Polly.Order.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly ILogger<OrderController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private HttpClient _httpClient;
        private string apiurl = @"http://localhost:5001/";

        private OrderDetails _orderDetails = null;

        private readonly AsyncRetryPolicy _retryPolicy;
        private static AsyncTimeoutPolicy _timeoutPolicy;
        private readonly AsyncFallbackPolicy<string> _fallbackPolicy;
        private static AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
        private static AsyncBulkheadPolicy _bulkheadPolicy;

        public OrderController(ILogger<OrderController> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            _orderDetails = new OrderDetails
            {
                Id = 7261,
                SetupDate = DateTime.Now.AddDays(-10),
                Items = new List<Item>
                {
                    new Item
                    {
                        Id = 6514,
                        Name = ".NET Core Book"
                    }
                }
            };

            _retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync(2, (_, retryCount, _) =>
                {
                    _logger.LogInformation($"{nameof(GetOrderByCustomerWithRetry)} - retrying - {retryCount}");
                });

            _timeoutPolicy = Policy.TimeoutAsync(10, TimeoutStrategy.Pessimistic,
                (_, _, _) =>
                {
                    _logger.LogInformation($"{nameof(GetOrderByCustomerWithTimeout)} - give up after 10 seconds");
                    return Task.CompletedTask;
                });

            _fallbackPolicy = Policy<string>
                                .Handle<Exception>()
                                .FallbackAsync("Customer Name Not Available - Please retry later");

            void OnBreak(Exception exception, TimeSpan timespan, Context context)
            {
                _logger.LogInformation($"{nameof(GetOrderByCustomerWithCircuitBreaker)} - on break");
            }

            void OnReset(Context context)
            {
                _logger.LogInformation($"{nameof(GetOrderByCustomerWithCircuitBreaker)} - on reset");
            }

            _circuitBreakerPolicy ??= Policy.Handle<Exception>()
                .CircuitBreakerAsync(2, TimeSpan.FromSeconds(10),OnBreak, OnReset);

            _bulkheadPolicy = Policy.BulkheadAsync(3, 6, context =>
            {
                _logger.LogInformation($"{nameof(GetOrderByCustomerWithBulkHead)} - bulkhead");
                return Task.CompletedTask;
            });
        }

        [HttpGet]
        [Route("GetOrderByCustomer/{customerCode}")]
        public async Task<OrderDetails> GetOrderByCustomer(int customerCode)
        {
            _httpClient = _httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri(apiurl);
            var uri = "/api/Customer/GetCustomerName/" + customerCode;
            var result = await _httpClient.GetStringAsync(uri);

            _orderDetails.CustomerName = result;

            return _orderDetails;
        }

        [HttpGet]
        [Route("GetOrderByCustomerWithRetry/{customerCode}")]
        public async Task<OrderDetails> GetOrderByCustomerWithRetry(int customerCode)
        {
            _httpClient = _httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri(apiurl);
            var uri = "/api/Customer/GetCustomerNameWithTempFailure/" + customerCode;
            var result = await _retryPolicy.ExecuteAsync(() => _httpClient.GetStringAsync(uri));

            _orderDetails.CustomerName = result;

            return _orderDetails;
        }

        [HttpGet]
        [Route("GetOrderByCustomerWithTimeout/{customerCode}")]
        public async Task<OrderDetails> GetOrderByCustomerWithTimeout(int customerCode)
        {
            try
            {
                _httpClient = _httpClientFactory.CreateClient();
                _httpClient.BaseAddress = new Uri(apiurl);
                var uri = "/api/Customer/GetCustomerNameWithDelay/" + customerCode;
                var result = await _timeoutPolicy.ExecuteAsync(() => _httpClient.GetStringAsync(uri));

                _orderDetails.CustomerName = result;

                return _orderDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Occurred");
                _orderDetails.CustomerName = "Customer Name Not Available as of Now";
                return _orderDetails;
            }
        }

        [HttpGet]
        [Route("GetOrderByCustomerWithFallback/{customerCode}")]
        public async Task<OrderDetails> GetOrderByCustomerWithFallback(int customerCode)
        {
            try
            {
                _httpClient = _httpClientFactory.CreateClient();
                _httpClient.BaseAddress = new Uri(apiurl);
                var uri = "/api/Customer/GetCustomerNameWithPermFailure/" + customerCode;
                var result = await _fallbackPolicy.ExecuteAsync(() => _httpClient.GetStringAsync(uri));

                _orderDetails.CustomerName = result;
                return _orderDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excpetion Occurred");
                _orderDetails.CustomerName = "Customer Name Not Available as of Now";
                return _orderDetails;
            }
        }

        [HttpGet]
        [Route("GetOrderByCustomerWithCircuitBreaker/{customerCode}")]
        public async Task<OrderDetails> GetOrderByCustomerWithCircuitBreaker(int customerCode)
        {
            try
            {
                _httpClient = _httpClientFactory.CreateClient();
                _httpClient.BaseAddress = new Uri(apiurl);
                var uri = "/api/Customer/GetCustomerNameWithPermFailure/" + customerCode;
                var result = await _circuitBreakerPolicy.ExecuteAsync(() => _httpClient.GetStringAsync(uri));

                _orderDetails.CustomerName = result;
                return _orderDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excpetion Occurred");
                _orderDetails.CustomerName = "Customer Name Not Available as of Now";
                return _orderDetails;
            }
        }

        [HttpGet]
        [Route("GetOrderByCustomerWithBulkHead/{customerCode}")]
        public async Task<OrderDetails> GetOrderByCustomerWithBulkHead(int customerCode)
        {
            _httpClient = _httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri(apiurl);
            var uri = "/api/Customer/GetCustomerName/" + customerCode;
            var result = await _bulkheadPolicy.ExecuteAsync(() => _httpClient.GetStringAsync(uri));

            _orderDetails.CustomerName = result;

            return _orderDetails;
        }
    }
}
