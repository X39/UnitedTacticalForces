namespace X39.UnitedTacticalForces.WebApp;

public partial class EventSlot
{
    /// <summary>
    /// Checks the identifying slot data of the event slot.
    /// </summary>
    /// <remarks>
    /// Identifying data is <see cref="Title"/>, <see cref="Side"/> and <see cref="Group"/>.
    /// </remarks>
    /// <param name="obj">The <see cref="EventSlot"/> to compare against.</param>
    /// <returns><see langword="true"/> if both represent the same slot in a mission.</returns>
    public bool EqualIdentificationData(EventSlot obj)
    {
        return obj.Title == Title
               && obj.Side == Side
               && obj.Group == Group;
    }
    /// <summary>
    /// Checks the data fields of the slot whether they are the same or not.
    /// </summary>
    /// <remarks>
    /// Does not compare <see cref="SlotNumber"/>, <see cref="EventFk"/> and <see cref="AssignedToFk"/>.
    /// Non-Primitives (not including <see cref="string"/>) are never checked.
    /// </remarks>
    /// <param name="obj">The <see cref="EventSlot"/> to compare against.</param>
    /// <returns><see langword="true"/> if both represent the same slot with the same data in a mission.</returns>
    public bool EqualData(EventSlot obj)
    {
        return obj.Title == Title
               && obj.Side == Side
               && obj.Group == Group;
    }
    public EventSlot DeepCopy()
    {
        return new EventSlot
        {
            Event = Event?.DeepCopy(),
            Group = Group,
            Side = Side,
            Title = Title,
            AssignedTo = AssignedTo?.PartialCopy(),
            EventFk = EventFk,
            IsVisible = IsVisible,
            SlotNumber = SlotNumber,
            AssignedToFk = AssignedToFk,
            IsSelfAssignable = IsSelfAssignable,
        };
    }

    public EventSlot ShallowCopy()
    {
        return new EventSlot
        {
            Event            = null,
            Group            = Group,
            Side             = Side,
            Title            = Title,
            AssignedTo       = null,
            EventFk          = EventFk,
            IsVisible        = IsVisible,
            SlotNumber       = SlotNumber,
            AssignedToFk     = AssignedToFk,
            IsSelfAssignable = IsSelfAssignable,
        };
    }
}