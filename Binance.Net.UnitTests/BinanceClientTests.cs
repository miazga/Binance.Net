using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Binance.Net.Clients;
using Binance.Net.Clients.SpotApi;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects;
using Binance.Net.Objects.Models.Spot;
using Binance.Net.UnitTests.TestImplementations;
using CryptoExchange.Net;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Requests;
using CryptoExchange.Net.Sockets;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Binance.Net.UnitTests;

[TestFixture]
public class BinanceClientTests
{
    [TestCase(1508837063996)]
    [TestCase(1507156891385)]
    public async Task GetServerTime_Should_RespondWithServerTimeDateTime(long milisecondsTime)
    {
        // arrange
        DateTime expected = new DateTime(1970, 1, 1).AddMilliseconds(milisecondsTime);
        BinanceCheckTime time = new() { ServerTime = expected };
        IBinanceClient client = TestHelpers.CreateResponseClient(JsonConvert.SerializeObject(time));

        // act
        WebCallResult<DateTime> result = await client.SpotApi.ExchangeData.GetServerTimeAsync();

        // assert
        Assert.AreEqual(true, result.Success);
        Assert.AreEqual(expected, result.Data);
    }

    [TestCase]
    public async Task StartUserStream_Should_RespondWithListenKey()
    {
        // arrange
        BinanceListenKey key = new() { ListenKey = "123" };

        IBinanceClient client = TestHelpers.CreateResponseClient(key,
            new BinanceClientOptions
            {
                ApiCredentials = new ApiCredentials("Test", "Test"),
                SpotApiOptions = new BinanceApiClientOptions { AutoTimestamp = false }
            });

        // act
        WebCallResult<string> result = await client.SpotApi.Account.StartUserStreamAsync();

        // assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(key.ListenKey == result.Data);
    }

    [TestCase]
    public async Task KeepAliveUserStream_Should_Respond()
    {
        // arrange
        IBinanceClient client = TestHelpers.CreateResponseClient("{}",
            new BinanceClientOptions
            {
                ApiCredentials = new ApiCredentials("Test", "Test"),
                SpotApiOptions = new BinanceApiClientOptions { AutoTimestamp = false }
            });

        // act
        WebCallResult<object> result = await client.SpotApi.Account.KeepAliveUserStreamAsync("test");

        // assert
        Assert.IsTrue(result.Success);
    }

    [TestCase]
    public async Task StopUserStream_Should_Respond()
    {
        // arrange
        IBinanceClient client = TestHelpers.CreateResponseClient("{}",
            new BinanceClientOptions
            {
                ApiCredentials = new ApiCredentials("Test", "Test"),
                SpotApiOptions = new BinanceApiClientOptions { AutoTimestamp = false }
            });

        // act
        WebCallResult<object> result = await client.SpotApi.Account.StopUserStreamAsync("test");

        // assert
        Assert.IsTrue(result.Success);
    }

    [TestCase]
    public async Task EnablingAutoTimestamp_Should_CallServerTime()
    {
        // arrange
        IBinanceClient client = TestHelpers.CreateResponseClient("{}",
            new BinanceClientOptions
            {
                ApiCredentials = new ApiCredentials("Test", "Test"),
                SpotApiOptions = new BinanceApiClientOptions { AutoTimestamp = true }
            });

        // act
        try
        {
            await client.SpotApi.Trading.GetOpenOrdersAsync();
        }
        catch (Exception)
        {
            // Exception is thrown because stream is being read twice, doesn't happen normally
        }


        // assert
        Mock.Get(client.RequestFactory)
            .Verify(
                f => f.Create(It.IsAny<HttpMethod>(), It.Is<Uri>(uri => uri.ToString().Contains("/time")),
                    It.IsAny<int>()), Times.Exactly(2));
    }

    [TestCase]
    public async Task ReceivingBinanceError_Should_ReturnBinanceErrorAndNotSuccess()
    {
        // arrange
        IBinanceClient client = TestHelpers.CreateClient();
        TestHelpers.SetErrorWithResponse(client, "{\"msg\": \"Error!\", \"code\": 123}", HttpStatusCode.BadRequest);

        // act
        WebCallResult<DateTime> result = await client.SpotApi.ExchangeData.GetServerTimeAsync();

        // assert
        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.Error);
        Assert.IsTrue(result.Error.Code == 123);
        Assert.IsTrue(result.Error.Message == "Error!");
    }

    [Test]
    public void ProvidingApiCredentials_Should_SaveApiCredentials()
    {
        // arrange
        // act
        BinanceAuthenticationProvider authProvider = new(new ApiCredentials("TestKey", "TestSecret"));

        // assert
        Assert.AreEqual(authProvider.Credentials.Key.GetString(), "TestKey");
        Assert.AreEqual(authProvider.Credentials.Secret.GetString(), "TestSecret");
    }

    [Test]
    public void AddingAuthToRequest_Should_AddApiKeyHeader()
    {
        // arrange
        BinanceAuthenticationProvider authProvider = new(new ApiCredentials("TestKey", "TestSecret"));
        HttpClient client = new();
        Request request = new(new HttpRequestMessage(HttpMethod.Get, "https://test.test-api.com"), client, 1);

        // act
        Dictionary<string, string> headers = new();
        authProvider.AuthenticateRequest(null, request.Uri, HttpMethod.Get, new Dictionary<string, object>(), true,
            ArrayParametersSerialization.MultipleValues,
            HttpMethodParameterPosition.InUri, out SortedDictionary<string, object> uriParameters,
            out SortedDictionary<string, object> bodyParameters, out headers);

        // assert
        Assert.IsTrue(headers.First().Key == "X-MBX-APIKEY" && headers.First().Value == "TestKey");
    }

    [TestCase("BTCUSDT", true)]
    [TestCase("NANOUSDT", true)]
    [TestCase("NANOAUSDTA", true)]
    [TestCase("NANOBTC", true)]
    [TestCase("ETHBTC", true)]
    [TestCase("BEETC", true)]
    [TestCase("EETC", false)]
    [TestCase("KP3RBNB", true)]
    [TestCase("BTC-USDT", false)]
    [TestCase("BTC-USD", false)]
    public void CheckValidBinanceSymbol(string symbol, bool isValid)
    {
        if (isValid)
        {
            Assert.DoesNotThrow(symbol.ValidateBinanceSymbol);
        }
        else
        {
            Assert.Throws(typeof(ArgumentException), symbol.ValidateBinanceSymbol);
        }
    }

    [Test]
    public void CheckRestInterfaces()
    {
        Assembly assembly = Assembly.GetAssembly(typeof(BinanceClient));
        string[] ignore = { "IBinanceClientUsdFuturesApi", "IBinanceClientCoinFuturesApi", "IBinanceClientSpotApi" };
        IEnumerable<Type> clientInterfaces = assembly.GetTypes()
            .Where(t => t.Name.StartsWith("IBinanceClient") && !ignore.Contains(t.Name));

        foreach (Type clientInterface in clientInterfaces)
        {
            Type implementation =
                assembly.GetTypes().Single(t => t.IsAssignableTo(clientInterface) && t != clientInterface);
            int methods = 0;
            foreach (MethodInfo method in implementation.GetMethods()
                         .Where(m => m.ReturnType.IsAssignableTo(typeof(Task))))
            {
                MethodInfo interfaceMethod = clientInterface.GetMethod(method.Name,
                    method.GetParameters().Select(p => p.ParameterType).ToArray());
                Assert.NotNull(interfaceMethod,
                    $"Missing interface for method {method.Name} in {implementation.Name} implementing interface {clientInterface.Name}");
                methods++;
            }

            Debug.WriteLine($"{clientInterface.Name} {methods} methods validated");
        }
    }

    [Test]
    public void CheckSocketInterfaces()
    {
        Assembly assembly = Assembly.GetAssembly(typeof(BinanceSocketClientSpotStreams));
        IEnumerable<Type> clientInterfaces = assembly.GetTypes().Where(t => t.Name.StartsWith("IBinanceSocketClient"));

        foreach (Type clientInterface in clientInterfaces)
        {
            Type implementation =
                assembly.GetTypes().Single(t => t.IsAssignableTo(clientInterface) && t != clientInterface);
            int methods = 0;
            foreach (MethodInfo method in implementation.GetMethods().Where(m =>
                         m.ReturnType.IsAssignableTo(typeof(Task<CallResult<UpdateSubscription>>))))
            {
                MethodInfo interfaceMethod = clientInterface.GetMethod(method.Name,
                    method.GetParameters().Select(p => p.ParameterType).ToArray());
                Assert.NotNull(interfaceMethod,
                    $"Missing interface for method {method.Name} in {implementation.Name} implementing interface {clientInterface.GetType().Name}");
                methods++;
            }

            Debug.WriteLine($"{clientInterface.Name} {methods} methods validated");
        }
    }
}