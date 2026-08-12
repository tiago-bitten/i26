using i26.Core.DomainEvents;
using i26.Core.Entities;
using i26.Core.Ids;
using i26.Core.Pagination;

namespace i26.Core.Tests.Entities;

/// <summary>
/// What the base is for: an entity carries the id declared for it and nothing else, and two
/// instances of it are the same entity when they say the same id.
/// </summary>
public sealed class EntityTests
{
    [Fact]
    public void An_entity_is_born_with_an_id_of_its_own_type()
    {
        var course = new Course();

        Assert.NotEqual(default, course.Id);
        Assert.StartsWith("crs_", course.Id.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Two_entities_do_not_share_an_id_type()
    {
        // The whole point of the type parameter: this is the assertion the compiler makes for you.
        Assert.IsType<CourseId>(new Course().Id);
        Assert.IsType<StudentId>(new Student().Id);
    }

    [Fact]
    public void An_id_this_service_does_not_mint_is_left_for_whoever_does()
    {
        // Minted = false makes TypedId.New throw; the base leaves the id unset rather than throwing
        // on every materialised row.
        Assert.Equal(default, new Mirror().Id);
        Assert.NotEqual(default, new Mirror(UserId.Parse("usr_01h455vb4pex5vsknk084sn02q")).Id);
    }

    [Fact]
    public void The_same_id_is_the_same_entity()
    {
        var id = CourseId.New();

        Assert.Equal(new Course(id), new Course(id));
        Assert.Equal(new Course(id).GetHashCode(), new Course(id).GetHashCode());
    }

    [Fact]
    public void A_different_id_is_a_different_entity()
    {
        Assert.NotEqual(new Course(), new Course());
    }

    [Fact]
    public void An_entity_without_an_id_is_only_itself()
    {
        var mirror = new Mirror();

        // Both ids are default, and an id nobody assigned identifies nothing.
        Assert.Equal(mirror, mirror);
        Assert.NotEqual(new Mirror(), mirror);
    }

    [Fact]
    public void Two_types_that_happen_to_share_an_id_are_not_the_same_entity()
    {
        var id = CourseId.New();

        Assert.NotEqual<object>(new Course(id), new OtherCourse(id));
    }

    [Fact]
    public void An_entity_records_what_happened_to_it()
    {
        var course = new Course();

        course.Publish();

        var raised = Assert.IsType<CoursePublished>(Assert.Single(course.DomainEvents));
        Assert.Equal(course.Id, raised.Id);
    }

    [Fact]
    public void The_events_are_forgotten_once_taken()
    {
        var course = new Course();
        course.Publish();

        course.ClearDomainEvents();

        Assert.Empty(course.DomainEvents);
    }

    [Fact]
    public void An_entity_is_pageable_by_cursor_without_declaring_anything()
    {
        // Id and CreatedAt are already there, so the interface costs the entity nothing.
        ICursorPageable<CourseId> row = new Course();

        Assert.Equal(default, row.CreatedAt);
        Assert.NotEqual(default, row.Id);
    }
}
