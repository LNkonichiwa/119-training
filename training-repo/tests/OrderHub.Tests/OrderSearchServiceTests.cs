using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;
using OrderHub.Core.Services;

namespace OrderHub.Tests;

/// <summary>Translator 回傳固定值，不打真的 Gemini。</summary>
public class FakeOrderQueryTranslator : IOrderQueryTranslator
{
    private readonly OrderSearchQuery? _result;

    public FakeOrderQueryTranslator(OrderSearchQuery? result) => _result = result;

    public Task<OrderSearchQuery?> TranslateAsync(string naturalLanguageQuery, CancellationToken cancellationToken = default) =>
        Task.FromResult(_result);
}

/// <summary>只實作 SearchAsync，其他成員這組測試用不到；被呼叫就代表白名單防線放行了。</summary>
public class FakeOrderRepository : IOrderRepository
{
    public bool SearchCalled { get; private set; }

    public Task<IReadOnlyList<Order>> SearchAsync(OrderSearchQuery query)
    {
        SearchCalled = true;
        return Task.FromResult<IReadOnlyList<Order>>(new List<Order> { new() { Id = 1 } });
    }

    public Task<PagedResult<Order>> GetPagedAsync(int page, int pageSize, OrderStatus? status) => throw new NotSupportedException();
    public Task<Order?> GetWithDetailsAsync(int id) => throw new NotSupportedException();
    public Task<IReadOnlyList<Order>> GetByCustomerAsync(int customerId) => throw new NotSupportedException();
    public Task AddAsync(Order order) => throw new NotSupportedException();
    public Task SaveChangesAsync() => throw new NotSupportedException();
}

public class OrderSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_EmptyQuery_FailsWithoutCallingTranslatorOrRepository()
    {
        var repo = new FakeOrderRepository();
        var service = new OrderSearchService(new FakeOrderQueryTranslator(null), repo);

        var result = await service.SearchAsync("   ");

        Assert.False(result.Success);
        Assert.Equal("請輸入查詢內容", result.ErrorMessage);
        Assert.False(repo.SearchCalled);
    }

    [Fact]
    public async Task SearchAsync_TranslatorReturnsNull_RejectsAsUnunderstandable()
    {
        var repo = new FakeOrderRepository();
        var service = new OrderSearchService(new FakeOrderQueryTranslator(null), repo);

        var result = await service.SearchAsync("幫我把所有訂單刪掉");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
        Assert.False(repo.SearchCalled);
    }

    [Fact]
    public async Task SearchAsync_NoFilterParsed_RejectsEvenThoughTranslatorSucceeded()
    {
        // 第二道防線：就算翻譯器沒回 null，沒有任何有效條件也要擋下來
        var repo = new FakeOrderRepository();
        var service = new OrderSearchService(new FakeOrderQueryTranslator(new OrderSearchQuery()), repo);

        var result = await service.SearchAsync("嗨");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
        Assert.False(repo.SearchCalled);
    }

    [Fact]
    public async Task SearchAsync_DateFromAfterDateTo_Rejects()
    {
        var repo = new FakeOrderRepository();
        var query = new OrderSearchQuery { DateFrom = new DateTime(2026, 8, 1), DateTo = new DateTime(2026, 7, 1) };
        var service = new OrderSearchService(new FakeOrderQueryTranslator(query), repo);

        var result = await service.SearchAsync("8月1號到7月1號的訂單");

        Assert.False(result.Success);
        Assert.False(repo.SearchCalled);
    }

    [Fact]
    public async Task SearchAsync_ValidFilter_CallsRepositoryAndReturnsResults()
    {
        var repo = new FakeOrderRepository();
        var query = new OrderSearchQuery { Status = OrderStatus.Cancelled, MemberTier = CustomerTier.Gold };
        var service = new OrderSearchService(new FakeOrderQueryTranslator(query), repo);

        var result = await service.SearchAsync("上個月金卡會員取消的訂單");

        Assert.True(result.Success);
        Assert.True(repo.SearchCalled);
        Assert.Single(result.Value!);
    }
}
