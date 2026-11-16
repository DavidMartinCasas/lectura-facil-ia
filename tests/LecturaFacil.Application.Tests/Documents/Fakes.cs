// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Application.Tests.Documents;

internal sealed class InMemoryDocumentRepository : IDocumentRepository
{
    public List<Document> Added { get; } = [];

    public Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Added.Add(document);
        return Task.CompletedTask;
    }
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
