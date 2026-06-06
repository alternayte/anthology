using Anthology.Kernel;
using Anthology.Modules.Tracking;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public class CuratedListTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ListId = Guid.NewGuid();
    private static readonly Guid TitleA = Guid.NewGuid();
    private static readonly Guid TitleB = Guid.NewGuid();
    private static readonly Guid TitleC = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CuratedListState Given(params IDomainEvent[] events) =>
        events.Aggregate(CuratedListState.Initial, CuratedList.Evolve);

    private static Result<IReadOnlyList<IDomainEvent>> When(CuratedListState state, ICuratedListCommand command) =>
        CuratedList.Decide(state, command);

    // --- Create ---

    [Fact]
    public void Create_on_new_stream_emits_ListCreated()
    {
        var state = Given();
        var result = When(state, new CreateList.Command("Favourites", "My top picks", ListVisibility.Private, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        var created = result.Value.Should().ContainSingle().Which.Should().BeOfType<ListCreated>().Subject;
        created.UserId.Should().Be(UserId);
        created.Name.Should().Be("Favourites");
        created.Description.Should().Be("My top picks");
        created.Visibility.Should().Be(ListVisibility.Private);
    }

    [Fact]
    public void Create_on_existing_stream_returns_conflict()
    {
        var state = Given(new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now));
        var result = When(state, new CreateList.Command("Another", null, ListVisibility.Private, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    // --- Update ---

    [Fact]
    public void Update_name_emits_ListRenamed()
    {
        var state = Given(new ListCreated(UserId, "Old Name", null, ListVisibility.Private, Now));
        var result = When(state, new UpdateList.Command("New Name", null, false, null, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle().Which.Should().BeOfType<ListRenamed>()
            .Which.Name.Should().Be("New Name");
    }

    [Fact]
    public void Update_with_no_changes_emits_no_events()
    {
        var state = Given(new ListCreated(UserId, "Favourites", "Desc", ListVisibility.Private, Now));
        var result = When(state, new UpdateList.Command(null, null, false, null, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void Update_multiple_fields_emits_multiple_events()
    {
        var state = Given(new ListCreated(UserId, "Old", "Old desc", ListVisibility.Private, Now));
        var result = When(state, new UpdateList.Command("New", "New desc", true, ListVisibility.PublicByLink, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(3);
        result.Value.Should().ContainSingle(e => e is ListRenamed);
        result.Value.Should().ContainSingle(e => e is ListDescriptionChanged);
        result.Value.Should().ContainSingle(e => e is ListVisibilityChanged);
    }

    [Fact]
    public void Update_on_deleted_list_returns_conflict()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ListDeleted(Now));
        var result = When(state, new UpdateList.Command("New", null, false, null, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Update_on_nonexistent_list_returns_not_found()
    {
        var state = Given();
        var result = When(state, new UpdateList.Command("Name", null, false, null, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.NotFound);
    }

    // --- Delete ---

    [Fact]
    public void Delete_existing_emits_ListDeleted()
    {
        var state = Given(new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now));
        var result = When(state, new DeleteList.Command(UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle().Which.Should().BeOfType<ListDeleted>();
    }

    [Fact]
    public void Delete_already_deleted_returns_conflict()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ListDeleted(Now));
        var result = When(state, new DeleteList.Command(UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Delete_nonexistent_returns_not_found()
    {
        var state = Given();
        var result = When(state, new DeleteList.Command(UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.NotFound);
    }

    // --- Add Item ---

    [Fact]
    public void Add_item_emits_ItemAddedToList_with_position_1()
    {
        var state = Given(new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now));
        var result = When(state, new AddItemToList.Command(TitleA, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        var added = result.Value.Should().ContainSingle().Which.Should().BeOfType<ItemAddedToList>().Subject;
        added.TitleId.Should().Be(TitleA);
        added.Position.Should().Be(1.0);
    }

    [Fact]
    public void Add_second_item_gets_position_2()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now));
        var result = When(state, new AddItemToList.Command(TitleB, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        var added = result.Value.Should().ContainSingle().Which.Should().BeOfType<ItemAddedToList>().Subject;
        added.Position.Should().Be(2.0);
    }

    [Fact]
    public void Add_duplicate_item_returns_conflict()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now));
        var result = When(state, new AddItemToList.Command(TitleA, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Add_item_to_deleted_list_returns_conflict()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ListDeleted(Now));
        var result = When(state, new AddItemToList.Command(TitleA, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Add_item_to_nonexistent_list_returns_not_found()
    {
        var state = Given();
        var result = When(state, new AddItemToList.Command(TitleA, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.NotFound);
    }

    // --- Remove Item ---

    [Fact]
    public void Remove_existing_item_emits_ItemRemovedFromList()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now));
        var result = When(state, new RemoveItemFromList.Command(TitleA, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle().Which.Should().BeOfType<ItemRemovedFromList>()
            .Which.TitleId.Should().Be(TitleA);
    }

    [Fact]
    public void Remove_nonexistent_item_returns_not_found()
    {
        var state = Given(new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now));
        var result = When(state, new RemoveItemFromList.Command(TitleA, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public void Remove_item_from_deleted_list_returns_conflict()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now),
            new ListDeleted(Now));
        var result = When(state, new RemoveItemFromList.Command(TitleA, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    // --- Reorder ---

    [Fact]
    public void Reorder_to_top_gives_position_less_than_min()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now),
            new ItemAddedToList(TitleB, 2.0, Now));
        var result = When(state, new ReorderItem.Command(TitleB, null, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        var reordered = result.Value.Should().ContainSingle().Which.Should().BeOfType<ListItemReordered>().Subject;
        reordered.TitleId.Should().Be(TitleB);
        reordered.NewPosition.Should().BeLessThan(1.0);
    }

    [Fact]
    public void Reorder_after_item_gives_position_between_items()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now),
            new ItemAddedToList(TitleB, 2.0, Now),
            new ItemAddedToList(TitleC, 3.0, Now));
        var result = When(state, new ReorderItem.Command(TitleC, TitleA, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        var reordered = result.Value.Should().ContainSingle().Which.Should().BeOfType<ListItemReordered>().Subject;
        reordered.NewPosition.Should().BeGreaterThan(1.0).And.BeLessThan(2.0);
    }

    [Fact]
    public void Reorder_after_last_item_gives_position_greater_than_max()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now),
            new ItemAddedToList(TitleB, 2.0, Now));
        var result = When(state, new ReorderItem.Command(TitleA, TitleB, UserId, ListId, Now));
        result.IsError.Should().BeFalse();
        var reordered = result.Value.Should().ContainSingle().Which.Should().BeOfType<ListItemReordered>().Subject;
        reordered.NewPosition.Should().BeGreaterThan(2.0);
    }

    [Fact]
    public void Reorder_nonexistent_item_returns_not_found()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now));
        var result = When(state, new ReorderItem.Command(TitleB, null, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public void Reorder_after_nonexistent_reference_returns_not_found()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now));
        var result = When(state, new ReorderItem.Command(TitleA, TitleB, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public void Reorder_on_deleted_list_returns_conflict()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now),
            new ListDeleted(Now));
        var result = When(state, new ReorderItem.Command(TitleA, null, UserId, ListId, Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    // --- Evolve ---

    [Fact]
    public void Evolve_tracks_version()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now));
        state.Version.Should().Be(2);
        state.Name.Should().Be("Favourites");
        state.Items.Should().ContainKey(TitleA);
    }

    [Fact]
    public void Evolve_remove_clears_item()
    {
        var state = Given(
            new ListCreated(UserId, "Favourites", null, ListVisibility.Private, Now),
            new ItemAddedToList(TitleA, 1.0, Now),
            new ItemRemovedFromList(TitleA));
        state.Items.Should().BeEmpty();
        state.Version.Should().Be(3);
    }

    // --- Command metadata ---

    [Fact]
    public void Commands_provide_StreamId_and_correlation_hints()
    {
        var userId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var command = new CreateList.Command("Test", null, ListVisibility.Private, userId, listId, DateTimeOffset.UtcNow);

        var esCommand = (IEventSourcedCommand)command;
        esCommand.StreamId.Should().Be(listId);

        var (hintUserId, hintContextId) = esCommand.GetCorrelationHints();
        hintUserId.Should().Be(userId);
        hintContextId.Should().BeNull();
    }
}
