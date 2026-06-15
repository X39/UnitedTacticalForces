using System.Collections;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Data;

public sealed class SlotGroup : IReadOnlyCollection<PlainEventSlotDto>
{
    private readonly IReadOnlyList<PlainEventSlotDto> _slots;

    public SlotGroup(string name, IEnumerable<PlainEventSlotDto> slots)
    {
        Name = name;
        _slots = slots
            .OrderBy((slot) => slot.SlotNumber ?? int.MaxValue)
            .ThenBy((slot) => slot.Title)
            .ToArray();
    }

    public string Name { get; }

    public int Count => _slots.Count;

    public IEnumerator<PlainEventSlotDto> GetEnumerator()
        => _slots.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
