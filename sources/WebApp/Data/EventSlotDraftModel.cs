using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Data;

public sealed class EventSlotDraftModel
{
    public Guid DraftKey { get; init; } = Guid.NewGuid();

    public int? SlotNumber { get; set; }

    public Guid? AssignedToFk { get; set; }

    public PlainUserDto? AssignedUser { get; set; }

    public PlainUserDto? AssignmentCandidate { get; set; }

    public string? AssignmentSearchText { get; set; }

    public string Group { get; set; } = "Alpha";

    public string Title { get; set; } = "Rifleman";

    public int Side { get; set; } = 2;

    public bool IsSelfAssignable { get; set; } = true;

    public bool IsVisible { get; set; } = true;

    public bool IsDeleted { get; set; }

    public bool IsPersisted
        => SlotNumber is not null;

    public static EventSlotDraftModel CreateDefault()
        => new();

    public static EventSlotDraftModel FromSlot(PlainEventSlotDto slot)
    {
        return new EventSlotDraftModel
        {
            SlotNumber       = slot.SlotNumber,
            AssignedToFk     = slot.AssignedToFk,
            Group            = string.IsNullOrWhiteSpace(slot.Group) ? "Ungrouped" : slot.Group,
            Title            = string.IsNullOrWhiteSpace(slot.Title) ? "Unassigned role" : slot.Title,
            Side             = slot.Side is { } side ? (int) side : 2,
            IsSelfAssignable = slot.IsSelfAssignable ?? true,
            IsVisible        = slot.IsVisible ?? true,
        };
    }

    public EventSlotCreationPayload ToCreationPayload()
    {
        return new EventSlotCreationPayload
        {
            AssignedToFk      = AssignedToFk,
            Group             = Group.Trim(),
            Title             = Title.Trim(),
            Side              = ToArmaSide(),
            IsSelfAssignable  = IsSelfAssignable,
            IsVisible         = IsVisible,
        };
    }

    public EventSlotUpdate ToUpdatePayload()
    {
        return new EventSlotUpdate
        {
            Group            = Group.Trim(),
            Title            = Title.Trim(),
            Side             = ToArmaSide(),
            IsSelfAssignable = IsSelfAssignable,
            IsVisible        = IsVisible,
        };
    }

    public IReadOnlyList<string> GetValidationErrors()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Group))
            errors.Add("Slot group is required.");
        if (string.IsNullOrWhiteSpace(Title))
            errors.Add("Slot title is required.");
        if (Side is < 0 or > 6)
            errors.Add("Slot side is invalid.");
        return errors;
    }

    public static string FormatSide(int side)
        => side switch
        {
            1 => "East",
            2 => "West",
            3 => "Resistance",
            4 => "Civilian",
            5 => "Ambient",
            6 => "Logic",
            _ => "Empty",
        };

    private EArmaSide ToArmaSide()
        => Side is >= 0 and <= 6 ? (EArmaSide) Side : EArmaSide.Empty;
}
