using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

/// <summary>
/// The members a record struct is handed by the compiler and a plain struct is not. Without them a
/// <c>[TypedId]</c> struct falls back to <see cref="ValueType.Equals(object)"/> — reflective,
/// boxing on every dictionary lookup — and <c>left == right</c> does not compile at all.
/// </summary>
public class GeneratedIdEqualityTests
{
    [Fact]
    public void A_plain_struct_id_has_the_equality_operators()
    {
        var guid = Uuid7.New();
        var left = GeneratedStructId.FromGuid(guid);
        var right = GeneratedStructId.FromGuid(guid);

        Assert.True(left == right);
        Assert.False(left != right);
        Assert.False(left == GeneratedStructId.New());
    }

    [Fact]
    public void A_plain_struct_id_is_equatable_without_boxing()
    {
        var guid = Uuid7.New();

        Assert.IsAssignableFrom<IEquatable<GeneratedStructId>>(GeneratedStructId.FromGuid(guid));
        Assert.True(GeneratedStructId.FromGuid(guid).Equals(GeneratedStructId.FromGuid(guid)));
        Assert.Equal(
            GeneratedStructId.FromGuid(guid).GetHashCode(),
            GeneratedStructId.FromGuid(guid).GetHashCode());
    }

    [Fact]
    public void A_plain_struct_id_works_as_a_dictionary_key()
    {
        var id = GeneratedStructId.New();
        var other = GeneratedStructId.New();

        var map = new Dictionary<GeneratedStructId, string> { [id] = "first" };

        Assert.Equal("first", map[GeneratedStructId.FromGuid(id.Value)]);
        Assert.False(map.ContainsKey(other));
    }

    [Fact]
    public void An_id_sorts_without_being_handed_a_comparer()
    {
        var ids = new[] { GeneratedId.New(), GeneratedId.New(), GeneratedId.New() };

        var sorted = ids.Order().ToArray();

        Assert.Equal(ids.OrderBy(id => id.ToString(), StringComparer.Ordinal), sorted);
    }

    [Fact]
    public void A_plain_struct_id_sorts_the_same_way()
    {
        var ids = new[] { GeneratedStructId.New(), GeneratedStructId.New(), GeneratedStructId.New() };

        Assert.Equal(
            ids.OrderBy(id => id.ToString(), StringComparer.Ordinal),
            ids.Order().ToArray());
    }

    [Fact]
    public void The_comparison_operators_agree_with_TypedId_Compare()
    {
        var earlier = GeneratedId.FromGuid(new Guid("01890a5d-ac96-774b-bcce-b302099a8057"));
        var later = GeneratedId.FromGuid(new Guid("01890a5d-ac96-774b-bcce-b302099a8058"));
        var same = GeneratedId.FromGuid(earlier.Value);

        Assert.True(earlier < later);
        Assert.True(earlier <= later);
        Assert.False(earlier > later);
        Assert.False(earlier >= later);

        Assert.True(earlier <= same);
        Assert.True(earlier >= same);
        Assert.False(earlier < same);
        Assert.False(earlier > same);

        Assert.Equal(TypedId.Compare(earlier, later) < 0, earlier < later);
    }

    [Fact]
    public void CompareTo_reads_the_bytes_big_endian_the_way_the_text_sorts()
    {
        // The first three fields of a Guid are little-endian, so Guid.CompareTo disagrees with the
        // order the formatted ids are in. The id has to follow the text, not the Guid.
        var earlier = GeneratedId.FromGuid(new Guid("01000000-0000-7000-8000-000000000000"));
        var later = GeneratedId.FromGuid(new Guid("02000000-0000-7000-8000-000000000000"));

        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(string.CompareOrdinal(earlier.ToString(), later.ToString()) < 0);
    }

    [Fact]
    public void A_sorted_set_keeps_ids_in_creation_order()
    {
        var first = GeneratedId.FromGuid(new Guid("01890a5d-ac96-774b-bcce-000000000001"));
        var second = GeneratedId.FromGuid(new Guid("01890a5d-ac96-774b-bcce-000000000002"));

        var set = new SortedSet<GeneratedId> { second, first };

        Assert.Equal([first, second], set);
    }
}
