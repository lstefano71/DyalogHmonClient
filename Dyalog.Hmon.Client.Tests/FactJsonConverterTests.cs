using System.Text.Json;
using System.Linq;
using Dyalog.Hmon.Client.Lib;
using Xunit;

namespace Dyalog.Hmon.Client.Tests
{
    public class FactJsonConverterTests
    {
        [Fact]
        public void Deserializes_WorkspaceFact_And_ThreadCountFact()
        {
            var json = @"{
                ""UID"":""abc123"",
                ""Interval"":5000,
                ""Facts"":[
                    {""ID"":3,""Name"":""Workspace"",""Value"":{""WSID"":""CLEAR WS"",""Available"":123,""Used"":456,""Compactions"":1,""GarbageCollections"":2,""GarbagePockets"":3,""FreePockets"":4,""UsedPockets"":5,""Sediment"":6,""Allocation"":7,""AllocationHWM"":8,""TrapReserveWanted"":9,""TrapReserveActual"":10}},
                    {""ID"":6,""Name"":""ThreadCount"",""Value"":{""Total"":2,""Suspended"":0}}
                ]
            }";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new FactJsonConverter());
            var factsResponse = JsonSerializer.Deserialize<FactsResponse>(json, options);

            Assert.NotNull(factsResponse);
            Assert.Equal("abc123", factsResponse!.UID);
            var facts = factsResponse.Facts.ToList();
            Assert.Equal(2, facts.Count);
            Assert.IsType<WorkspaceFact>(facts[0]);
            Assert.IsType<ThreadCountFact>(facts[1]);
            var ws = (WorkspaceFact)facts[0];
            Assert.Equal("CLEAR WS", ws.WSID);
            var tc = (ThreadCountFact)facts[1];
            Assert.Equal(2, tc.Total);
            Assert.Equal(0, tc.Suspended);
        }

        [Fact]
        public void Serializes_And_Deserializes_ThreadCountFact_RoundTrip()
        {
            var fact = new ThreadCountFact(5, 1);
            var factsResponse = new FactsResponse("test-uid", 1000, new[] { fact });
            var options = new JsonSerializerOptions();
            options.Converters.Add(new FactJsonConverter());
            var json = JsonSerializer.Serialize(factsResponse, options);
            var deserialized = JsonSerializer.Deserialize<FactsResponse>(json, options);
            Assert.NotNull(deserialized);
            var facts = deserialized!.Facts.ToList();
            Assert.Single(facts);
            Assert.IsType<ThreadCountFact>(facts[0]);
            var tc = (ThreadCountFact)facts[0];
            Assert.Equal(5, tc.Total);
            Assert.Equal(1, tc.Suspended);
        }
    }
}