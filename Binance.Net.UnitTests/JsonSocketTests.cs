using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Spot.Socket;
using Binance.Net.UnitTests.TestImplementations;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Binance.Net.UnitTests;

internal class JsonSocketTests
{
    [Test]
    public async Task ValidatAggregatedTradeUpdateStreamJson()
    {
        await TestFileToObject<BinanceStreamAggregatedTrade>(@"JsonResponses/Spot/Socket/AggregatedTradeUpdate.txt");
    }

    [Test]
    public async Task ValidateTradeUpdateStreamJson()
    {
        await TestFileToObject<BinanceStreamTrade>(@"JsonResponses/Spot/Socket/TradeUpdate.txt");
    }

    [Test]
    public async Task ValidateKlineUpdateStreamJson()
    {
        await TestFileToObject<BinanceStreamKlineData>(@"JsonResponses/Spot/Socket/KlineUpdate.txt",
            new List<string> { "B" });
    }

    [Test]
    public async Task ValidateMiniTickUpdateStreamJson()
    {
        await TestFileToObject<BinanceStreamMiniTick>(@"JsonResponses/Spot/Socket/MiniTickUpdate.txt",
            new List<string> { "B" });
    }

    [Test]
    public async Task ValidateBookPriceUpdateStreamJson()
    {
        await TestFileToObject<BinanceStreamBookPrice>(@"JsonResponses/Spot/Socket/BookPriceUpdate.txt",
            new List<string> { "B" });
    }

    [Test]
    public async Task ValidateTickerUpdateStreamJson()
    {
        await TestFileToObject<BinanceStreamTick>(@"JsonResponses/Spot/Socket/TickerUpdate.txt",
            new List<string> { "B" });
    }

    [Test]
    public async Task ValidateUserUpdateStreamJson()
    {
        await TestFileToObject<BinanceStreamOrderUpdate>(@"JsonResponses/Spot/Socket/UserUpdate1.txt",
            new List<string> { "M" });
        await TestFileToObject<BinanceStreamOrderList>(@"JsonResponses/Spot/Socket/UserUpdate2.txt",
            new List<string> { "B" });
        await TestFileToObject<BinanceStreamPositionsUpdate>(@"JsonResponses/Spot/Socket/UserUpdate3.txt",
            new List<string> { "B" });
        await TestFileToObject<BinanceStreamBalanceUpdate>(@"JsonResponses/Spot/Socket/UserUpdate4.txt",
            new List<string> { "B" });
    }

    private static async Task TestFileToObject<T>(string filePath, List<string> ignoreProperties = null)
    {
        EnumValueTraceListener listener = new();
        Trace.Listeners.Add(listener);
        string path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
        string json;
        FileStream file = File.OpenRead(Path.Combine(path, filePath));
        using StreamReader reader = new(file);
        json = await reader.ReadToEndAsync();

        T result = JsonConvert.DeserializeObject<T>(json);
        JsonToObjectComparer<IBinanceSocketClient>.ProcessData("", result, json,
            ignoreProperties: new Dictionary<string, List<string>> { { "", ignoreProperties ?? new List<string>() } });
        Trace.Listeners.Remove(listener);
    }
}

internal class EnumValueTraceListener : TraceListener
{
    public override void Write(string message)
    {
        if (message.Contains("Cannot map"))
        {
            throw new Exception("Enum value error: " + message);
        }
    }

    public override void WriteLine(string message)
    {
        if (message.Contains("Cannot map"))
        {
            throw new Exception("Enum value error: " + message);
        }
    }
}