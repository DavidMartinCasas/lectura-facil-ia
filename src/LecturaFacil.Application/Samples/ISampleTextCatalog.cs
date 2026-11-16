// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Samples;

/// <summary>Textos de ejemplo que se ofrecen a los usuarios para lanzar una adaptación con un clic.</summary>
public interface ISampleTextCatalog
{
    IReadOnlyList<SampleText> All();
}
