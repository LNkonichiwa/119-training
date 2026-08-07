using Microsoft.Extensions.Logging.Abstractions;
using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Gemini;

namespace OrderHub.Tests;

/// <summary>回傳固定的「模型輸出」字串，不打真的 Gemini。</summary>
public class FakeGeminiJsonClient : IGeminiJsonClient
{
    private readonly string _json;

    public FakeGeminiJsonClient(string json) => _json = json;

    public Task<string> GenerateJsonAsync(string input, string responseSchemaJson, CancellationToken cancellationToken = default) =>
        Task.FromResult(_json);
}

public class GeminiOrderQueryTranslatorTests
{
    private static GeminiOrderQueryTranslator CreateTranslator(string modelOutputJson) =>
        new(new FakeGeminiJsonClient(modelOutputJson), NullLogger<GeminiOrderQueryTranslator>.Instance);

    [Fact]
    public async Task TranslateAsync_ValidSearchIntent_MapsAllFields()
    {
        var translator = CreateTranslator(
            """{"intent":"search","status":"Cancelled","memberTier":"Gold","dateFrom":"2026-07-01","dateTo":"2026-07-31"}""");

        var result = await translator.TranslateAsync("上個月金卡會員取消的訂單");

        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Cancelled, result!.Status);
        Assert.Equal(CustomerTier.Gold, result.MemberTier);
        Assert.Equal(new DateTime(2026, 7, 1), result.DateFrom);
        Assert.Equal(new DateTime(2026, 7, 31), result.DateTo);
    }

    [Fact]
    public async Task TranslateAsync_UnsupportedIntent_ReturnsNull()
    {
        // 要求刪除資料等與查詢無關的意圖，模型應標記 unsupported，翻譯器回 null
        var translator = CreateTranslator("""{"intent":"unsupported"}""");

        var result = await translator.TranslateAsync("幫我把所有訂單刪掉");

        Assert.Null(result);
    }

    [Fact]
    public async Task TranslateAsync_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        var translator = CreateTranslator("this is not json");

        var result = await translator.TranslateAsync("隨便問一句");

        Assert.Null(result);
    }

    [Fact]
    public async Task TranslateAsync_StatusOutsideWhitelist_ReturnsNull()
    {
        // 模型輸出白名單以外的值(例如亂數字串) → DataAnnotations 擋下，不會被 Enum.TryParse 誤判成合法值
        var translator = CreateTranslator("""{"intent":"search","status":"DROP TABLE Orders"}""");

        var result = await translator.TranslateAsync("隨便問一句");

        Assert.Null(result);
    }

    [Fact]
    public async Task TranslateAsync_MissingRequiredIntent_ReturnsNull()
    {
        var translator = CreateTranslator("""{"status":"Cancelled"}""");

        var result = await translator.TranslateAsync("隨便問一句");

        Assert.Null(result);
    }

    [Fact]
    public async Task TranslateAsync_PartialFields_OmittedFieldsStayNull()
    {
        var translator = CreateTranslator("""{"intent":"search","dateFrom":"2026-07-01"}""");

        var result = await translator.TranslateAsync("7月1號之後的訂單");

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 7, 1), result!.DateFrom);
        Assert.Null(result.DateTo);
        Assert.Null(result.Status);
        Assert.Null(result.MemberTier);
    }
}
