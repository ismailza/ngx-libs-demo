namespace Demo.Api.Infrastructure;

/// <summary>The page shape <c>ApiService.getPage()</c> reads: <c>{ content, page: { number, size, … } }</c>.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Content, PageInfo Page);

public sealed record PageInfo(int Number, int Size, int TotalElements, int TotalPages);

/// <summary>Zero-based page request, clamped to sane bounds.</summary>
public readonly record struct PageRequest(int Page, int Size)
{
    // The page cap keeps Page * Size far from int overflow.
    public static PageRequest From(int? page, int? size) =>
        new(Math.Clamp(page ?? 0, 0, 100_000), Math.Clamp(size ?? 10, 1, 100));

    public int Skip => Page * Size;

    public PagedResponse<T> ToResponse<T>(IReadOnlyList<T> content, int totalElements) =>
        new(content, new PageInfo(Page, Size, totalElements, (int)Math.Ceiling(totalElements / (double)Size)));
}
