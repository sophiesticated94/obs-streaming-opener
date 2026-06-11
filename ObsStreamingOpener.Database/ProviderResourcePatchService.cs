using System.Text.Json;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database;

public interface IProviderResourcePatchService
{
    ProviderResourcePatchResult Apply(ProviderResource entity, ProviderResourceUpsert resource, DateTimeOffset capturedAtUtc);

    IReadOnlyList<ProviderResourcePatchDto> ReadCompactHistory(string? json);
}

public sealed record ProviderResourcePatchResult(
    IReadOnlyList<ProviderResourcePatchFieldDto> Fields,
    IReadOnlyList<ProviderResourcePatchDto> History,
    string? HistoryJson);

public sealed class ProviderResourcePatchService : IProviderResourcePatchService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProviderResourcePatchResult Apply(ProviderResource entity, ProviderResourceUpsert resource, DateTimeOffset capturedAtUtc)
    {
        var observedKinds = ReadList<ProviderResourceKind>(entity.ObservedKindsJson);
        observedKinds.Add(resource.ResourceKind);
        var observedKindsJson = SerializeObservedKinds(observedKinds);
        var nextPrimaryKind = ChoosePrimaryKind(observedKinds);

        var patchFields = new List<ProviderResourcePatchFieldDto>();
        PatchIfChanged(nameof(ProviderResource.ResourceKind), entity.ResourceKind, nextPrimaryKind, v => entity.ResourceKind = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.Title), entity.Title, Normalize(resource.Title), v => entity.Title = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.Description), entity.Description, Normalize(resource.Description), v => entity.Description = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.Url), entity.Url, Normalize(resource.Url), v => entity.Url = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.ThumbnailUrl), entity.ThumbnailUrl, Normalize(resource.ThumbnailUrl), v => entity.ThumbnailUrl = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.Status), entity.Status, Normalize(resource.Status), v => entity.Status = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.PublishedAt), Normalize(entity.PublishedAt), Normalize(resource.PublishedAt), v => entity.PublishedAt = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.ScheduledStartAt), Normalize(entity.ScheduledStartAt), Normalize(resource.ScheduledStartAt), v => entity.ScheduledStartAt = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.ActualStartAt), Normalize(entity.ActualStartAt), Normalize(resource.ActualStartAt), v => entity.ActualStartAt = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.ActualEndAt), Normalize(entity.ActualEndAt), Normalize(resource.ActualEndAt), v => entity.ActualEndAt = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.DurationSeconds), entity.DurationSeconds, resource.DurationSeconds, v => entity.DurationSeconds = v, patchFields);
        PatchIfChanged(nameof(ProviderResource.ObservedKindsJson), NormalizeObservedKindsJson(entity.ObservedKindsJson), observedKindsJson, v => entity.ObservedKindsJson = v, patchFields);

        var history = ReadCompactHistory(entity.PatchHistoryJson).ToList();
        if (patchFields.Count > 0 && !IsDuplicateOfLastPatch(history, patchFields))
        {
            history.Add(new ProviderResourcePatchDto(capturedAtUtc.ToUniversalTime(), resource.Provider.ToString(), patchFields));
        }

        history = Compact(history).ToList();
        return new ProviderResourcePatchResult(patchFields, history, history.Count == 0 ? null : JsonSerializer.Serialize(history, JsonOptions));
    }

    public IReadOnlyList<ProviderResourcePatchDto> ReadCompactHistory(string? json)
        => Compact(ReadList<ProviderResourcePatchDto>(json)).ToList();

    private static void PatchIfChanged<T>(
        string field,
        T oldValue,
        T newValue,
        Action<T> apply,
        ICollection<ProviderResourcePatchFieldDto> patchFields)
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }

        patchFields.Add(new ProviderResourcePatchFieldDto(field, ToPatchValue(oldValue), ToPatchValue(newValue)));
        apply(newValue);
    }

    private static IEnumerable<ProviderResourcePatchDto> Compact(IEnumerable<ProviderResourcePatchDto> history)
    {
        ProviderResourcePatchDto? previous = null;
        foreach (var patch in history.Where(x => x.Fields.Count > 0).OrderBy(x => x.CapturedAtUtc))
        {
            if (previous is not null && SameFields(previous.Fields, patch.Fields))
            {
                continue;
            }

            previous = patch with { Fields = NormalizeFields(patch.Fields) };
            yield return previous;
        }
    }

    private static bool IsDuplicateOfLastPatch(IReadOnlyList<ProviderResourcePatchDto> history, IReadOnlyList<ProviderResourcePatchFieldDto> fields)
        => history.Count > 0 && SameFields(history[^1].Fields, fields);

    private static bool SameFields(IReadOnlyList<ProviderResourcePatchFieldDto> left, IReadOnlyList<ProviderResourcePatchFieldDto> right)
        => NormalizeFields(left).SequenceEqual(NormalizeFields(right));

    private static IReadOnlyList<ProviderResourcePatchFieldDto> NormalizeFields(IEnumerable<ProviderResourcePatchFieldDto> fields)
        => fields
            .Where(x => !string.IsNullOrWhiteSpace(x.Field))
            .Select(x => new ProviderResourcePatchFieldDto(x.Field.Trim(), Normalize(x.OldValue), Normalize(x.NewValue)))
            .OrderBy(x => x.Field, StringComparer.Ordinal)
            .ThenBy(x => x.OldValue, StringComparer.Ordinal)
            .ThenBy(x => x.NewValue, StringComparer.Ordinal)
            .ToList();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset? Normalize(DateTimeOffset? value)
        => value?.ToUniversalTime();

    private static string? ToPatchValue<T>(T value)
        => value switch
        {
            null => null,
            DateTimeOffset date => date.ToUniversalTime().ToString("O"),
            _ => value.ToString()
        };

    private static string NormalizeObservedKindsJson(string? json)
        => SerializeObservedKinds(ReadList<ProviderResourceKind>(json));

    private static string SerializeObservedKinds(IEnumerable<ProviderResourceKind> kinds)
        => JsonSerializer.Serialize(
            kinds.Distinct().OrderBy(x => x.ToString()).ToList(),
            JsonOptions);

    private static ProviderResourceKind ChoosePrimaryKind(IEnumerable<ProviderResourceKind> kinds)
        => kinds
            .Distinct()
            .OrderByDescending(x => x switch
            {
                ProviderResourceKind.LiveBroadcast => 5,
                ProviderResourceKind.Video => 4,
                ProviderResourceKind.PlaylistItem => 3,
                ProviderResourceKind.LiveStream => 2,
                ProviderResourceKind.Channel => 1,
                _ => 0
            })
            .FirstOrDefault();

    private static List<T> ReadList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
