using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Localization;
using MudBlazor;
using X39.BI.Config.Parsing;
using X39.UnitedTacticalForces.WebApp.Properties;
using X39.Util;

namespace X39.UnitedTacticalForces.WebApp.Helpers;

internal class ConfigToSlots
{
    private readonly IStringLocalizer<Language> _localizer;
    private readonly ISnackbar                  _snackbar;

    public ConfigToSlots(IStringLocalizer<Language> localizer, ISnackbar snackbar)
    {
        _localizer = localizer;
        _snackbar  = snackbar;
    }

    [Conditional("DEBUG")]
    static void DebugLine(string text) => Console.WriteLine(text);

    void GetObjectSidePair(ConfigClass configClass, out EArmaSide side)
    {
        var sidePair = configClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "side");
        if (sidePair is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities/{configClass.Identifier}/side");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        if (sidePair.Value is not string tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{configClass.Identifier}/side",
                "String",
                sidePair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        side = tmp switch
        {
            "West"       => EArmaSide.West,
            "East"       => EArmaSide.East,
            "Civilian"   => EArmaSide.Civilian,
            "Resistance" => EArmaSide.Resistance,
            _            => EArmaSide.Empty,
        };
    }

    void GetGroupObjectSidePair(ConfigClass groupConfigClass, ConfigClass groupItemConfigClass, out EArmaSide side)
    {
        var sidePair = groupItemConfigClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "side");
        if (sidePair is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/side");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        if (sidePair.Value is not string tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/side",
                "String",
                sidePair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        side = tmp switch
        {
            "West"       => EArmaSide.West,
            "East"       => EArmaSide.East,
            "Civilian"   => EArmaSide.Civilian,
            "Resistance" => EArmaSide.Resistance,
            _            => EArmaSide.Empty,
        };
    }

    bool GetItemDataTypePair(ConfigClass configClass, [NotNullWhen(true)] out string? dataType)
    {
        var dataTypePair = configClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "dataType");

        if (dataTypePair is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities/{configClass.Identifier}/dataType");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        if (dataTypePair.Value is not string tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{configClass.Identifier}/dataType",
                "String",
                dataTypePair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        dataType = tmp;

        return true;
    }

    bool GetGroupItemDataTypePair(
        ConfigClass groupConfigClass,
        ConfigClass groupItemConfigClass,
        [NotNullWhen(true)] out string? dataType)
    {
        var dataTypePair = groupItemConfigClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "dataType");

        if (dataTypePair is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/dataType");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        if (dataTypePair.Value is not string tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/dataType",
                "String",
                dataTypePair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        dataType = tmp;

        return true;
    }

    bool GetConfigMissionClass(
        ConfigCollection configCollection1,
        [NotNullWhen(true)] out ConfigClass? missionConfigClass1)
    {
        missionConfigClass1 = configCollection1
            .OfType<ConfigClass>()
            .SingleOrDefault((q) => q.Identifier is "Mission");
        if (missionConfigClass1 is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            return false;
        }

        return true;
    }

    bool GetMissionEntities(ConfigClass missionConfigClass1, [NotNullWhen(true)] out ConfigClass? entitiesConfigClass1)
    {
        entitiesConfigClass1 = missionConfigClass1.Children
            .OfType<ConfigClass>()
            .SingleOrDefault((q) => q.Identifier is "Entities");
        if (entitiesConfigClass1 is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities");
            _snackbar.Add(
                text,
                Severity.Error);
            Console.Error.WriteLine(text);
            return false;
        }

        return true;
    }

    bool GetGroupEntities(ConfigClass groupConfig, [NotNullWhen(true)] out ConfigClass? entitiesConfigClass1)
    {
        entitiesConfigClass1 = groupConfig.Children
            .OfType<ConfigClass>()
            .SingleOrDefault((q) => q.Identifier is "Entities");
        if (entitiesConfigClass1 is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities/{groupConfig.Identifier}/Entities");
            _snackbar.Add(
                text,
                Severity.Error);
            Console.Error.WriteLine(text);
            return false;
        }

        return true;
    }

    bool GetObjectAttributes(ConfigClass configClass, [NotNullWhen(true)] out ConfigClass? attributesConfigClass)
    {
        attributesConfigClass = configClass.Children
            .OfType<ConfigClass>()
            .SingleOrDefault((q) => q.Identifier is "Attributes");
        return attributesConfigClass is not null;
    }

    bool GetLogicsIsPlayable(ConfigClass configClass, out double isPlayable)
    {
        var isPlayablePair = configClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "isPlayable");

        if (isPlayablePair is null)
        {
            isPlayable = default;
            return false;
        }

        if (isPlayablePair.Value is not double tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{configClass.Identifier}/isPlayable",
                "String",
                isPlayablePair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        isPlayable = tmp;
        return true;
    }

    bool GetGroupLogicsIsPlayable(ConfigClass groupConfigClass, ConfigClass groupItemConfigClass, out double isPlayable)
    {
        var isPlayablePair = groupItemConfigClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "isPlayable");

        if (isPlayablePair is null)
        {
            isPlayable = default;
            return false;
        }

        if (isPlayablePair.Value is not double tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/isPlayable",
                "Double",
                isPlayablePair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        isPlayable = tmp;
        return true;
    }

    void GetLogicDescription(ConfigClass configClass, out string description)
    {
        var descriptionPair = configClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "description");
        if (descriptionPair is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities/{configClass.Identifier}/description");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        if (descriptionPair.Value is not string tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{configClass.Identifier}/description",
                "String",
                descriptionPair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        description = tmp;
    }

    void GetGroupLogicDescription(
        ConfigClass groupConfigClass,
        ConfigClass groupItemConfigClass,
        out string description)
    {
        var descriptionPair = groupItemConfigClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "description");
        if (descriptionPair is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/description");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        if (descriptionPair.Value is not string tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/description",
                "String",
                descriptionPair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        description = tmp;
    }

    bool GetObjectAttributesIsPlayable(
        ConfigClass configClass,
        ConfigClass attributesConfigClass,
        out double isPlayable)
    {
        var isPlayablePair = attributesConfigClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "isPlayable");

        if (isPlayablePair is null)
        {
            isPlayable = default;
            return false;
        }

        if (isPlayablePair.Value is not double tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{configClass.Identifier}/Attributes/isPlayable",
                "Double",
                isPlayablePair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        isPlayable = tmp;
        return true;
    }

    bool GetGroupObjectAttributesIsPlayable(
        ConfigClass groupConfigClass,
        ConfigClass groupItemConfigClass,
        ConfigClass attributesConfigClass,
        out double isPlayable)
    {
        var isPlayablePair = attributesConfigClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "isPlayable");

        if (isPlayablePair is null)
        {
            isPlayable = default;
            return false;
        }

        if (isPlayablePair.Value is not double tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/Attributes/isPlayable",
                "Double",
                isPlayablePair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        isPlayable = tmp;
        return true;
    }

    void GetObjectAttributesDescription(
        ConfigClass configClass,
        ConfigClass attributesConfigClass,
        out string description)
    {
        var descriptionPair = attributesConfigClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "description");
        if (descriptionPair is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities/{configClass.Identifier}/Attributes/description");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        if (descriptionPair.Value is not string tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{configClass.Identifier}/Attributes/description",
                "String",
                descriptionPair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        description = tmp;
    }

    void GetGroupObjectAttributesDescription(
        ConfigClass groupConfigClass,
        ConfigClass groupItemConfigClass,
        ConfigClass attributesConfigClass,
        out string description)
    {
        var descriptionPair = attributesConfigClass.Children
            .OfType<ConfigPair>()
            .SingleOrDefault((q) => q.Key is "description");
        if (descriptionPair is null)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_PathDoesNotExists_0path)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/Attributes/description");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        if (descriptionPair.Value is not string tmp)
        {
            var text = string.Format(
                _localizer[nameof(Language
                    .Pages_EventView_InvalidDataType_0path_1expected_2got)],
                $"/Mission/Entities/{groupConfigClass.Identifier}/Entities/{groupItemConfigClass.Identifier}/Attributes/description",
                "String",
                descriptionPair.Value?.GetType().Name ?? "null");
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            throw new Exception(text);
        }

        description = tmp;
    }

    public (IReadOnlyCollection<EventSlot>, bool) GetSlots(MemoryStream inputStream)
    {
        using var reader = new StreamReader(inputStream);
        {
            var buff = new char[1024];
            var len = reader.Read(buff);
            if (buff[..len].Any((c)=>char.IsControl(c) && !char.IsWhiteSpace(c)))
            {
                var text = _localizer[nameof(Language
                    .Pages_EventView_FailedToParseConfig_ParsedConfigIsBinarized)];
                _snackbar.Add(text, Severity.Error);
                Console.Error.WriteLine(text);
                return (ArraySegment<EventSlot>.Empty, false);
            }
        }
        inputStream.Seek(0, SeekOrigin.Begin);
        var (config, configParserError) = ConfigParser.Parse(reader);

        if (configParserError is not null)
        {
            var text = configParserError.ToString();
            _snackbar.Add(text, Severity.Error);
            Console.Error.WriteLine(text);
            return (ArraySegment<EventSlot>.Empty, false);
        }

        if (config is not ConfigCollection configCollection)
        {
            var text = _localizer[nameof(Language
                .Pages_EventView_FailedToParseConfig_ParsedConfigIsNoValidMissionFile)];
            _snackbar.Add(
                text,
                Severity.Error);
            Console.Error.WriteLine(text);
            return (ArraySegment<EventSlot>.Empty, false);
        }

        if (!GetConfigMissionClass(configCollection, out var missionConfigClass))
            return (ArraySegment<EventSlot>.Empty, false);
        if (!GetMissionEntities(missionConfigClass, out var entitiesConfigClass))
            return (ArraySegment<EventSlot>.Empty, false);
        var slots = new List<EventSlot>();
        var groupIndex = 0;
        foreach (var itemClassConfig in entitiesConfigClass.Children
                     .OfType<ConfigClass>())
        {
            if (!GetItemDataTypePair(itemClassConfig, out var itemDataType))
                return (ArraySegment<EventSlot>.Empty, false);
            DebugLine($"Handling: {itemClassConfig.Identifier} with type {itemDataType}");

            switch (itemDataType)
            {
                case "Logic":
                {
                    if (!GetLogicsIsPlayable(itemClassConfig, out var isPlayable))
                        break;
                    DebugLine($"{itemClassConfig.Identifier} has playable");
                    if (isPlayable > 0)
                    {
                        DebugLine($"{itemClassConfig.Identifier} is playable");
                        GetLogicDescription(itemClassConfig, out var description);
                        var descriptionSplatted = description.Split('@');
                        slots.Add(
                            new EventSlot
                            {
                                Title = descriptionSplatted.Length > 1
                                    ? descriptionSplatted.First()
                                    : description,
                                Group = descriptionSplatted.Length > 1
                                    ? string.Join("@", descriptionSplatted.Skip(1))
                                    : $"Alpha-{++groupIndex}",
                                Side             = EArmaSide.Logic,
                                IsSelfAssignable = true,
                            });
                    }

                    break;
                }
                case "Object":
                {
                    if (!GetObjectAttributes(itemClassConfig, out var attributesConfigClass))
                        break;
                    DebugLine($"{itemClassConfig.Identifier} has attributes");
                    if (!GetObjectAttributesIsPlayable(itemClassConfig, attributesConfigClass, out var isPlayable))
                        break;
                    DebugLine($"{itemClassConfig.Identifier} has playable");
                    if (isPlayable > 0)
                    {
                        DebugLine($"{itemClassConfig.Identifier} is playable");
                        GetObjectSidePair(itemClassConfig, out var side);
                        GetObjectAttributesDescription(itemClassConfig, attributesConfigClass, out var description);
                        var descriptionSplatted = description.Split('@', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        slots.Add(
                            new EventSlot
                            {
                                Title = descriptionSplatted.Length > 1
                                    ? descriptionSplatted.First()
                                    : description,
                                Group = descriptionSplatted.Length > 1
                                    ? string.Join("@", descriptionSplatted.Skip(1))
                                    : $"Alpha-{++groupIndex}",
                                Side             = side,
                                IsSelfAssignable = true,
                            });
                    }

                    break;
                }
                case "Group":
                {
                    if (!GetGroupEntities(itemClassConfig, out var groupEntitiesConfigClass))
                        return (ArraySegment<EventSlot>.Empty, false);
                    ++groupIndex;
                    foreach (var groupItemConfigClass in groupEntitiesConfigClass.Children
                                 .OfType<ConfigClass>())
                    {
                        if (!GetGroupItemDataTypePair(itemClassConfig, groupItemConfigClass, out var groupItemDataType))
                            return (ArraySegment<EventSlot>.Empty, false);
                        DebugLine($"Handling: {groupItemConfigClass.Identifier} with type {groupItemDataType}");

                        switch (groupItemDataType)
                        {
                            case "Logic":
                            {
                                if (!GetGroupLogicsIsPlayable(
                                        itemClassConfig,
                                        groupItemConfigClass,
                                        out var isPlayable))
                                    break;
                                DebugLine($"{groupItemConfigClass.Identifier} has playable");
                                if (isPlayable > 0)
                                {
                                    DebugLine($"{groupItemConfigClass.Identifier} is playable");
                                    GetGroupLogicDescription(
                                        itemClassConfig,
                                        groupItemConfigClass,
                                        out var description);
                                    var descriptionSplatted = description.Split('@', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                                    slots.Add(
                                        new EventSlot
                                        {
                                            Title = descriptionSplatted.Length > 1
                                                ? descriptionSplatted.First()
                                                : description,
                                            Group = descriptionSplatted.Length > 1
                                                ? string.Join("@", descriptionSplatted.Skip(1))
                                                : $"Alpha-{++groupIndex}",
                                            Side = EArmaSide.Logic,
                                            IsSelfAssignable = true,
                                        });
                                }

                                break;
                            }
                            case "Object":
                            {
                                if (!GetObjectAttributes(groupItemConfigClass, out var attributesConfigClass))
                                    break;
                                DebugLine($"{groupItemConfigClass.Identifier} has attributes");
                                if (!GetGroupObjectAttributesIsPlayable(
                                        itemClassConfig,
                                        groupItemConfigClass,
                                        attributesConfigClass,
                                        out var isPlayable))
                                    break;
                                DebugLine($"{groupItemConfigClass.Identifier} has playable");
                                if (isPlayable > 0)
                                {
                                    DebugLine($"{groupItemConfigClass.Identifier} is playable");
                                    GetGroupObjectSidePair(itemClassConfig, groupItemConfigClass, out var side);
                                    GetGroupObjectAttributesDescription(
                                        itemClassConfig,
                                        groupItemConfigClass,
                                        attributesConfigClass,
                                        out var description);
                                    var descriptionSplatted = description.Split('@', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                                    slots.Add(
                                        new EventSlot
                                        {
                                            Title = descriptionSplatted.Length > 1
                                                ? descriptionSplatted.First()
                                                : description,
                                            Group = descriptionSplatted.Length > 1
                                                ? string.Join("@", descriptionSplatted.Skip(1))
                                                : $"Alpha-{++groupIndex}",
                                            Side             = side,
                                            IsSelfAssignable = true,
                                        });
                                }

                                break;
                            }
                            case "Group":
                            {
                                break;
                            }
                        }
                    }

                    break;
                }
            }
        }

        return (slots, true);
    }
}