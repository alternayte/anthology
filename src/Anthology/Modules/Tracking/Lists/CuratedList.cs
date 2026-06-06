using Anthology.Kernel;

namespace Anthology.Modules.Tracking;

public enum ListVisibility { Private, PublicByLink }

public sealed record ListCreated(Guid UserId, string Name, string? Description, ListVisibility Visibility, DateTimeOffset CreatedAt) : IDomainEvent;
public sealed record ListRenamed(string Name) : IDomainEvent;
public sealed record ListDescriptionChanged(string? Description) : IDomainEvent;
public sealed record ListVisibilityChanged(ListVisibility Visibility) : IDomainEvent;
public sealed record ListDeleted(DateTimeOffset DeletedAt) : IDomainEvent;
public sealed record ItemAddedToList(Guid TitleId, double Position, DateTimeOffset AddedAt) : IDomainEvent;
public sealed record ItemRemovedFromList(Guid TitleId) : IDomainEvent;
public sealed record ListItemReordered(Guid TitleId, double NewPosition) : IDomainEvent;

public sealed record CuratedListState(
    Guid UserId, string Name, string? Description, ListVisibility Visibility,
    bool IsDeleted, Dictionary<Guid, double> Items, int Version) : IAggregateState<CuratedListState>
{
    public static CuratedListState Initial => new(Guid.Empty, "", null, ListVisibility.Private, false, new(), 0);
    public static string StreamType => "curated_list";
}

public interface ICuratedListCommand : IEventSourcedCommand;

public sealed record CuratedListDto(Guid ListId, string Name, string? Description, ListVisibility Visibility, int ItemCount);

public static class CuratedList
{
    public static Result<IReadOnlyList<IDomainEvent>> Decide(CuratedListState state, ICuratedListCommand command) =>
        command switch
        {
            CreateList.Command c => HandleCreate(state, c),
            UpdateList.Command c => HandleUpdate(state, c),
            DeleteList.Command c => HandleDelete(state, c),
            AddItemToList.Command c => HandleAddItem(state, c),
            RemoveItemFromList.Command c => HandleRemoveItem(state, c),
            ReorderItem.Command c => HandleReorder(state, c),
            _ => Error.Unprocessable("lists.unknown_command", "Unrecognised command.")
        };

    public static CuratedListState Evolve(CuratedListState state, IDomainEvent e) => e switch
    {
        ListCreated c => state with
        {
            UserId = c.UserId, Name = c.Name, Description = c.Description,
            Visibility = c.Visibility, Version = state.Version + 1
        },
        ListRenamed r => state with { Name = r.Name, Version = state.Version + 1 },
        ListDescriptionChanged d => state with { Description = d.Description, Version = state.Version + 1 },
        ListVisibilityChanged v => state with { Visibility = v.Visibility, Version = state.Version + 1 },
        ListDeleted => state with { IsDeleted = true, Version = state.Version + 1 },
        ItemAddedToList a => state with
        {
            Items = new Dictionary<Guid, double>(state.Items) { [a.TitleId] = a.Position },
            Version = state.Version + 1
        },
        ItemRemovedFromList r => state with
        {
            Items = new Dictionary<Guid, double>(state.Items.Where(kv => kv.Key != r.TitleId)),
            Version = state.Version + 1
        },
        ListItemReordered o => state with
        {
            Items = new Dictionary<Guid, double>(state.Items) { [o.TitleId] = o.NewPosition },
            Version = state.Version + 1
        },
        _ => state
    };

    private static Result<IReadOnlyList<IDomainEvent>> HandleCreate(CuratedListState state, CreateList.Command c) =>
        state.Version > 0
            ? Error.Conflict("lists.already_exists", "List already exists.")
            : Ok(new ListCreated(c.UserId, c.Name, c.Description, c.Visibility, c.At));

    private static Result<IReadOnlyList<IDomainEvent>> HandleUpdate(CuratedListState state, UpdateList.Command c)
    {
        if (state.Version == 0)
            return Error.NotFound("lists.not_found", "List not found.");
        if (state.IsDeleted)
            return Error.Conflict("lists.deleted", "List has been deleted.");

        var events = new List<IDomainEvent>();

        if (c.Name is not null && c.Name != state.Name)
            events.Add(new ListRenamed(c.Name));

        if (c.DescriptionProvided && c.Description != state.Description)
            events.Add(new ListDescriptionChanged(c.Description));

        if (c.Visibility.HasValue && c.Visibility.Value != state.Visibility)
            events.Add(new ListVisibilityChanged(c.Visibility.Value));

        return Result<IReadOnlyList<IDomainEvent>>.FromValue(events);
    }

    private static Result<IReadOnlyList<IDomainEvent>> HandleDelete(CuratedListState state, DeleteList.Command c)
    {
        if (state.Version == 0)
            return Error.NotFound("lists.not_found", "List not found.");
        if (state.IsDeleted)
            return Error.Conflict("lists.already_deleted", "List is already deleted.");

        return Ok(new ListDeleted(c.At));
    }

    private static Result<IReadOnlyList<IDomainEvent>> HandleAddItem(CuratedListState state, AddItemToList.Command c)
    {
        if (state.Version == 0)
            return Error.NotFound("lists.not_found", "List not found.");
        if (state.IsDeleted)
            return Error.Conflict("lists.deleted", "List has been deleted.");
        if (state.Items.ContainsKey(c.TitleId))
            return Error.Conflict("lists.item_already_in_list", "Item is already in the list.");

        var position = state.Items.Count == 0 ? 1.0 : state.Items.Values.Max() + 1.0;
        return Ok(new ItemAddedToList(c.TitleId, position, c.At));
    }

    private static Result<IReadOnlyList<IDomainEvent>> HandleRemoveItem(CuratedListState state, RemoveItemFromList.Command c)
    {
        if (state.Version == 0)
            return Error.NotFound("lists.not_found", "List not found.");
        if (state.IsDeleted)
            return Error.Conflict("lists.deleted", "List has been deleted.");
        if (!state.Items.ContainsKey(c.TitleId))
            return Error.NotFound("lists.item_not_found", "Item is not in the list.");

        return Ok(new ItemRemovedFromList(c.TitleId));
    }

    private static Result<IReadOnlyList<IDomainEvent>> HandleReorder(CuratedListState state, ReorderItem.Command c)
    {
        if (state.Version == 0)
            return Error.NotFound("lists.not_found", "List not found.");
        if (state.IsDeleted)
            return Error.Conflict("lists.deleted", "List has been deleted.");
        if (!state.Items.ContainsKey(c.TitleId))
            return Error.NotFound("lists.item_not_found", "Item is not in the list.");

        double newPosition;
        if (c.AfterTitleId is null)
        {
            newPosition = state.Items.Values.Min() / 2.0;
        }
        else
        {
            if (!state.Items.TryGetValue(c.AfterTitleId.Value, out var afterPos))
                return Error.NotFound("lists.reference_item_not_found", "Reference item is not in the list.");

            var sorted = state.Items.Values.OrderBy(v => v).ToList();
            var afterIndex = sorted.IndexOf(afterPos);
            newPosition = afterIndex < sorted.Count - 1
                ? (afterPos + sorted[afterIndex + 1]) / 2.0
                : afterPos + 2.0;
        }

        return Ok(new ListItemReordered(c.TitleId, newPosition));
    }

    private static Result<IReadOnlyList<IDomainEvent>> Ok(IDomainEvent e) =>
        Result<IReadOnlyList<IDomainEvent>>.FromValue(new List<IDomainEvent> { e });
}
