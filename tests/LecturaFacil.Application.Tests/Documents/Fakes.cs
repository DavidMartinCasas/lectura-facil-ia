// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Application.Tests.Documents;

internal sealed class InMemoryDocumentRepository : IDocumentRepository
{
    public List<Document> Added { get; } = [];

    /// <summary>Estados guardados con <see cref="SaveAsync"/>, en orden.</summary>
    public List<DocumentStatus> SavedStatuses { get; } = [];

    /// <summary>Estados que devolverá <see cref="FindStatusForOwnerAsync"/>, uno por llamada (el último se repite).</summary>
    public Queue<DocumentStatus> ScriptedStatuses { get; } = new();

    public Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Added.Add(document);
        return Task.CompletedTask;
    }

    public Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken) =>
        Task.FromResult(Added.Find(document => document.Id == id));

    public Task<Document?> FindForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(Added.Find(document => document.Id == id && document.OwnerId == ownerId));

    public Task<DocumentStatus?> FindStatusForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken)
    {
        var document = Added.Find(candidate => candidate.Id == id && candidate.OwnerId == ownerId);
        if (document is null)
        {
            return Task.FromResult<DocumentStatus?>(null);
        }

        var status = ScriptedStatuses.Count switch
        {
            0 => document.Status,
            1 => ScriptedStatuses.Peek(),
            _ => ScriptedStatuses.Dequeue(),
        };
        return Task.FromResult<DocumentStatus?>(status);
    }

    public Task<IReadOnlyList<DocumentId>> ListAwaitingAdaptationAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentId>>(
            [.. Added.Where(document => DocumentStatusTransitions.IsAwaitingAdaptation(document.Status)).Select(document => document.Id)]);

    public Task SaveAsync(Document document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SavedStatuses.Add(document.Status);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingAdaptationQueue : IAdaptationQueue
{
    public List<DocumentId> Enqueued { get; } = [];

    public ValueTask EnqueueAsync(DocumentId id, CancellationToken cancellationToken)
    {
        Enqueued.Add(id);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Carga los prompts embebidos reales, para comprobar lo que de verdad se envía al modelo.</summary>
internal static class Prompts
{
    public static IPromptLoader Real { get; } = new LecturaFacil.Infrastructure.Llm.EmbeddedPromptLoader();
}

internal sealed class StubTextExtractor(Result<ExtractedText> result) : ITextExtractor
{
    public int Calls { get; private set; }

    public static StubTextExtractor Returning(string text, int pages = 1) =>
        new(Result<ExtractedText>.Success(new ExtractedText(text, pages)));

    public static StubTextExtractor Failing(Error error) => new(Result<ExtractedText>.Failure(error));

    public Task<Result<ExtractedText>> ExtractTextAsync(byte[] pdf, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(result);
    }
}

internal sealed class FixedCurrentUser(OwnerId ownerId) : ICurrentUser
{
    public OwnerId OwnerId { get; } = ownerId;
}

/// <summary>Flujo que dice tener una longitud distinta de la real, como una cabecera Content-Length falsa.</summary>
internal sealed class LyingLengthStream(byte[] content, long reportedLength) : MemoryStream(content)
{
    public override long Length => reportedLength;
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Devuelve siempre un hallazgo de frases cortas sobre las cinco primeras letras del texto.</summary>
internal sealed class StubGuidelineChecker(int findingsPerText = 1) : IGuidelineChecker
{
    public List<string> CheckedTexts { get; } = [];

    public GuidelineAnalysis Check(string text)
    {
        CheckedTexts.Add(text);
        var finding = Finding.At(GuidelineCatalog.Find(GuidelineCodes.ShortSentences), text, 0, 5, "Divide la frase.");
        return new GuidelineAnalysis([.. Enumerable.Repeat(finding, findingsPerText)], []);
    }
}
