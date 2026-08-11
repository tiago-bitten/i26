using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

/// <summary>Sample id in the canonical shape, with the <c>usr</c> prefix.</summary>
public readonly record struct TestUserId(Guid Value) : ITypedId<TestUserId>
{
    public static string Prefix => "usr";

    public static TestUserId FromGuid(Guid value) => new(value);

    public static TestUserId New() => TypedId.New<TestUserId>();

    public override string ToString() => TypedId.Format(this);

    public static TestUserId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<TestUserId>(s);

    public static bool TryParse(string? s, IFormatProvider? _, out TestUserId result)
        => TypedId.TryParse(s, out result);
}

/// <summary>Sample id in the canonical shape, with the <c>ord</c> prefix.</summary>
public readonly record struct TestOrderId(Guid Value) : ITypedId<TestOrderId>
{
    public static string Prefix => "ord";

    public static TestOrderId FromGuid(Guid value) => new(value);

    public static TestOrderId New() => TypedId.New<TestOrderId>();

    public override string ToString() => TypedId.Format(this);

    public static TestOrderId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<TestOrderId>(s);

    public static bool TryParse(string? s, IFormatProvider? _, out TestOrderId result)
        => TypedId.TryParse(s, out result);
}

/// <summary>
/// Sample id referencing another microservice: same shape, but no <c>New()</c> — only the service
/// that owns the prefix mints ids with it. Its prefix is four characters, so it also stands for the
/// extended rule.
/// </summary>
public readonly record struct TestExternalAuthId(Guid Value) : ITypedId<TestExternalAuthId>
{
    public static string Prefix => "auth";

    public static bool UsesExtendedPrefix => true;

    public static TestExternalAuthId FromGuid(Guid value) => new(value);

    public override string ToString() => TypedId.Format(this);

    public static TestExternalAuthId Parse(string s, IFormatProvider? _ = null)
        => TypedId.Parse<TestExternalAuthId>(s);

    public static bool TryParse(string? s, IFormatProvider? _, out TestExternalAuthId result)
        => TypedId.TryParse(s, out result);
}
