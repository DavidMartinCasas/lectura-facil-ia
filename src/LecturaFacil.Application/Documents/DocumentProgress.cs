// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Documents;

/// <summary>Estado de un documento y, mientras se adapta, la fase en la que está.</summary>
public sealed record DocumentProgress(DocumentStatus Status, ProcessingStage? Stage);
